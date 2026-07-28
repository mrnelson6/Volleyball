using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Volleyball
{
    public enum MatchState { Serving, Rallying, PointScored, MatchOver }

    /// <summary>
    /// The match "brain": owns score, serve flow, rally state, the 3-touch rule and
    /// in/out scoring. Other components query it (CanTeamTouch) and report to it
    /// (RegisterTouch); it listens to the ball's ground-contact event to end rallies.
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
        public string Banner { get; private set; } = "";
        public TeamSide ServingTeam { get; private set; } = TeamSide.A;
        public int Touches { get; private set; }
        public TeamSide Possession { get; private set; }

        float _timer;
        VolleyPlayer _server;
        VolleyPlayer _lastToucher;
        bool _serveTossed; // the server has tossed the ball for a jump serve

        /// <summary>True from the serve until the receiving team first touches it. You may not
        /// block a serve, so blocks check this.</summary>
        public bool ServeInFlight { get; private set; }

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
            BeginServe(TeamSide.A);
        }

        /// <summary>
        /// Dress the court from the menu's pre-match choices. A campaign match casts all four
        /// slots explicitly (protagonist duo vs the region's opponent duo). Quick Play dresses
        /// the human as their pick and randomizes whichever sides were asked for — opponents
        /// only by default, so the teammate stays your usual partner. No-op when the menu
        /// didn't set anything (playing a scene directly from the editor).
        /// </summary>
        void ApplyMatchSetup()
        {
            if (MatchSetup.teamAIds != null && MatchSetup.teamBIds != null)
            {
                foreach (var p in players)
                {
                    bool isHumanSlot = p is PlayerController;
                    string id = p.team == TeamSide.A
                        ? MatchSetup.teamAIds[isHumanSlot ? 0 : 1]
                        : MatchSetup.teamBIds[p.halfSign < 0f ? 0 : 1];
                    CharacterSprites.Apply(p, CharacterRoster.Get(id));
                }
                return;
            }

            var pool = new List<CharacterDef>(CharacterRoster.All);

            if (MatchSetup.humanCharacterId != null)
            {
                CharacterDef chosen = CharacterRoster.Get(MatchSetup.humanCharacterId);
                foreach (var p in players)
                    if (p is PlayerController)
                    {
                        CharacterSprites.Apply(p, chosen);
                        pool.Remove(chosen);
                    }
            }

            foreach (var p in players)
            {
                if (!(p is AIController)) continue;
                bool randomize = p.team == TeamSide.A ? MatchSetup.randomizeTeammate
                                                      : MatchSetup.randomizeOpponents;
                if (!randomize) continue;
                if (pool.Count == 0) pool.AddRange(CharacterRoster.All);
                CharacterDef draw = pool[Random.Range(0, pool.Count)];
                pool.Remove(draw);
                CharacterSprites.Apply(p, draw);
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
                if (Banner == PerfectBanner) Banner = "";
            }

            if (Touches > maxTouches)
                EndRally(Possession.Other(), $"over {maxTouches} touches");
        }

        void HandleGroundHit(Vector3 point, Vector3 impactVel)
        {
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
            Banner = scorer == TeamSide.A ? "Point — You!" : "Point — Opponents";
            if (!string.IsNullOrEmpty(reason)) Banner += $" ({reason})";

            if (ScoreA >= pointsToWin || ScoreB >= pointsToWin)
            {
                State = MatchState.MatchOver;
                if (MatchSetup.isCampaign)
                    ResolveCampaignResult(ScoreA > ScoreB);
                else
                    Banner = (ScoreA > ScoreB ? "You win the match!" : "Opponents win the match!")
                             + "  —  press Hit to play again";
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
            Banner = "";
            _serveTossed = false;
            ServeInFlight = false;

            ResetPositions();
            _server = FirstPlayerOf(t);

            // server stands behind their own back line to serve
            if (_server != null)
            {
                _server.transform.position = new Vector3(
                    0f, 0f, CourtGeometry.SideSign(t) * (CourtGeometry.HalfDepth + 0.8f));
                _server.ResetState();
            }

            ball.Hold(ServePosition());
            _timer = aiServeDelay;

            GameAudio.PlayWhistle(); // referee authorises the serve
            VBLog.Event($"BEGIN SERVE team={t} server='{(_server != null ? _server.name : "?")}' score A={ScoreA} B={ScoreB}");
        }

        const string ServeHint = "Your serve —  J: underhand    K: toss, then Space + L: jump serve";
        const string TossHint = "Run in — Jump (Space) and Spike (L) at the peak!";
        const string PerfectBanner = "PERFECT SERVE!";

        string _powerBanner;      // the activation shout currently on the banner, if any
        float _powerBannerUntil;

        /// <summary>Flash a power-up activation on the banner for a moment. The timed clear
        /// only fires while the banner still shows this exact text, so serve hints and point
        /// banners are never stomped.</summary>
        public void ShowPowerBanner(string text)
        {
            Banner = text;
            _powerBanner = text;
            _powerBannerUntil = Time.time + 2.5f;
        }

        void Update()
        {
            if (_powerBanner != null)
            {
                if (Banner != _powerBanner) _powerBanner = null; // something else took over
                else if (Time.time >= _powerBannerUntil) { Banner = ""; _powerBanner = null; }
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
                    if (_server is AIController)
                    {
                        if (!_serveTossed) ball.Hold(ServePosition());
                        _timer -= Time.deltaTime;
                        if (_timer <= 0f) DoServe(); // AI always serves underhand
                    }
                    else
                    {
                        var gi = GameInput.Instance;
                        if (!_serveTossed)
                        {
                            ball.Hold(ServePosition());
                            Banner = ServeHint;
                            if (gi != null)
                            {
                                if (gi.BumpPressed) DoServe();        // underhand serve (J)
                                else if (gi.SetPressed) DoToss();      // toss up for a jump serve (K)
                            }
                        }
                        else
                        {
                            // ball is in the air: jump and Spike (L) to jump-serve it
                            Banner = TossHint;
                            Vector3 bp = ball.transform.position;
                            if (gi != null && gi.SpikePressed && _server != null
                                && !_server.IsGrounded && bp.y > 1.5f)
                                DoJumpServe();
                            else if (bp.y < 0.6f && ball.Body.linearVelocity.y <= 0f)
                                _serveTossed = false; // missed the toss — settle back to a held ball
                        }
                    }
                    break;

                case MatchState.PointScored:
                    _timer -= Time.deltaTime;
                    if (_timer <= 0f) BeginServe(ServingTeam);
                    break;

                case MatchState.MatchOver:
                    if (GameInput.Instance != null && GameInput.Instance.AnyHitPressed)
                    {
                        if (!MatchSetup.isCampaign) { RestartMatch(); break; }
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
                    break;
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
            Banner = "";
            _server?.Power.AddCharge(Cfg.powerChargePerTouch); // serves bypass RegisterTouch

            Vector3 target = VolleyPlayer.ApplyContactError(ServeTarget(),
                                                            _server != null ? _server.ServeError()
                                                                            : GameConfig.Instance.serveBaseError);
            ball.LaunchTo(target, 4f, ServingTeam, _server, HitType.Serve); // high apex to clear the net from behind the baseline
            ball.LockHits(0.45f);
            _server?.TriggerSwing(HitType.Serve); // animate the serve (DoServe bypasses TryHit)
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
            Banner = "";
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
            // low, driven trajectory; a perfect strike barely rises at all and goes out flat.
            float apex = perfect ? 0.35f : Mathf.Lerp(3.2f, 0.9f, quality);
            float depth = perfect ? 0.92f : Mathf.Lerp(0.62f, 0.85f, quality);

            Vector3 target = VolleyPlayer.ApplyContactError(ServeTarget(depth),
                                                            _server != null ? _server.ServeError()
                                                                            : GameConfig.Instance.serveBaseError);
            ball.LaunchTo(target, apex, ServingTeam, _server, HitType.Serve);
            ball.LockHits(0.45f);
            _server?.TriggerSwing(HitType.Spike); // spike motion for the jump serve
            GameAudio.PlayHit(HitType.Spike, ball.transform.position);
            if (perfect)
            {
                Banner = PerfectBanner;      // cleared when the receivers touch it
                GameAudio.PlayCrowd(0.4f);   // the crowd knows a perfect strike when it sees one
            }

            Vector3 sv = ball.Body.linearVelocity;
            VBLog.Event($"JUMP SERVE by '{(_server != null ? _server.name : "?")}' team={ServingTeam} touch#1 " +
                        $"apexSpeed={vertSpeed:F2} quality={quality:F2} perfect={perfect} apex={apex:F2} " +
                        $"depth={depth:F2} vel={VBLog.V(sv)} speed={sv.magnitude:F1} spin={ball.Spin:F0}");
        }

        void RestartMatch()
        {
            ScoreA = 0;
            ScoreB = 0;
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
        /// </summary>
        void ResolveCampaignResult(bool won)
        {
            CampaignSave save = SaveSystem.Load();
            if (save == null) { Banner = "You win!  —  press Hit to play again"; return; }

            RegionDef region = RegionRoster.Get(save.regionIndex);

            if (!won)
            {
                save.matchesLost++;
                save.attemptsThisMatch++;
                _campaignOutcome = CampaignOutcome.Retry;
                Banner = "Match lost  —  press Hit to retry";
            }
            else
            {
                save.matchesWon++;
                save.attemptsThisMatch = 0;
                save.matchIndex++;

                if (save.matchIndex < region.matches.Length)
                {
                    _campaignOutcome = CampaignOutcome.NextMatch;
                    Banner = $"Match won!  —  press Hit for match " +
                             $"{save.matchIndex + 1}/{region.matches.Length}";
                }
                else if (save.regionIndex + 1 < RegionRoster.All.Length)
                {
                    save.regionIndex++;
                    save.matchIndex = 0;
                    _campaignOutcome = CampaignOutcome.RegionComplete;
                    Banner = $"{region.displayName} conquered!  —  press Hit to travel on";
                }
                else
                {
                    // stay parked on the grand final so Continue can replay it
                    save.matchIndex = region.matches.Length - 1;
                    save.tourComplete = true;
                    _campaignOutcome = CampaignOutcome.TourComplete;
                    Banner = "WORLD TOUR CHAMPIONS!  —  press Hit to take the trophy home";
                }
            }

            SaveSystem.Save(save);
            VBLog.Event($"CAMPAIGN result won={won} -> region={save.regionIndex} " +
                        $"match={save.matchIndex} outcome={_campaignOutcome}");
        }

        Vector3 ServePosition()
        {
            if (_server == null) return new Vector3(0f, 1.5f, CourtGeometry.SideSign(ServingTeam) * CourtGeometry.HalfDepth * 0.9f);
            Vector3 p = _server.transform.position;
            return new Vector3(p.x, 1.5f, p.z + CourtGeometry.SideSign(ServingTeam) * 0.3f);
        }

        VolleyPlayer FirstPlayerOf(TeamSide t)
        {
            foreach (var p in players)
                if (p != null && p.team == t) return p;
            return players.Count > 0 ? players[0] : null;
        }

        void ResetPositions()
        {
            foreach (var p in players)
            {
                if (p == null) continue;
                float x = p.halfSign * CourtGeometry.HalfWidth * 0.45f;
                float z = CourtGeometry.SideSign(p.team) * CourtGeometry.HalfDepth * 0.55f;
                p.transform.position = new Vector3(x, 0f, z);
                p.ResetState();
            }
        }
    }
}
