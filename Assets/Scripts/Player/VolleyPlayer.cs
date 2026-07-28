using UnityEngine;

namespace Volleyball
{
    /// <summary>The kind of contact a player makes with the ball.</summary>
    public enum HitType { Bump, Set, Spike, Serve, Block, Dive }

    /// <summary>
    /// Shared movement/jump/hit behaviour for both the human player and the AI.
    /// Movement is code-driven (no Rigidbody) on the X/Z plane with a manual vertical
    /// jump integration, so players never physically shove the ball — hits are explicit.
    /// Subclasses supply the input (keyboard vs AI decision) and the aim target.
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
        public float blockNetDistance => Cfg.blockNetDistance;
        public float blockMinHeight => Cfg.blockMinHeight;
        public float blockReach => Cfg.blockReach * Character.height * Power.BlockReachMult;
        public float blockBallBand => Cfg.blockBallBand;

        protected float height;   // current height above the ground (from jumping)
        protected float vertVel;
        protected float hitCooldown;

        HitType _bufferedHit;
        float _bufferUntil;
        bool _swingWantedPrev;

        float _diveTimer;    // > 0 while sliding along the dive
        float _diveRecover;  // > 0 while getting back up afterwards
        Vector3 _diveDir;
        Vector2 _lastMoveDir; // last non-zero steering input, so a stationary dive still has a direction

        Vector3 _lastGroundPos; // ground-plane position last frame, for sand-mark spacing
        float _strideAccum;     // distance run since the last footprint
        float _footSide = 1f;   // which foot lands next: +1 / -1 alternating
        float _diveMarkAccum;   // distance slid since the last dive streak

        protected MatchManager match;
        protected BallController ball;

        public bool IsGrounded => height <= 0.001f;

        /// <summary>Current vertical speed of the manual jump integration (+up, −falling).
        /// Zero exactly at the peak of the jump — the jump serve reads it to score timing.</summary>
        public float VerticalVelocity => vertVel;
        /// <summary>True while laid out on a dive — the slide and the get-up afterwards.</summary>
        public bool IsDiving => _diveTimer > 0f || _diveRecover > 0f;
        /// <summary>World direction of the current/last dive (unit XZ). Read by the visuals
        /// to pick a sideways vs toward/away-from-camera dive pose.</summary>
        public Vector3 DiveDir => _diveDir;

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
                if (_diveTimer > 0f) return 1f - _diveTimer / Mathf.Max(Cfg.diveDuration, 0.01f);
                if (_diveRecover > 0f) return Mathf.Clamp01(_diveRecover / standUpTime);
                return 0f;
            }
        }
        public Vector3 GroundPosition => new Vector3(transform.position.x, 0f, transform.position.z);

        /// <summary>Raised when the player swings — a hit attempt (even a whiff), a landed
        /// contact, a block, or a serve — so visuals can react (e.g. a swing pose).</summary>
        public event System.Action<HitType> Swung;

        /// <summary>Externally trigger a swing pose (used for the serve, which bypasses TryHit).</summary>
        public void TriggerSwing(HitType type) => Swung?.Invoke(type);

        protected virtual void Start()
        {
            match = FindAnyObjectByType<MatchManager>();
            ball = FindAnyObjectByType<BallController>();
            _lastGroundPos = GroundPosition;
            Power.Bind(this, match);

            // the charged/active glow lives on the sprite child; added at runtime so the
            // baked arena scenes need no rebuild for it
            var anim = GetComponentInChildren<CharacterAnimator>();
            if (anim != null && anim.GetComponent<PowerUpGlow>() == null)
                anim.gameObject.AddComponent<PowerUpGlow>();
        }

        protected abstract Vector2 ReadMove();
        protected abstract bool ReadJumpPressed();
        protected abstract bool ReadDivePressed();
        protected abstract Vector3 ChooseHitTarget(HitType type);

        /// <summary>True to fire the power-up this frame (human: the power button; AI: the
        /// cue policy in AIController). Default false so serve flow etc. never trigger it.</summary>
        protected virtual bool ReadPowerPressed() => false;

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

        /// <summary>Return true to hit this frame, with the explicitly chosen contact type.</summary>
        protected abstract bool TryGetDesiredHit(out HitType type);

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

        public void ResetState()
        {
            height = 0f;
            vertVel = 0f;
            hitCooldown = 0f;
            _diveTimer = 0f;
            _diveRecover = 0f;
        }

        protected virtual void Update()
        {
            float dt = Time.deltaTime;
            hitCooldown -= dt;
            Power.Tick(dt);
            if (ReadPowerPressed()) TryActivatePower();

            if (_diveTimer > 0f)
            {
                _diveTimer -= dt;
                if (_diveTimer <= 0f) _diveRecover = Cfg.diveRecoverTime; // slide over — get up
            }
            else if (_diveRecover > 0f) _diveRecover -= dt;

            // --- horizontal movement (clamped to own side of the net) ---
            Vector2 mv = ReadMove();
            if (mv.sqrMagnitude > 0.01f) _lastMoveDir = mv;
            Vector3 pos = transform.position;
            if (_diveTimer > 0f)
            {
                // mid-dive: committed to the lunge — the dive direction overrides steering
                pos.x += _diveDir.x * diveSpeed * dt;
                pos.z += _diveDir.z * diveSpeed * dt;
            }
            else if (_diveRecover <= 0f)
            {
                pos.x += mv.x * moveSpeed * dt;
                pos.z += mv.y * moveSpeed * dt;
            }
            // (while recovering: face down in the sand — no movement)

            // The deep zone behind the baseline is walkable ALL match — same space whether
            // serving or rallying, so ending a serve never snaps anyone forward. (Standing
            // deep is its own punishment: deep balls are already out.)
            const float backMargin = 4f;
            bool servePhase = match != null && match.IsServePhaseFor(this);
            pos.x = Mathf.Clamp(pos.x, -(CourtGeometry.HalfWidth + 1f), CourtGeometry.HalfWidth + 1f);
            if (team == TeamSide.A)
                pos.z = Mathf.Clamp(pos.z, -(CourtGeometry.HalfDepth + backMargin), -CourtGeometry.NetBuffer);
            else
                pos.z = Mathf.Clamp(pos.z, CourtGeometry.NetBuffer, CourtGeometry.HalfDepth + backMargin);

            // The server must stay behind their own back line while holding the ball — but
            // once the jump-serve toss is up they may run in after it, like a real run-up.
            if (servePhase && !(match != null && match.ServeTossed))
            {
                if (team == TeamSide.A) pos.z = Mathf.Min(pos.z, -CourtGeometry.HalfDepth);
                else pos.z = Mathf.Max(pos.z, CourtGeometry.HalfDepth);
            }

            // --- diving: a grounded lunge toward the steer direction (or the ball) ---
            if (ReadDivePressed() && IsGrounded && !IsDiving
                && !(match != null && match.IsServePhaseFor(this)))
                StartDive(mv);

            // --- jump + gravity (you can't jump out of a dive) ---
            if (ReadJumpPressed() && IsGrounded && !IsDiving)
                vertVel = jumpSpeed;
            vertVel += Physics.gravity.y * dt;
            height += vertVel * dt;
            if (height <= 0f) { height = 0f; vertVel = 0f; }
            pos.y = height;

            transform.position = pos;
            LeaveSandMarks(pos);

            if (IsDiving)
            {
                // laid out: the only possible contact is the chaotic dive dig, and only
                // while still sliding — once recovering, we're face down and out of the play
                if (_diveTimer > 0f && hitCooldown <= 0f) TryDiveHit();
                _bufferUntil = 0f;
            }
            // --- blocking: jumping at the net into an opponent's attack auto-blocks it ---
            else if (hitCooldown <= 0f && TryBlock())
            {
                _bufferUntil = 0f;
            }
            else
            {
                // --- hitting (buffered: an early or slightly-off press is remembered briefly
                //     and fires the moment the ball comes into reach) ---
                bool wantsHit = TryGetDesiredHit(out HitType type);

                // Swing on the press edge — the character visibly swings the instant you commit
                // to a hit, even if no ball is in reach (a whiff), not only when contact lands.
                if (wantsHit && !_swingWantedPrev) TriggerSwing(type);
                _swingWantedPrev = wantsHit;

                if (wantsHit)
                {
                    _bufferedHit = type;
                    _bufferUntil = Time.time + hitBufferTime;
                }
                if (hitCooldown <= 0f && Time.time <= _bufferUntil && TryHit(_bufferedHit))
                    _bufferUntil = 0f;
            }
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

            if (_diveTimer > 0f)
            {
                _diveMarkAccum += moved;
                if (_diveMarkAccum >= 0.3f)
                {
                    _diveMarkAccum = 0f;
                    SandMarks.DiveStreak(flat, dir);
                }
            }
            else if (IsGrounded)
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
        /// A block: while airborne and near the net, an opponent's attack within reach is
        /// stuffed straight back down onto the attackers' side. Returns true if it happened.
        /// </summary>
        protected bool TryBlock()
        {
            if (ball == null || IsGrounded || !ball.CanBeHit) return false;
            if (match != null && !match.CanTeamTouch(team)) return false;
            if (match != null && match.ServeInFlight) return false; // a serve may not be blocked
            if (ball.LastTouchTeam != team.Other()) return false; // only block incoming attacks

            Vector3 bp = ball.transform.position;
            TeamSide opp = team.Other();
            float fwd = CourtGeometry.SideSign(opp); // +Z for team A: toward the net / opponents

            if (Mathf.Abs(transform.position.z) > blockNetDistance) return false; // I'm at the net
            if (Mathf.Abs(bp.z) > blockBallBand) return false;                    // ball is right at the net
            if (bp.y < blockMinHeight || bp.y > height + hitReachHeight) return false;

            // Must be close to the ball. The near-net band above keeps blocks at the net, so we
            // never reach deep into the opponents' court to pick off their passes.
            if (Vector2.Distance(new Vector2(transform.position.x, transform.position.z),
                                 new Vector2(bp.x, bp.z)) > blockReach)
                return false;

            // Meet the ball just over the net (hands over the tape): nudge the contact forward a
            // little so the downward stuff originates on the attackers' side and clears the net.
            Vector3 contact = bp;
            float forwardShift = Mathf.Clamp((fwd * 0.2f - bp.z) * fwd, 0f, 1.0f);
            contact.z = bp.z + forwardShift * fwd;
            ball.transform.position = contact;

            // stuff it straight down onto the attackers' near court
            float ox = Mathf.Clamp(contact.x + Random.Range(-0.8f, 0.8f),
                                   -CourtGeometry.HalfWidth + 0.3f, CourtGeometry.HalfWidth - 0.3f);
            float oz = fwd * CourtGeometry.HalfDepth * 0.22f;
            float blockError = ComputeContactError(HitType.Block, ball.Body.linearVelocity.magnitude);
            Vector3 blockTarget = ApplyContactError(new Vector3(ox, 0.2f, oz), blockError);
            ball.LaunchTo(blockTarget, 0.5f, team, this, HitType.Block);
            match?.RegisterTouch(team, this);
            LogContact(HitType.Block, blockTarget);
            hitCooldown = 0.25f;
            return true;
        }

        /// <summary>Commit to a dive: a fast grounded lunge along the held steer direction,
        /// or the way we were last running when nothing is held right now.</summary>
        void StartDive(Vector2 steer)
        {
            if (steer.sqrMagnitude < 0.01f) steer = _lastMoveDir;
            Vector3 dir = new Vector3(steer.x, 0f, steer.y);
            if (dir.sqrMagnitude < 0.01f) dir = new Vector3(0f, 0f, CourtGeometry.SideSign(team.Other()));
            _diveDir = dir.normalized;
            _diveTimer = Cfg.diveDuration;
            TriggerSwing(HitType.Dive);
            VBLog.Event($"DIVE by '{name}' team={team} dir={VBLog.V(_diveDir)}");
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
            if (Vector2.Distance(new Vector2(transform.position.x, transform.position.z),
                                 new Vector2(bp.x, bp.z)) > Cfg.diveReach) return false;

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
            hitCooldown = 0.25f;
            return true;
        }

        /// <summary>Per-controller skill multiplier on contact error (1 = baseline human).</summary>
        protected virtual float ContactSkill => 1f;

        /// <summary>
        /// The serve's contact error for this player. Serves are launched by MatchManager
        /// (outside TryHit), so this exposes the full error model — the character's control
        /// stat and the controller's skill both shape serve placement.
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
                error += Mathf.Abs(vertVel) * cfg.jumpTimingPenalty;
            }

            // Reaching at the edge of our range is a worse contact than one right at our feet.
            if (ball != null)
            {
                float dist = Vector2.Distance(new Vector2(transform.position.x, transform.position.z),
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
        void LogContact(HitType type, Vector3 target)
        {
            if (ball == null) return;
            Vector3 v = ball.Body.linearVelocity;
            int touch = match != null ? match.Touches : 0;
            VBLog.Event($"{type} by '{name}' team={team} touch#{touch} from={VBLog.V(ball.transform.position)} " +
                        $"target={VBLog.V(target)} vel={VBLog.V(v)} speed={v.magnitude:F1} spin={ball.Spin:F0}");
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

            float headY = height + hitReachHeight;
            if (bp.y > headY || bp.y < 0f) return false;

            Vector2 a = new Vector2(transform.position.x, transform.position.z);
            Vector2 b = new Vector2(bp.x, bp.z);
            return Vector2.Distance(a, b) <= reach;
        }

        protected virtual bool TryHit(HitType type)
        {
            if (ball == null || !ball.CanBeHit || !BallInReach()) return false;
            if (match != null && !match.CanTeamTouch(team)) return false;

            // measure the incoming pace BEFORE we relaunch — a hard-driven ball is harder to handle
            float incomingSpeed = ball.Body.linearVelocity.magnitude;
            float error = ComputeContactError(type, incomingSpeed);
            Vector3 target = ApplyContactError(ChooseHitTarget(type), error);
            ball.LaunchTo(target, ApexFor(type, ball.transform.position.y), team, this, type);
            match?.RegisterTouch(team, this);
            LogContact(type, target);
            hitCooldown = 0.25f;
            return true;
        }
    }
}
