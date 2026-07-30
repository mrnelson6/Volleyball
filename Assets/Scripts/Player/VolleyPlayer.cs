using UnityEngine;

namespace Volleyball
{
    /// <summary>The kind of contact a player makes with the ball.</summary>
    public enum HitType { Bump, Set, Spike, Serve, Block, Dive }

    /// <summary>
    /// Shared movement/jump/hit behaviour for both the human player and the AI, stepped as a
    /// deterministic fixed-tick simulation: controllers produce one <see cref="InputCommand"/>
    /// per tick (<see cref="GetCommand"/>) and <see cref="Simulate"/> advances
    /// <see cref="PlayerSimState"/> from nothing else. The transform is only the rendered
    /// view, interpolated between the last two ticks in Update — gameplay must read
    /// <see cref="SimPosition"/>/<see cref="GroundPosition"/>, never the transform.
    /// Simulation code never touches cameras or UI; contact effects (audio, swing poses,
    /// sand marks) flow out through events and the view.
    /// </summary>
    public abstract class VolleyPlayer : MonoBehaviour
    {
        [Header("Team (per-player)")]
        public TeamSide team = TeamSide.A;

        [Tooltip("Half of the court this player favours: -1 = left (x<0), +1 = right.")]
        public float halfSign = -1f;

        [Header("Character")]
        [Tooltip("Which roster animal this player is — stats (height/speed/power/control/jump) " +
                 "and appearance. See CharacterRoster in CharacterDef.cs.")]
        public string characterId = CharacterRoster.DefaultId;

        [Tooltip("This player slot's jersey colour (set by the scene builders). Used to pick " +
                 "the right baked sprite set when the character is swapped at runtime.")]
        public Color jerseyColor = Color.white;

        /// <summary>This player's character (stats + look). Unknown ids fall back to the default.</summary>
        public CharacterDef Character => CharacterRoster.Get(characterId);

        /// <summary>This player's power-up meter and any effects currently on them.</summary>
        public PowerUpState Power { get; } = new PowerUpState();

        /// <summary>True when a human drives this slot (local or, later, remote).</summary>
        public virtual bool IsHuman => false;

        /// <summary>True when THIS machine produces the commands for this player. Always true
        /// offline; the network layer flips it off for remote humans' proxies.</summary>
        public bool IsLocallyControlled { get; set; } = true;

        /// <summary>Master gate on the self-driven tick — the network layer turns it off on
        /// machines that only render this player (proxies, client-side AI).</summary>
        public bool SimulationEnabled { get; set; } = true;

        // Baseline sizes and speeds are global — edit them in the GameConfig asset
        // (Volleyball → Create Game Config). The character's stats scale them per-player,
        // and any live power-up effect multiplies on top (1 when nothing is active).
        static GameConfig Cfg => GameConfig.Instance;
        public float moveSpeed => Cfg.moveSpeed * Character.speed * Power.MoveMult;
        // sqrt: apex height scales with jumpSpeed², so this makes apex height scale
        // linearly with the jump stat (a 1.35 jumper peaks 35% higher, not 82%)
        public float jumpSpeed => Cfg.jumpSpeed * Mathf.Sqrt(Character.jump) * Power.JumpMult;
        public float reach => Cfg.reach * Power.ReachMult;
        public float hitReachHeight => Cfg.hitReachHeight * Character.height * Power.ReachHeightMult;
        public float hitBufferTime => Cfg.hitBufferTime;
        public float diveSpeed => Cfg.diveSpeed * Character.speed * Power.MoveMult;
        public float bodyRadius => Cfg.bodyRadius;
        public float bodyHeight => Cfg.bodyHeight * Character.height;
        public float blockNetDistance => Cfg.blockNetDistance;
        public float blockMinHeight => Cfg.blockMinHeight;
        public float blockReach => Cfg.blockReach * Character.height * Power.BlockReachMult;
        public float blockBallBand => Cfg.blockBallBand;

        protected PlayerSimState _sim;

        // The rendered view lags the simulation by up to one tick: Update lerps the transform
        // between the last two simulated positions so 50Hz stepping never shows as judder.
        Vector3 _prevViewPos;
        Vector3 _currViewPos;

        Vector3 _lastGroundPos; // rendered ground position last frame, for sand-mark spacing
        float _strideAccum;     // distance run since the last footprint
        float _footSide = 1f;   // which foot lands next: +1 / -1 alternating
        float _diveMarkAccum;   // distance slid since the last dive streak

        protected MatchManager match;
        protected BallController ball;

        /// <summary>Standing on something — the sand, or a prop we've landed on top of. The
        /// vertical-speed half matters now that ground is not always y=0: rising past the lip
        /// of a bleacher tread puts a standable surface above our feet, and that must not read
        /// as "grounded" and hand back a second jump.</summary>
        public bool IsGrounded => _sim.vertVel <= 0f && _sim.position.y <= _sim.groundY + 0.001f;

        /// <summary>Height of the surface underfoot: 0 on the sand, higher on a prop.</summary>
        public float GroundHeight => _sim.groundY;

        /// <summary>Current vertical speed of the jump integration (+up, −falling).
        /// Zero exactly at the peak of the jump — the jump serve reads it to score timing.</summary>
        public float VerticalVelocity => _sim.vertVel;
        /// <summary>True while laid out on a dive — the slide and the get-up afterwards.</summary>
        public bool IsDiving => _sim.diveTimer > 0f || _sim.diveRecover > 0f;
        /// <summary>World direction of the current/last dive (unit XZ). Read by the visuals
        /// to pick a sideways vs toward/away-from-camera dive pose.</summary>
        public Vector3 DiveDir => _sim.diveDir;

        /// <summary>
        /// How horizontal the diver's body is, 0 (upright) → 1 (flat on the ground). Ramps up
        /// over the slide, holds flat while down, and eases back to 0 as they stand up at the
        /// end of the recovery. Drives the visuals (sprite roll); gameplay ignores it.
        /// </summary>
        public float DiveFlat01
        {
            get
            {
                const float standUpTime = 0.25f; // the last part of the recovery = getting up
                if (_sim.diveTimer > 0f) return 1f - _sim.diveTimer / Mathf.Max(Cfg.diveDuration, 0.01f);
                if (_sim.diveRecover > 0f) return Mathf.Clamp01(_sim.diveRecover / standUpTime);
                return 0f;
            }
        }

        /// <summary>Simulated world position (y = jump height). The authoritative one — the
        /// transform only renders it.</summary>
        public Vector3 SimPosition => _sim.position;
        public Vector3 GroundPosition => new Vector3(_sim.position.x, 0f, _sim.position.z);

        /// <summary>The RENDERED ground position — the interpolated transform, which moves
        /// smoothly every frame. Anything visual that differentiates position over frames
        /// (run cycles, movement audio) must read this, not the 50Hz-stepped sim position,
        /// or the per-frame delta strobes between zero and a double step.</summary>
        public Vector3 ViewGroundPosition => new Vector3(transform.position.x, 0f, transform.position.z);

        /// <summary>Snapshot/restore the full simulated state (prediction + reconciliation).</summary>
        public PlayerSimState CaptureSimState() => _sim;
        public void ApplySimState(in PlayerSimState state)
        {
            _sim = state;
            _prevViewPos = _currViewPos = _sim.position;
        }

        /// <summary>Raised when the player swings — a hit attempt (even a whiff), a landed
        /// contact, a block, or a serve — so visuals can react (e.g. a swing pose).</summary>
        public event System.Action<HitType> Swung;

        /// <summary>Externally trigger a swing pose (used for the serve, which bypasses the hit path).</summary>
        public void TriggerSwing(HitType type) => Swung?.Invoke(type);

        protected virtual void Start()
        {
            match = FindAnyObjectByType<MatchManager>();
            ball = FindAnyObjectByType<BallController>();

            // adopt the scene-baked placement as the initial simulated position
            _sim.position = new Vector3(transform.position.x, 0f, transform.position.z);
            SettleOnGround();
            _prevViewPos = _currViewPos = _sim.position;
            _lastGroundPos = GroundPosition;
            Power.Bind(this, match);

            // the charged/active glow lives on the sprite child; added at runtime so the
            // baked arena scenes need no rebuild for it
            var anim = GetComponentInChildren<CharacterAnimator>();
            if (anim != null && anim.GetComponent<PowerUpGlow>() == null)
                anim.gameObject.AddComponent<PowerUpGlow>();
        }

        /// <summary>Produce this player's intent for the given tick. The human samples its
        /// input source; the AI runs its decision pass. This is the ONLY input channel into
        /// the simulation.</summary>
        public abstract InputCommand GetCommand(int tick);

        /// <summary>
        /// Fire this player's power-up if the meter is full and the match is live. Handles
        /// the fanfare (banner, sound, log); the effect itself is applied by
        /// <see cref="PowerUpState.Activate"/>. Returns true if it fired.
        /// </summary>
        public bool TryActivatePower()
        {
            if (match != null && match.State != MatchState.Serving
                              && match.State != MatchState.Rallying) return false;
            if (!Power.Activate()) return false;

            PowerUpDef def = Power.Def;
            match?.ShowPowerBanner($"{Character.displayName}: {def.bannerText}");
            GameAudio.PlayPowerUp(transform.position);
            VBLog.Event($"POWERUP {def.type} by '{name}' team={team} duration={def.duration:F1}");
            return true;
        }

        static float ApexFor(HitType type, float startY)
        {
            switch (type)
            {
                // spike comes down hard; raise the apex just enough to clear the net when
                // hit from a low contact point, but stay flat/fast when hit from up high
                case HitType.Spike: return Mathf.Max(1.0f, CourtGeometry.NetHeight + 0.7f - startY);
                case HitType.Set: return 3.4f;  // high, soft, hangs for the spiker
                case HitType.Bump: return 2.8f; // lofted return
                case HitType.Dive: return GameConfig.Instance.divePopApex; // desperate pop straight up
                default: return 4f;             // serve
            }
        }

        /// <summary>Drop the body onto whatever is under it right now and clear the jump. Ground
        /// is no longer assumed to be y=0, so every place that used to zero the height has to
        /// ask the world instead.</summary>
        void SettleOnGround()
        {
            float y = WorldCollision.GroundHeightAt(_sim.position, bodyRadius);
            _sim.position.y = y;
            _sim.groundY = y;
            _sim.vertVel = 0f;
        }

        public void ResetState()
        {
            SettleOnGround();
            _sim.hitCooldown = 0f;
            _sim.bufferTime = 0f;
            _sim.blockArm = 0f;
            _sim.diveTimer = 0f;
            _sim.diveRecover = 0f;
            _prevViewPos = _currViewPos = _sim.position;
        }

        /// <summary>Hold the rendered view still at the latest simulated position. Called on
        /// ticks where the networked clock deliberately skips a sim step (client converging on
        /// its lead) — without this, Update would re-lerp across the previous step's interval
        /// and the view visibly steps backward.</summary>
        public void FlattenViewInterpolation() => _prevViewPos = _currViewPos;

        /// <summary>Place the player instantly (rally resets, serve spots). Snaps the view —
        /// never interpolate across a teleport.</summary>
        public void TeleportTo(Vector3 groundPos)
        {
            _sim.position = new Vector3(groundPos.x, 0f, groundPos.z);
            SettleOnGround(); // court spots are on the sand, but never assume it
            _prevViewPos = _currViewPos = _sim.position;
            transform.position = _sim.position;
            _lastGroundPos = GroundPosition;
        }

        protected virtual void FixedUpdate()
        {
            // Online, NetworkPlayer drives the sim (authority / prediction / proxy) — this
            // self-drive is the OFFLINE path only, and must stay silent the moment a session
            // starts, even before the adapters finish configuring.
            if (!SimulationEnabled || NetworkSession.IsOnline) return;
            int tick = Mathf.RoundToInt(Time.fixedTime / Time.fixedDeltaTime);
            InputCommand cmd = GetCommand(tick);
            Simulate(in cmd, Time.fixedDeltaTime);
        }

        /// <summary>
        /// Advance one tick of the simulation from one command. Deterministic given
        /// (state, command, dt) — apart from the authority-side contact rolls in the
        /// Execute* methods, nothing here may read wall-clock time, per-frame input,
        /// randomness, or the camera. The <paramref name="role"/> scopes what runs:
        /// movement/jump/dive state always; local feedback (swing edges) only on live
        /// steps (never in replay); contacts, power-ups and serve routing only as
        /// Authority — on a predicting client those travel to the server inside the
        /// command stream and come back as replicated results.
        /// </summary>
        public void Simulate(in InputCommand cmd, float dt, SimRole role = SimRole.Authority)
        {
            bool authority = role == SimRole.Authority;
            bool live = role != SimRole.Replay; // a real-time step, not a reconciliation re-run

            _sim.hitCooldown -= dt;
            if (live) Power.Tick(dt); // real-time effects must never re-tick during replay
            if (authority && cmd.power) TryActivatePower();

            // Callouts ride the command stream like power-ups: authority-only, so a predicting
            // client never double-says anything and a replay never re-says it.
            if (authority && cmd.chat != ChatCall.None) ChatDirector.Say(this, cmd.chat);

            if (_sim.diveTimer > 0f)
            {
                _sim.diveTimer -= dt;
                if (_sim.diveTimer <= 0f) _sim.diveRecover = Cfg.diveRecoverTime; // slide over — get up
            }
            else if (_sim.diveRecover > 0f) _sim.diveRecover -= dt;

            // --- horizontal movement (clamped to the court, blocked by the net itself) ---
            Vector2 mv = cmd.moveWorld;
            if (mv.sqrMagnitude > 0.01f) _sim.lastMoveDir = mv;
            Vector3 pos = _sim.position;
            if (_sim.diveTimer > 0f)
            {
                // mid-dive: committed to the lunge — the dive direction overrides steering
                pos.x += _sim.diveDir.x * diveSpeed * dt;
                pos.z += _sim.diveDir.z * diveSpeed * dt;
            }
            else if (_sim.diveRecover <= 0f)
            {
                pos.x += mv.x * moveSpeed * dt;
                pos.z += mv.y * moveSpeed * dt;
            }
            // (while recovering: face down in the sand — no movement)

            // One roam box for everyone: both halves and the deep zones behind both baselines
            // are walkable ALL match, by either team. Nobody is fenced into their own court —
            // the world's colliders decide where you can actually go. Reach still stops at the
            // tape (BallInReach/TryDiveHit test the BALL's side, not the player's), so crossing
            // over gains no touches; the serve rule below is the only other rule-based gate.
            bool servePhase = match != null && match.IsServePhaseFor(this);
            pos.x = Mathf.Clamp(pos.x, -CourtGeometry.RoamHalfWidth, CourtGeometry.RoamHalfWidth);
            pos.z = Mathf.Clamp(pos.z, -CourtGeometry.RoamHalfDepth, CourtGeometry.RoamHalfDepth);

            // Solid world: sweep the body to where it asked to go, sliding along whatever it
            // meets. The step lift is grounded-only — a runner should ride over a bleacher
            // tread, but someone mid-jump has to actually clear the ledge they're flying at.
            pos = WorldCollision.SlideHorizontal(_sim.position, pos, bodyRadius, bodyHeight,
                                                 IsGrounded ? Cfg.stepHeight : 0f);

            // The net's collider is 0.1 thick — thinner than one tick of a dive — so a fast
            // enough lunge could sweep clean past it between queries. This test is analytic and
            // cannot be tunnelled, so it stays as the net's guarantee.
            pos = CourtGeometry.BlockNetCrossing(_sim.position, pos);

            // The server must stay behind their own back line while holding the ball — but
            // once the jump-serve toss is up they may run in after it, like a real run-up.
            if (servePhase && !(match != null && match.ServeTossed))
            {
                if (team == TeamSide.A) pos.z = Mathf.Min(pos.z, -CourtGeometry.HalfDepth);
                else pos.z = Mathf.Max(pos.z, CourtGeometry.HalfDepth);
            }

            // --- diving: a grounded lunge toward the steer direction (or the ball) ---
            if (cmd.dive && IsGrounded && !IsDiving && !servePhase)
                StartDive(mv, live);

            // --- jump + gravity (you can't jump out of a dive) ---
            if (cmd.jump && IsGrounded && !IsDiving)
                _sim.vertVel = jumpSpeed;
            _sim.vertVel += Physics.gravity.y * dt;
            pos.y = _sim.position.y + _sim.vertVel * dt;

            // Land on whatever is under the NEW footprint — sand, a bleacher tread, a crate —
            // rather than an assumed floor at zero. Only settle while falling: on the way up,
            // a ledge we're passing over must not swallow the jump. The same snap carries a
            // walker up onto a step, since SlideHorizontal already let them over its lip.
            float support = WorldCollision.GroundHeightAt(pos, bodyRadius);
            if (_sim.vertVel <= 0f && pos.y <= support) { pos.y = support; _sim.vertVel = 0f; }
            _sim.groundY = support;

            _prevViewPos = _currViewPos;
            _sim.position = pos;
            _currViewPos = pos;

            // Serve actions route to the match AFTER integration, so a jump-serve strike is
            // judged on this tick's vertical speed — exactly what the player's view shows.
            if (authority && cmd.serve != ServeIntent.None && match != null)
                match.OnServeIntent(this, cmd.serve);

            if (IsDiving)
            {
                // laid out: the only possible contact is the chaotic dive dig, and only
                // while still sliding — once recovering, we're face down and out of the play
                if (authority && _sim.diveTimer > 0f && _sim.hitCooldown <= 0f) TryDiveHit();
                _sim.bufferTime = 0f;
            }
            else
            {
                bool wantsHit = cmd.hitPressed;
                // The press EDGE, not the press: the AI holds its intent across ticks, and a
                // block has to answer "did you hit the key at this moment", asked once.
                bool pressEdge = wantsHit && !_sim.swingWantedPrev;
                _sim.swingWantedPrev = wantsHit;

                // --- blocking: the press puts the hands up and starts the clock. The block
                //     lands on the first tick the window opens while the arm is running, and
                //     what's left of the arm by then is the timing score. Anything else and the
                //     press would have to hit the ~4 ticks the ball spends crossing the net.
                // Up at the net the hit key means BLOCK. Spike and Set still do their own thing
                // from up there — a jump spike is struck from inside blockNetDistance — so the
                // contact you asked for is what tells them apart: Bump (J) is the block.
                bool atNet = !IsGrounded && Mathf.Abs(_sim.position.z) <= blockNetDistance;
                bool spikeOverride = pressEdge && cmd.hitType != HitType.Bump;
                if (spikeOverride) _sim.blockArm = 0f; // changed your mind — abandon the block

                // A press at the net arms one, and so does a press still BUFFERED from a hair
                // before takeoff: pressing on the way up is going for a block too, and that one
                // used to come out as a bump in mid-air.
                bool armBlock = atNet && !spikeOverride && _sim.blockArm <= 0f
                                && ((pressEdge && cmd.hitType == HitType.Bump)
                                    || (_sim.bufferTime > 0f && _sim.bufferedHit == HitType.Bump));
                if (armBlock)
                {
                    _sim.blockArm = Cfg.blockPressWindow;
                    _sim.bufferTime = 0f;   // the press belongs to the block now
                }
                else if (!atNet) _sim.blockArm = 0f; // landed or drifted off — attempt's over
                // Mashing can't re-arm: you commit to your first press, or one press would
                // always land on the ball and the timing would score itself.
                else _sim.blockArm = Mathf.Max(0f, _sim.blockArm - dt);

                // Swing on the press edge — the character visibly commits the instant you press,
                // even if nothing is in reach (a whiff), not only when contact lands.
                if (live)
                {
                    if (armBlock) TriggerSwing(HitType.Block);
                    else if (pressEdge) TriggerSwing(cmd.hitType);
                }

                // An armed block COMMITS: no ordinary hit may fire under it. Without this the
                // same press both armed the block and buffered a bump, and since `reach` (2.6)
                // is far bigger than `blockReach` (1.6) the bump usually won — which is exactly
                // what a bump animation coming out of a block attempt was telling you.
                bool committed = _sim.blockArm > 0f;

                if (authority && committed && _sim.hitCooldown <= 0f && TryBlock())
                {
                    _sim.blockArm = 0f;
                    _sim.bufferTime = 0f;
                }
                else
                {
                    // --- hitting (buffered: an early or slightly-off press is remembered
                    //     briefly and fires the moment the ball comes into reach) ---
                    if (wantsHit && !committed)
                    {
                        _sim.bufferedHit = cmd.hitType;
                        _sim.bufferTime = hitBufferTime;
                    }
                    else
                    {
                        _sim.bufferTime -= dt;
                    }
                    if (authority && !committed && _sim.hitCooldown <= 0f && _sim.bufferTime > 0f
                        && RequestHit(_sim.bufferedHit, in cmd))
                        _sim.bufferTime = 0f;
                }
            }

            // match over: any hit press from a human is the "continue" input
            if (authority && cmd.hitPressed && IsHuman
                && match != null && match.State == MatchState.MatchOver)
                match.OnContinuePressed();
        }

        /// <summary>Render the simulation: interpolate the transform between the last two
        /// ticks and drop the cosmetic sand marks along the rendered path.</summary>
        protected virtual void Update()
        {
            float alpha = Time.fixedDeltaTime > 0f
                ? Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime) : 1f;
            transform.position = Vector3.Lerp(_prevViewPos, _currViewPos, alpha);
            LeaveSandMarks(transform.position);
        }

        /// <summary>Drop footprints while running and drag streaks while dive-sliding,
        /// spaced by ground distance actually covered this frame.</summary>
        void LeaveSandMarks(Vector3 pos)
        {
            Vector3 flat = new Vector3(pos.x, 0f, pos.z);
            Vector3 delta = flat - _lastGroundPos;
            _lastGroundPos = flat;

            float moved = delta.magnitude;
            if (moved < 1e-5f || moved > 1f) return; // idle, or teleported by a rally reset
            Vector3 dir = delta / moved;

            // Marks are painted flat on the court, so only the sand takes them — someone up on
            // the bleachers must not stamp footprints into the sand underneath them.
            bool onSand = _sim.groundY <= 0.05f;

            if (_sim.diveTimer > 0f && onSand)
            {
                _diveMarkAccum += moved;
                if (_diveMarkAccum >= 0.3f)
                {
                    _diveMarkAccum = 0f;
                    SandMarks.DiveStreak(flat, dir);
                }
            }
            else if (IsGrounded && onSand)
            {
                _strideAccum += moved;
                if (_strideAccum >= 0.75f)
                {
                    _strideAccum = 0f;
                    _footSide = -_footSide;
                    SandMarks.Footstep(flat, dir, _footSide);
                }
            }
            else
            {
                _strideAccum = 0f; // airborne: the stride restarts on landing
            }
        }

        /// <summary>
        /// True when a block is there to be TAKEN this instant: airborne at the net with an
        /// opponent's attack inside the hands' window. This is only the window being open —
        /// the block itself needs a press, which is the whole skill (see <see cref="TryBlock"/>).
        /// Pure state, no input, so the AI and the swing pose can ask the same question.
        /// </summary>
        public bool BlockWindowOpen()
        {
            if (ball == null || IsGrounded || !ball.CanBeHit) return false;
            if (match != null && !match.CanTeamTouch(team)) return false;
            if (match != null && match.ServeInFlight) return false; // a serve may not be blocked
            if (ball.LastTouchTeam != team.Other()) return false; // only block incoming attacks

            Vector3 bp = ball.transform.position;
            if (Mathf.Abs(_sim.position.z) > blockNetDistance) return false; // I'm at the net
            if (Mathf.Abs(bp.z) > blockBallBand) return false;               // ball is right at the net
            if (bp.y < blockMinHeight || bp.y > _sim.position.y + hitReachHeight) return false;

            // Must be close to the ball. The near-net band above keeps blocks at the net, so we
            // never reach deep into the opponents' court to pick off their passes.
            return Vector2.Distance(new Vector2(_sim.position.x, _sim.position.z),
                                    new Vector2(bp.x, bp.z)) <= blockReach;
        }

        /// <summary>
        /// How well-timed the block press was, from how long the hands waited before the attack
        /// actually arrived. Anything up to <see cref="GameConfig.blockPerfectLead"/> early is
        /// perfect, falling to 0 at the far end of the press window.
        ///
        /// Note the grace band — it is not slack, it is the point. "Hit it as the ball reaches
        /// your hands" is a cue to press BEFORE contact, and people do so by a fairly consistent
        /// 0.1–0.25s. Scoring a simultaneous press as the ideal marks every honest attempt late.
        /// Measured on press-to-contact rather than on how near the ball is, too: the net holds
        /// a blocker ~0.4m off the tape while the ball may still be 0.9m the other side, so a
        /// textbook block never gets "close" and distance scored good ones as mistimed.
        /// </summary>
        public float BlockTiming()
        {
            float window = Mathf.Max(Cfg.blockPressWindow, 0.01f);
            float lead = window - _sim.blockArm; // how long ago the press was
            float perfect = Mathf.Clamp(Cfg.blockPerfectLead, 0f, window - 0.01f);
            float t = lead <= perfect ? 1f
                                      : 1f - Mathf.Clamp01((lead - perfect) / (window - perfect));

            // The AI presses the instant the window opens, with none of a human's hesitation,
            // so it would stuff every single attack. Its skill is a stated handicap instead.
            return IsHuman ? t : t * Mathf.Clamp01(Cfg.aiBlockTiming);
        }

        /// <summary>
        /// A block attempt while the press is still armed. Blocking is never automatic — no
        /// press, no block — but the press does NOT have to land on the exact tick the ball
        /// arrives: the ball crosses the net band in under a tenth of a second, so demanding
        /// that would be a coin flip rather than a skill. The press puts the hands up, and how
        /// long they wait there is what separates a stuff from a deflection.
        /// </summary>
        protected bool TryBlock()
        {
            if (!BlockWindowOpen()) return false;
            return ExecuteBlockAuthoritative(BlockTiming());
        }

        /// <summary>The authority-side half of a block: contact-error roll and ball launch,
        /// shaped by how well the press was timed (see <see cref="BlockTiming"/>).
        /// (Offline the request IS the execution; the network layer reroutes it.)</summary>
        public bool ExecuteBlockAuthoritative(float timing)
        {
            timing = Mathf.Clamp01(timing);
            Vector3 bp = ball.transform.position;
            TeamSide opp = team.Other();
            float fwd = CourtGeometry.SideSign(opp); // +Z for team A: toward the net / opponents

            // Meet the ball just over the net (hands over the tape): nudge the contact forward a
            // little so the downward stuff originates on the attackers' side and clears the net.
            Vector3 contact = bp;
            float forwardShift = Mathf.Clamp((fwd * 0.2f - bp.z) * fwd, 0f, 1.0f);
            contact.z = bp.z + forwardShift * fwd;
            ball.transform.position = contact;

            // A well-timed block STUFFS: flat and straight down at the attackers' feet. A
            // mistimed one is a deflection off the hands — it pops up and drifts deep, handing
            // them a free ball instead of ending the rally. The error roll widens the same way,
            // so a bad enough touch sprays back onto our own side, or out.
            float ox = Mathf.Clamp(contact.x + Random.Range(-0.8f, 0.8f),
                                   -CourtGeometry.HalfWidth + 0.3f, CourtGeometry.HalfWidth - 0.3f);
            float oz = fwd * CourtGeometry.HalfDepth * Mathf.Lerp(0.75f, 0.22f, timing);
            float blockError = ComputeContactError(HitType.Block, ball.Body.linearVelocity.magnitude)
                               + (1f - timing) * Cfg.blockMistimePenalty;
            Vector3 blockTarget = ApplyContactError(new Vector3(ox, 0.2f, oz), blockError);

            // Time it well and it's a STUFF — driven straight down at them, no arc to read.
            // Below that you only got a piece of it, and the ball loops up off your hands.
            bool stuff = timing >= Cfg.blockStuffTiming;
            ball.LaunchTo(blockTarget, Mathf.Lerp(2.4f, 0.5f, timing), team, this, HitType.Block,
                          0f, stuff);
            match?.RegisterTouch(team, this);
            // Timing rides in the log line: "am I mistiming it or is it broken" is otherwise
            // unanswerable from the outside — the outcome alone can't tell you which.
            LogContact(HitType.Block, blockTarget,
                       $" timing={timing:F2} {(stuff ? "STUFF" : "deflection")}");
            _sim.hitCooldown = 0.25f;
            return true;
        }

        /// <summary>Commit to a dive: a fast grounded lunge along the held steer direction,
        /// or the way we were last running when nothing is held right now. The dive STATE is
        /// part of the simulation (all roles); the pose event and log fire only on live steps.</summary>
        void StartDive(Vector2 steer, bool live)
        {
            if (steer.sqrMagnitude < 0.01f) steer = _sim.lastMoveDir;
            Vector3 dir = new Vector3(steer.x, 0f, steer.y);
            if (dir.sqrMagnitude < 0.01f) dir = new Vector3(0f, 0f, CourtGeometry.SideSign(team.Other()));
            _sim.diveDir = dir.normalized;
            _sim.diveTimer = Cfg.diveDuration;
            if (!live) return;
            TriggerSwing(HitType.Dive);
            VBLog.Event($"DIVE by '{name}' team={team} dir={VBLog.V(_sim.diveDir)}");
        }

        /// <summary>
        /// The dive contact: a desperate one-armed dig on a low ball while laid out. There is
        /// no aim at all — the ball squirts off the platform in a completely random direction
        /// (sometimes straight up, sometimes shanked metres sideways) at a random height.
        /// Returns true if contact was made.
        /// </summary>
        protected bool TryDiveHit()
        {
            if (ball == null || !ball.CanBeHit) return false;
            if (match != null && !match.CanTeamTouch(team)) return false;

            Vector3 bp = ball.transform.position;
            if (CourtGeometry.SideOf(bp) != team) return false;      // never reach across the net
            if (bp.y < 0f || bp.y > Cfg.diveMaxBallHeight) return false; // a dive only digs low balls
            if (Vector2.Distance(new Vector2(_sim.position.x, _sim.position.z),
                                 new Vector2(bp.x, bp.z)) > Cfg.diveReach) return false;

            return ExecuteDiveHitAuthoritative();
        }

        /// <summary>Authority-side half of the dive dig — the random spray roll and launch.</summary>
        public bool ExecuteDiveHitAuthoritative()
        {
            Vector3 bp = ball.transform.position;
            float incomingSpeed = ball.Body.linearVelocity.magnitude;
            float error = ComputeContactError(HitType.Dive, incomingSpeed);

            // Spray around the contact point itself — no drift toward anywhere "safe", so the
            // dig is as likely to squirt sideways or backwards as up-court. The pop height is
            // rolled too: anything from a flat shank to a sky ball.
            Vector3 target = ApplyContactError(new Vector3(bp.x, 0.6f, bp.z), error);
            float apex = Cfg.divePopApex * Random.Range(0.6f, 1.5f);
            ball.LaunchTo(target, apex, team, this, HitType.Dive);
            match?.RegisterTouch(team, this);
            LogContact(HitType.Dive, target);
            _sim.hitCooldown = 0.25f;
            return true;
        }

        /// <summary>Per-controller skill multiplier on contact error (1 = baseline human).</summary>
        protected virtual float ContactSkill => 1f;

        /// <summary>
        /// The serve's contact error for this player. Serves are launched by MatchManager
        /// (outside the hit path), so this exposes the full error model — the character's
        /// control stat and the controller's skill both shape serve placement.
        /// </summary>
        public float ServeError() => ComputeContactError(HitType.Serve, 0f);

        /// <summary>
        /// How much a contact strays (metres of spray), built from the situation: the contact
        /// type, how hard the incoming ball was, and — for spikes — whether it was set to us and
        /// how well-timed the jump was, plus how far we had to reach. Bigger = less controlled.
        /// </summary>
        float ComputeContactError(HitType type, float incomingSpeed)
        {
            var cfg = GameConfig.Instance;

            float baseErr;
            float speedPenaltyPerUnit;
            switch (type)
            {
                case HitType.Set:   baseErr = cfg.setBaseError;   speedPenaltyPerUnit = cfg.setSpeedPenalty;   break;
                case HitType.Spike: baseErr = cfg.spikeBaseError; speedPenaltyPerUnit = cfg.spikeSpeedPenalty; break;
                case HitType.Serve: baseErr = cfg.serveBaseError; speedPenaltyPerUnit = 0f;                    break;
                case HitType.Block: baseErr = cfg.blockBaseError; speedPenaltyPerUnit = 0f;                    break;
                case HitType.Dive:  baseErr = cfg.diveBaseError;  speedPenaltyPerUnit = cfg.bumpSpeedPenalty;  break;
                default:            baseErr = cfg.bumpBaseError;  speedPenaltyPerUnit = cfg.bumpSpeedPenalty;  break;
            }

            float error = baseErr;

            // Hard-driven balls are harder to handle — and far harder to set than to pass.
            float speedOver = Mathf.Max(0f, incomingSpeed - cfg.softBallSpeed);
            error += speedOver * speedPenaltyPerUnit;

            // Spike-specific: punish hitting a ball that wasn't set to you and mistimed jumps.
            if (type == HitType.Spike)
            {
                bool setToMe = ball != null && ball.LastHitType == HitType.Set && ball.LastTouchTeam == team;
                if (!setToMe) error += cfg.spikeNoSetPenalty;
                if (IsGrounded) error += cfg.groundedSpikePenalty;
                // |vertVel| is 0 at the apex of the jump and grows the further off the peak we are.
                error += Mathf.Abs(_sim.vertVel) * cfg.jumpTimingPenalty;
            }

            // Reaching at the edge of our range is a worse contact than one right at our feet.
            if (ball != null)
            {
                float dist = Vector2.Distance(new Vector2(_sim.position.x, _sim.position.z),
                                              new Vector2(ball.transform.position.x, ball.transform.position.z));
                error += cfg.reachErrorPenalty * Mathf.Clamp01(dist / Mathf.Max(reach, 0.01f));
            }

            // The character's stats shape the final spray: height tightens net work
            // (spike/block), control tightens everything else (bump/set/serve/dive).
            // Live power-ups multiply on top: own accuracy buffs and inflicted debuffs.
            return Mathf.Min(error * ContactSkill * Character.ErrorMult(type) * Power.ErrorMult,
                             cfg.maxContactError);
        }

        /// <summary>
        /// Spray the aim point by the computed error. Deliberately NOT clamped to the court — a
        /// bad enough contact lands out or in the net, which is how rallies end on a mistake.
        /// </summary>
        public static Vector3 ApplyContactError(Vector3 target, float error)
        {
            if (error <= 0f) return target;
            Vector2 off = Random.insideUnitCircle * error;
            return new Vector3(target.x + off.x, target.y, target.z + off.y);
        }

        /// <summary>One consolidated log line per contact: the hit data and the touch count.</summary>
        void LogContact(HitType type, Vector3 target, string extra = "")
        {
            if (ball == null) return;
            Vector3 v = ball.Body.linearVelocity;
            int touch = match != null ? match.Touches : 0;
            VBLog.Event($"{type} by '{name}' team={team} touch#{touch} from={VBLog.V(ball.transform.position)} " +
                        $"target={VBLog.V(target)} vel={VBLog.V(v)} speed={v.magnitude:F1} spin={ball.Spin:F0}{extra}");
            GameAudio.PlayHit(type, ball.transform.position);
            Swung?.Invoke(type);
        }

        // ---- debug hitbox visualizer (enable "Gizmos" in the Game view to see it) ----
        void OnDrawGizmos()
        {
            Vector3 c = new Vector3(transform.position.x, transform.position.y + 1.2f, transform.position.z);

            // general hit reach
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            DrawCircleXZ(c, reach);

            // block reach
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            DrawCircleXZ(c, blockReach);

            // the near-net band a block can engage in
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            Gizmos.DrawLine(new Vector3(-CourtGeometry.HalfWidth, 0.05f, blockBallBand),
                            new Vector3(CourtGeometry.HalfWidth, 0.05f, blockBallBand));
            Gizmos.DrawLine(new Vector3(-CourtGeometry.HalfWidth, 0.05f, -blockBallBand),
                            new Vector3(CourtGeometry.HalfWidth, 0.05f, -blockBallBand));
        }

        static void DrawCircleXZ(Vector3 center, float radius)
        {
            const int seg = 28;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        protected bool BallInReach()
        {
            if (ball == null) return false;
            Vector3 bp = ball.transform.position;

            // never reach across the net
            if (CourtGeometry.SideOf(bp) != team) return false;

            float headY = _sim.position.y + hitReachHeight;
            if (bp.y > headY || bp.y < 0f) return false;

            Vector2 a = new Vector2(_sim.position.x, _sim.position.z);
            Vector2 b = new Vector2(bp.x, bp.z);
            return Vector2.Distance(a, b) <= reach;
        }

        /// <summary>
        /// A hit attempt from this tick's command. Offline (and on the authority) the request
        /// executes immediately; the network layer reroutes a remote player's request to the
        /// server. Aim resolves from the command AT CONTACT: a steering human keeps adjusting
        /// through the hit buffer, the AI's planned point rides in the command itself.
        /// </summary>
        protected virtual bool RequestHit(HitType type, in InputCommand cmd)
        {
            if (ball == null || !ball.CanBeHit || !BallInReach()) return false;
            if (match != null && !match.CanTeamTouch(team)) return false;
            return ExecuteHitAuthoritative(type, cmd.aimMode, cmd.hitAim, cmd.moveWorld);
        }

        /// <summary>Authority-side half of a hit: re-check the gates, roll the contact error,
        /// launch the ball, and report the touch.</summary>
        public bool ExecuteHitAuthoritative(HitType type, AimMode aimMode, Vector3 aim, Vector2 steerWorld)
        {
            if (ball == null || !ball.CanBeHit || !BallInReach()) return false;
            if (match != null && !match.CanTeamTouch(team)) return false;

            // measure the incoming pace BEFORE we relaunch — a hard-driven ball is harder to handle
            float incomingSpeed = ball.Body.linearVelocity.magnitude;
            float error = ComputeContactError(type, incomingSpeed);
            Vector3 intent = aimMode == AimMode.Explicit ? aim : SteerAim(type, steerWorld);
            Vector3 target = ApplyContactError(intent, error);
            ball.LaunchTo(target, ApexFor(type, ball.transform.position.y), team, this, type);
            match?.RegisterTouch(team, this);
            LogContact(type, target);
            _sim.hitCooldown = 0.25f;
            return true;
        }

        /// <summary>
        /// Where a steering (human) hit goes: the held world-space direction shapes a bump,
        /// set, or spike target. Pure function of simulated state — runs identically on the
        /// aiming client and on the server replaying its commands.
        /// </summary>
        protected Vector3 SteerAim(HitType type, Vector2 steerWorld)
        {
            Vector3 steer = new Vector3(steerWorld.x, 0f, steerWorld.y);

            if (type == HitType.Set)
            {
                // keep it on our own side, up near the net, to set up a spike
                float sx = Mathf.Clamp(_sim.position.x + steer.x * 3f,
                                       -CourtGeometry.HalfWidth + 0.3f, CourtGeometry.HalfWidth - 0.3f);
                float sz = CourtGeometry.SideSign(team) * CourtGeometry.HalfDepth * 0.2f;
                return new Vector3(sx, 0.6f, sz);
            }

            // A bump only goes over the net if you aim toward the opponents' side; otherwise
            // it's a controlled pass that stays on your own court (up toward the net).
            if (type == HitType.Bump)
            {
                float towardOpponent = steer.z * CourtGeometry.SideSign(team.Other());
                if (towardOpponent <= 0.3f)
                {
                    float px = Mathf.Clamp(_sim.position.x + steer.x * 3f,
                                           -CourtGeometry.HalfWidth + 0.3f, CourtGeometry.HalfWidth - 0.3f);
                    float pz = CourtGeometry.SideSign(team) * CourtGeometry.HalfDepth * 0.25f;
                    return new Vector3(px, 0.6f, pz);
                }
            }

            // Spike (or a bump aimed over): send it to the opponents' court
            TeamSide opp = team.Other();
            float osign = CourtGeometry.SideSign(opp);
            float depthFrac = type == HitType.Spike ? 0.7f : 0.6f;

            float x = Mathf.Clamp(steer.x * CourtGeometry.HalfWidth * 0.9f,
                                  -CourtGeometry.HalfWidth + 0.3f, CourtGeometry.HalfWidth - 0.3f);
            float z = osign * Mathf.Clamp(
                CourtGeometry.HalfDepth * depthFrac + steer.z * 3f, 1f, CourtGeometry.HalfDepth);
            return new Vector3(x, 0.6f, z);
        }
    }
}
