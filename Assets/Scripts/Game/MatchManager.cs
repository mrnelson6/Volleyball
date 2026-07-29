using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Volleyball
{
    public enum MatchState { Serving, Rallying, PointScored, MatchOver }

    /// <summary>
    /// The match "brain": owns score, serve flow, rally state, the 3-touch rule and
    /// in/out scoring. Other components query it (CanTeamTouch) and report to it
    /// (RegisterTouch, OnServeIntent); it listens to the ball's ground-contact event to end
    /// rallies. This class is the single authority over match state — it reads NO input
    /// directly (player intents arrive as per-tick commands routed through the players), and
    /// announces through <see cref="BannerMessage"/> rather than viewer-perspective strings,
    /// so the whole thing can run server-side with clients merely mirroring it.
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        // Rules and timing are global — edit them in the GameConfig asset.
        static GameConfig Cfg => GameConfig.Instance;
        public int pointsToWin => Cfg.pointsToWin;
        public int maxTouches => Cfg.maxTouches;
        public float pointPauseSeconds => Cfg.pointPauseSeconds;
        public float aiServeDelay => Cfg.aiServeDelay;

        [Header("References (auto-found if empty)")]
        public BallController ball;
        public List<VolleyPlayer> players = new List<VolleyPlayer>();

        public int ScoreA { get; private set; }
        public int ScoreB { get; private set; }
        public MatchState State { get; private set; }
        public BannerMessage Banner { get; private set; } = BannerMessage.None;
        public TeamSide ServingTeam { get; private set; } = TeamSide.A;
        public int Touches { get; private set; }
        public TeamSide Possession { get; private set; }

        float _timer;
        VolleyPlayer _server;
        VolleyPlayer _lastToucher;
        bool _serveTossed; // the server has tossed the ball for a jump serve

        // Serve rotation, like real volleyball: the same player keeps serving while their
        // team holds the serve; when a team wins the serve back (side-out), its next player
        // in rotation steps up. Index 0 = team A, 1 = team B. B starts at -1 so its first
        // side-out advances to its first player.
        readonly int[] _serveRotation = { 0, -1 };
        TeamSide _lastServeTeam = TeamSide.None;

        /// <summary>True from the serve until the receiving team first touches it. You may not
        /// block a serve, so blocks check this.</summary>
        public bool ServeInFlight { get; private set; }

        /// <summary>The player currently holding the serve (mirrored to clients online).</summary>
        public VolleyPlayer CurrentServer => _server;

        /// <summary>Raised whenever a rally reset teleported everyone into formation — the
        /// network layer relays the new spots so clients snap instead of interpolating.</summary>
        public event System.Action PositionsReset;

        /// <summary>Raised when a rally ends: (scorer, reason). Network layer relays the
        /// moment (audio, power-up expiry) to clients.</summary>
        public event System.Action<TeamSide, string> RallyEnded;

        static bool IsCampaign => MatchSetup.Current != null && MatchSetup.Current.isCampaign;

        void Start()
        {
            if (ball == null) ball = FindAnyObjectByType<BallController>();
            if (players.Count == 0)
                players.AddRange(FindObjectsByType<VolleyPlayer>());

            if (ball != null) ball.OnGroundHit += HandleGroundHit;
            // every playable scene passes through here, so this also RESETS gravity/drag
            // to stock when the scene has no regional environment
            CourtEnvironment.ApplyFor(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, ball);
            ApplyMatchSetup();
            foreach (var p in players)
                p?.Power.ResetForMatch();

            // Online, the match starts when NetworkMatchState says so: the server waits for
            // every human slot to be claimed (BeginMatchServer), and clients never run the
            // state machine at all — they mirror it.
            if (NetworkSession.IsOnline) return;
            BeginServe(TeamSide.A);
        }

        /// <summary>Server-side kick-off once every human slot is filled.</summary>
        internal void BeginMatchServer() => BeginServe(TeamSide.A);

        /// <summary>Re-dress the court after the network config (with its slot casting)
        /// arrives on a client.</summary>
        internal void ReapplyMatchSetup() => ApplyMatchSetup();

        /// <summary>Let the network layer put a service banner up (e.g. "Waiting for players").</summary>
        internal void SetBannerServer(BannerMessage b) => Banner = b;

        /// <summary>The slot binder swapped a player's controller component (AI takeover of a
        /// dropped human, lobby casting) — repoint any internal references at the replacement,
        /// or a swapped-out server would park the serve state machine forever.</summary>
        internal void OnPlayerReplaced(VolleyPlayer old, VolleyPlayer fresh)
        {
            if (ReferenceEquals(_server, old)) _server = fresh;
            if (ReferenceEquals(_lastToucher, old)) _lastToucher = fresh;
        }

        /// <summary>
        /// Dress the court from the menu's pre-match config: every scene player is matched to
        /// its <see cref="MatchConfig.Slot"/> by team + court half and takes that slot's
        /// character. All ids in the config are already concrete (randoms were drawn when it
        /// was built), so this is a pure application — no rolls here. No-op when the menu
        /// didn't set anything (playing a scene directly from the editor).
        /// </summary>
        void ApplyMatchSetup()
        {
            MatchConfig cfg = MatchSetup.Current;
            if (cfg?.slots == null) return;

            foreach (var p in players)
            {
                if (p == null) continue;
                if (cfg.TryGetSlot(p.team, p.halfSign, out MatchConfig.Slot slot)
                    && !string.IsNullOrEmpty(slot.characterId))
                    CharacterSprites.Apply(p, CharacterRoster.Get(slot.characterId));
            }
        }

        void OnDestroy()
        {
            if (ball != null) ball.OnGroundHit -= HandleGroundHit;
        }

        /// <summary>A serve has to cross the net on its own: until the receivers touch it,
        /// the serving team may not play the ball — no rescuing a netted serve.</summary>
        public bool CanTeamTouch(TeamSide t)
            => State == MatchState.Rallying && !(ServeInFlight && t == ServingTeam);

        /// <summary>True while this player is the server and the serve hasn't happened yet.</summary>
        public bool IsServePhaseFor(VolleyPlayer p) => State == MatchState.Serving && _server == p;

        /// <summary>True once the server has tossed for a jump serve (ball in the air, not yet
        /// struck). The behind-the-baseline clamp releases so the server can chase their toss.</summary>
        public bool ServeTossed => _serveTossed;

        public void RegisterTouch(TeamSide t, VolleyPlayer p)
        {
            if (State != MatchState.Rallying) return;

            // any participation charges the meter — even a touch that turns out to be a fault
            p?.Power.AddCharge(Cfg.powerChargePerTouch);

            // a player may not contact the ball twice in a row
            if (p != null && p == _lastToucher)
            {
                VBLog.Event($"FAULT double-contact by '{p.name}' team={t}");
                EndRally(t.Other(), "double contact");
                return;
            }
            _lastToucher = p;

            if (t == Possession) Touches++;
            else { Possession = t; Touches = 1; }

            if (t != ServingTeam)
            {
                ServeInFlight = false; // serve received — blocking is legal again
                if (Banner.kind == BannerKind.Perfect) Banner = BannerMessage.None;
            }

            if (Touches > maxTouches)
                EndRally(Possession.Other(), $"over {maxTouches} touches");
        }

        void HandleGroundHit(Vector3 point, Vector3 impactVel)
        {
            if (NetworkSession.IsRemoteClient) return; // scoring is the server's call alone
            if (State != MatchState.Rallying) return;

            bool inBounds = CourtGeometry.InBounds(point);
            TeamSide landingSide = CourtGeometry.SideOf(point);
            TeamSide scorer = inBounds ? landingSide.Other() : ball.LastTouchTeam.Other();
            if (scorer == TeamSide.None) scorer = landingSide.Other();

            VBLog.Event($"BALL LANDED at {VBLog.V(point)} impactVel={VBLog.V(impactVel)} " +
                        $"inBounds={inBounds} side={landingSide} lastTouch={ball.LastTouchTeam}");
            GameAudio.PlayLanding(inBounds, point);
            EndRally(scorer, inBounds ? "in-bounds" : "out");
        }

        void EndRally(TeamSide scorer, string reason)
        {
            // every power-up effect ends with the rally (clean slate for the next serve),
            // and everyone banks the participation chunk — win or lose the point
            foreach (var p in players)
                p?.Power.OnRallyEnd(Cfg.powerChargePerRally);
            PowerUpDirector.RevertAll();
            _powerBanner = null;

            if (scorer == TeamSide.A) ScoreA++;
            else if (scorer == TeamSide.B) ScoreB++;

            VBLog.Event($"RALLY END scorer={scorer} reason='{reason}' -> score A={ScoreA} B={ScoreB}");

            ServingTeam = scorer;
            Banner = BannerMessage.Of(BannerKind.Point, scorer, reason);
            RallyEnded?.Invoke(scorer, reason);

            if (ScoreA >= pointsToWin || ScoreB >= pointsToWin)
            {
                State = MatchState.MatchOver;
                if (IsCampaign)
                    ResolveCampaignResult(ScoreA > ScoreB);
                else
                    Banner = BannerMessage.Of(BannerKind.MatchWon,
                                              ScoreA > ScoreB ? TeamSide.A : TeamSide.B);
                GameAudio.PlayMatchWin();
            }
            else
            {
                State = MatchState.PointScored;
                _timer = pointPauseSeconds;
                GameAudio.PlayPoint(scorer == TeamSide.A);
            }
        }

        void BeginServe(TeamSide t)
        {
            ServingTeam = t;
            Possession = t;
            Touches = 0;
            _lastToucher = null;
            State = MatchState.Serving;
            Banner = BannerMessage.None;
            _serveTossed = false;
            ServeInFlight = false;

            ResetPositions();
            _server = NextServerOf(t);

            // the local human isn't always the server — announce whose serve it is when an
            // AI steps up (each HUD decides whether its viewer cares)
            if (_server != null && !_server.IsHuman)
                Banner = BannerMessage.Of(BannerKind.AiServing, t, _server.Character.displayName);

            // server stands behind their own back line to serve
            if (_server != null)
            {
                _server.TeleportTo(new Vector3(
                    0f, 0f, CourtGeometry.SideSign(t) * (CourtGeometry.HalfDepth + 0.8f)));
                _server.ResetState();
            }

            ball.Hold(ServePosition());
            _timer = aiServeDelay;

            GameAudio.PlayWhistle(); // referee authorises the serve
            VBLog.Event($"BEGIN SERVE team={t} server='{(_server != null ? _server.name : "?")}' score A={ScoreA} B={ScoreB}");
            PositionsReset?.Invoke();
        }

        string _powerBanner;      // the activation shout currently on the banner, if any
        float _powerBannerUntil;

        /// <summary>Flash a power-up activation on the banner for a moment. The timed clear
        /// only fires while the banner still shows this exact shout, so serve hints and point
        /// banners are never stomped.</summary>
        public void ShowPowerBanner(string text)
        {
            Banner = BannerMessage.Of(BannerKind.PowerShout, TeamSide.None, text);
            _powerBanner = text;
            _powerBannerUntil = Time.time + 2.5f;
        }

        /// <summary>
        /// A serve action from the server's tick command: the underhand serve, the jump-serve
        /// toss, or the strike on the tossed ball. Ignored from anyone but the current human
        /// server. The strike is judged on the server's CURRENT tick state (their vertical
        /// speed at the press) — the timing skill stays with the player who timed it.
        /// </summary>
        public void OnServeIntent(VolleyPlayer p, ServeIntent intent)
        {
            if (State != MatchState.Serving || p == null || p != _server) return;

            if (!_serveTossed)
            {
                if (intent == ServeIntent.Underhand) DoServe();
                else if (intent == ServeIntent.Toss) DoToss();
            }
            else if (intent == ServeIntent.JumpStrike)
            {
                // ball is in the air: jump and strike it near the peak
                Vector3 bp = ball.transform.position;
                if (!p.IsGrounded && bp.y > 1.5f) DoJumpServe();
            }
        }

        /// <summary>A human's hit press once the match is over: rematch, or advance/retry the
        /// campaign. (Routed from the player tick — the match itself reads no input.)</summary>
        public void OnContinuePressed()
        {
            if (State != MatchState.MatchOver) return;

            if (!IsCampaign) { RestartMatch(); return; }
            switch (_campaignOutcome)
            {
                case CampaignOutcome.RegionComplete:
                case CampaignOutcome.TourComplete:
                    // back to the tour board so the player sees the ladder advance
                    MainMenuController.openCampaignOnLoad = true;
                    SceneFlow.LoadMenu();
                    break;
                default: // next match or a retry — both just relaunch from the save
                    SceneFlow.LoadCampaignMatch();
                    break;
            }
        }

        /// <summary>Adopt authoritative state mirrored from the network (clients only — the
        /// local state machine is bypassed entirely while mirroring).</summary>
        internal void MirrorNetworkState(int scoreA, int scoreB, MatchState state,
                                         TeamSide servingTeam, TeamSide possession, int touches,
                                         bool serveInFlight, bool serveTossed, BannerMessage banner,
                                         VolleyPlayer server)
        {
            ScoreA = scoreA;
            ScoreB = scoreB;
            State = state;
            ServingTeam = servingTeam;
            Possession = possession;
            Touches = touches;
            ServeInFlight = serveInFlight;
            _serveTossed = serveTossed;
            Banner = banner;
            _server = server;
        }

        void Update()
        {
            // a mirroring client runs no match logic — every field below is written by
            // MirrorNetworkState, and even the debug keys must not touch mirrored state
            if (NetworkSession.IsRemoteClient) return;

            if (_powerBanner != null)
            {
                bool stillShowing = Banner.kind == BannerKind.PowerShout && Banner.text == _powerBanner;
                if (!stillShowing) _powerBanner = null; // something else took over
                else if (Time.time >= _powerBannerUntil) { Banner = BannerMessage.None; _powerBanner = null; }
            }

            // debug shortcut: instantly win the current match (campaign advances normally)
            if ((Application.isEditor || Debug.isDebugBuild)
                && State != MatchState.MatchOver
                && Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
            {
                VBLog.Event("DEBUG F9 — auto-win match");
                ScoreA = pointsToWin - 1;
                EndRally(TeamSide.A, "debug win");
                return;
            }

            switch (State)
            {
                case MatchState.Serving:
                    // No server assigned means the match hasn't actually begun (online: still
                    // waiting for players — BeginServe hasn't run). Without this guard the
                    // zero-initialised state machine "serves" by itself on the first frame.
                    if (_server == null) break;
                    if (!_server.IsHuman)
                    {
                        if (!_serveTossed) ball.Hold(ServePosition());
                        _timer -= Time.deltaTime;
                        if (_timer <= 0f) DoServe(); // AI always serves underhand
                    }
                    else
                    {
                        // a human serves on their own tick commands (OnServeIntent) — this
                        // just holds the ball at their hand and keeps the hint fresh
                        if (!_serveTossed)
                        {
                            ball.Hold(ServePosition());
                            Banner = BannerMessage.Of(BannerKind.ServeHint, ServingTeam,
                                                      _server.Character.displayName);
                        }
                        else
                        {
                            Banner = BannerMessage.Of(BannerKind.TossHint, ServingTeam,
                                                      _server.Character.displayName);
                            Vector3 bp = ball.transform.position;
                            if (bp.y < 0.6f && ball.Body.linearVelocity.y <= 0f)
                                _serveTossed = false; // missed the toss — settle back to a held ball
                        }
                    }
                    break;

                case MatchState.PointScored:
                    _timer -= Time.deltaTime;
                    if (_timer <= 0f) BeginServe(ServingTeam);
                    break;

                // MatchOver: waits on OnContinuePressed from a human's command
            }
        }

        void DoServe()
        {
            State = MatchState.Rallying;
            Possession = ServingTeam;
            Touches = 1;
            _lastToucher = _server;
            _serveTossed = false;
            ServeInFlight = true;
            Banner = BannerMessage.None;
            _server?.Power.AddCharge(Cfg.powerChargePerTouch); // serves bypass RegisterTouch

            Vector3 target = VolleyPlayer.ApplyContactError(ServeTarget(),
                                                            _server != null ? _server.ServeError()
                                                                            : GameConfig.Instance.serveBaseError);
            ball.LaunchTo(target, 4f, ServingTeam, _server, HitType.Serve); // high apex to clear the net from behind the baseline
            ball.LockHits(0.45f);
            _server?.TriggerSwing(HitType.Serve); // animate the serve (DoServe bypasses the hit path)
            GameAudio.PlayHit(HitType.Serve, ball.transform.position); // the serve contact (DoServe bypasses LogContact)

            Vector3 sv = ball.Body.linearVelocity;
            VBLog.Event($"Serve by '{(_server != null ? _server.name : "?")}' team={ServingTeam} touch#1 " +
                        $"vel={VBLog.V(sv)} speed={sv.magnitude:F1} spin={ball.Spin:F0}");
        }

        /// <summary>Serve aim at a fraction of the receivers' court depth (1 = their baseline).
        /// Serves carry deep on normal air — and simply land shorter through heavy jungle air
        /// (the environment moves the landing spot; the launch never changes per region).</summary>
        Vector3 ServeTarget(float depthFrac = 0.75f)
            => new Vector3(0f, 0f, CourtGeometry.SideSign(ServingTeam.Other()) * CourtGeometry.HalfDepth * depthFrac);

        /// <summary>Where the jump-serve toss wants to be struck — the sweet spot the toss
        /// descends through above the baseline, and the top of the contact-quality ramp.</summary>
        const float JumpServeIdealContactY = 3.2f;

        /// <summary>
        /// Toss the ball up for a jump serve: high, and thrown forward so it comes down over
        /// the server's own baseline — regardless of how the server is moving. The skill is in
        /// what follows: time the run forward and the jump to meet the ball at the peak of the
        /// jump, near the line.
        /// </summary>
        void DoToss()
        {
            _serveTossed = true;

            const float upSpeed = 7.5f; // a high toss: plenty of time to run in under it

            // How long until the toss descends back through ideal contact height — then throw
            // it forward exactly hard enough to be above our baseline at that moment.
            float g = -Physics.gravity.y;
            Vector3 bp = ball.transform.position;
            float drop = JumpServeIdealContactY - bp.y;
            float tFlight = (upSpeed + Mathf.Sqrt(Mathf.Max(upSpeed * upSpeed - 2f * g * drop, 0.01f))) / g;

            float baselineZ = CourtGeometry.SideSign(ServingTeam) * CourtGeometry.HalfDepth;
            float toward = CourtGeometry.SideSign(ServingTeam.Other()); // net-ward direction
            float dz = (baselineZ - bp.z) * toward;                     // forward distance to the line
            float forwardSpeed = Mathf.Clamp(dz / tFlight, 0f, 3.5f) * toward;

            ball.Toss(upSpeed, new Vector3(0f, 0f, forwardSpeed));
            _server?.TriggerSwing(HitType.Set); // small toss motion
            VBLog.Event($"SERVE TOSS by '{(_server != null ? _server.name : "?")}' team={ServingTeam} " +
                        $"forward={forwardSpeed:F2} tFlight={tFlight:F2}");
        }

        /// <summary>
        /// Spike the tossed ball over as a jump serve. Contact height matters: the higher you
        /// hit it (the top of the jump), the flatter and faster the serve.
        /// </summary>
        void DoJumpServe()
        {
            State = MatchState.Rallying;
            Possession = ServingTeam;
            Touches = 1;
            _lastToucher = _server;
            _serveTossed = false;
            ServeInFlight = true;
            Banner = BannerMessage.None;
            _server?.Power.AddCharge(Cfg.powerChargePerTouch); // serves bypass RegisterTouch

            // Timing quality is measured off ONE thing: how close the server is to the peak
            // of their jump at the strike (vertical speed hits zero exactly at the apex).
            // Quality drives the pace two ways the ballistic solver keeps honest (the serve
            // always lands where it's aimed): a flatter arc AND a deeper target. Strike inside
            // the apex window and both jump off a cliff — a PERFECT serve, flat to the back line.
            float vertSpeed = _server != null ? Mathf.Abs(_server.VerticalVelocity) : float.MaxValue;
            float takeoff = _server != null ? _server.jumpSpeed : 1f;
            float quality = 1f - Mathf.Clamp01(vertSpeed / Mathf.Max(takeoff, 0.01f));
            bool perfect = vertSpeed <= 1.2f; // ~±0.12s around the apex

            // The apex here is measured ABOVE the (already ~3m high) contact, so anything
            // over ~1 still reads as a rainbow. The ramp runs from a slow floater down to a
            // low, driven trajectory. A perfect strike ignores the apex model entirely: it
            // takes the FASTEST flight that still clears the tape and lands on the back
            // line — a flat laser, and flatter still off a higher contact (tall animals,
            // Sky Jump, Moon Ball raise the strike point and speed it up further).
            float apex = perfect ? 0.35f : Mathf.Lerp(3.2f, 0.9f, quality);
            float depth = perfect ? 0.95f : Mathf.Lerp(0.62f, 0.85f, quality);

            Vector3 target = VolleyPlayer.ApplyContactError(ServeTarget(depth),
                                                            _server != null ? _server.ServeError()
                                                                            : GameConfig.Instance.serveBaseError);
            float flight = perfect ? FastestServeFlight(ball.transform.position, target) : 0f;
            ball.LaunchTo(target, apex, ServingTeam, _server, HitType.Serve, flight);
            ball.LockHits(0.45f);
            _server?.TriggerSwing(HitType.Spike); // spike motion for the jump serve
            GameAudio.PlayHit(HitType.Spike, ball.transform.position);
            if (perfect)
            {
                Banner = BannerMessage.Of(BannerKind.Perfect); // cleared when the receivers touch it
                GameAudio.PlayCrowd(0.4f);   // the crowd knows a perfect strike when it sees one
            }

            Vector3 sv = ball.Body.linearVelocity;
            VBLog.Event($"JUMP SERVE by '{(_server != null ? _server.name : "?")}' team={ServingTeam} touch#1 " +
                        $"apexSpeed={vertSpeed:F2} quality={quality:F2} perfect={perfect} apex={apex:F2} " +
                        $"depth={depth:F2} vel={VBLog.V(sv)} speed={sv.magnitude:F1} spin={ball.Spin:F0}");
        }

        /// <summary>
        /// The shortest ballistic flight time from <paramref name="start"/> to
        /// <paramref name="target"/> that still crosses the net plane with clearance —
        /// i.e. the fastest, flattest serve physics allows from this contact. Horizontal
        /// speed is constant in flight, so the net crossing sits at a fixed fraction of the
        /// flight time. Returns 0 (caller falls back to the arc solver) if even the slowest
        /// candidate can't clear — e.g. a strike from very low.
        /// </summary>
        static float FastestServeFlight(Vector3 start, Vector3 target)
        {
            float g = -Physics.gravity.y;
            float netFrac = Mathf.Clamp01(Mathf.Abs(start.z)
                                          / Mathf.Max(Mathf.Abs(target.z - start.z), 0.01f));
            float clearance = CourtGeometry.NetHeight + 0.25f; // tape + ball radius margin

            for (float t = 0.55f; t <= 1.4f; t += 0.05f)
            {
                float vy = (target.y - start.y + 0.5f * g * t * t) / t;
                float tn = netFrac * t;
                float yNet = start.y + vy * tn - 0.5f * g * tn * tn;
                if (yNet >= clearance) return t;
            }
            return 0f;
        }

        void RestartMatch()
        {
            ScoreA = 0;
            ScoreB = 0;
            _serveRotation[0] = 0;
            _serveRotation[1] = -1;
            _lastServeTeam = TeamSide.None;
            foreach (var p in players)
                p?.Power.ResetForMatch();
            PowerUpDirector.RevertAll();
            BeginServe(TeamSide.A);
        }

        enum CampaignOutcome { None, Retry, NextMatch, RegionComplete, TourComplete }
        CampaignOutcome _campaignOutcome = CampaignOutcome.None;

        /// <summary>
        /// Write a campaign match result to the save and set the end-of-match banner. A win
        /// advances the tournament ladder (and the region / the whole tour on rollover); a
        /// loss counts an attempt. Saved immediately so quitting at the banner loses nothing.
        /// (Campaign banners stay pre-rendered text: the campaign is the local player's story,
        /// and its lines never need another viewer's perspective.)
        /// </summary>
        void ResolveCampaignResult(bool won)
        {
            CampaignSave save = SaveSystem.Load();
            if (save == null)
            {
                Banner = BannerMessage.Raw("You win!  —  press Hit to play again");
                return;
            }

            RegionDef region = RegionRoster.Get(save.regionIndex);

            if (!won)
            {
                save.matchesLost++;
                save.attemptsThisMatch++;
                _campaignOutcome = CampaignOutcome.Retry;
                Banner = BannerMessage.Raw("Match lost  —  press Hit to retry");
            }
            else
            {
                save.matchesWon++;
                save.attemptsThisMatch = 0;
                save.matchIndex++;

                if (save.matchIndex < region.matches.Length)
                {
                    _campaignOutcome = CampaignOutcome.NextMatch;
                    Banner = BannerMessage.Raw($"Match won!  —  press Hit for match " +
                                               $"{save.matchIndex + 1}/{region.matches.Length}");
                }
                else if (save.regionIndex + 1 < RegionRoster.All.Length)
                {
                    save.regionIndex++;
                    save.matchIndex = 0;
                    _campaignOutcome = CampaignOutcome.RegionComplete;
                    Banner = BannerMessage.Raw($"{region.displayName} conquered!  —  press Hit to travel on");
                }
                else
                {
                    // stay parked on the grand final so Continue can replay it
                    save.matchIndex = region.matches.Length - 1;
                    save.tourComplete = true;
                    _campaignOutcome = CampaignOutcome.TourComplete;
                    Banner = BannerMessage.Raw("WORLD TOUR CHAMPIONS!  —  press Hit to take the trophy home");
                }
            }

            SaveSystem.Save(save);
            VBLog.Event($"CAMPAIGN result won={won} -> region={save.regionIndex} " +
                        $"match={save.matchIndex} outcome={_campaignOutcome}");
        }

        Vector3 ServePosition()
        {
            if (_server == null) return new Vector3(0f, 1.5f, CourtGeometry.SideSign(ServingTeam) * CourtGeometry.HalfDepth * 0.9f);
            Vector3 p = _server.SimPosition;
            return new Vector3(p.x, 1.5f, p.z + CourtGeometry.SideSign(ServingTeam) * 0.3f);
        }

        /// <summary>The player whose turn it is to serve for <paramref name="t"/>: rotation
        /// advances only on a side-out, so a scoring server keeps the ball.</summary>
        VolleyPlayer NextServerOf(TeamSide t)
        {
            var teamPlayers = new List<VolleyPlayer>();
            foreach (var p in players)
                if (p != null && p.team == t) teamPlayers.Add(p);
            if (teamPlayers.Count == 0) return players.Count > 0 ? players[0] : null;

            int idx = t == TeamSide.A ? 0 : 1;
            if (t != _lastServeTeam && _lastServeTeam != TeamSide.None)
                _serveRotation[idx]++;
            _lastServeTeam = t;
            return teamPlayers[_serveRotation[idx] % teamPlayers.Count];
        }

        void ResetPositions()
        {
            foreach (var p in players)
            {
                if (p == null) continue;
                float x = p.halfSign * CourtGeometry.HalfWidth * 0.45f;
                float z = CourtGeometry.SideSign(p.team) * CourtGeometry.HalfDepth * 0.55f;
                p.TeleportTo(new Vector3(x, 0f, z));
                p.ResetState();
            }
        }
    }
}
