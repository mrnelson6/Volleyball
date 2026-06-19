using UnityEngine;

namespace Volleyball
{
    /// <summary>The kind of contact a player makes with the ball.</summary>
    public enum HitType { Bump, Set, Spike, Serve, Block }

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

        // Sizes and speeds are global — edit them in the GameConfig asset
        // (Volleyball → Create Game Config), not per-player.
        static GameConfig Cfg => GameConfig.Instance;
        public float moveSpeed => Cfg.moveSpeed;
        public float jumpSpeed => Cfg.jumpSpeed;
        public float reach => Cfg.reach;
        public float hitReachHeight => Cfg.hitReachHeight;
        public float hitBufferTime => Cfg.hitBufferTime;
        public float blockNetDistance => Cfg.blockNetDistance;
        public float blockMinHeight => Cfg.blockMinHeight;
        public float blockReach => Cfg.blockReach;
        public float blockBallBand => Cfg.blockBallBand;

        protected float height;   // current height above the ground (from jumping)
        protected float vertVel;
        protected float hitCooldown;

        HitType _bufferedHit;
        float _bufferUntil;
        bool _swingWantedPrev;

        protected MatchManager match;
        protected BallController ball;

        public bool IsGrounded => height <= 0.001f;
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
        }

        protected abstract Vector2 ReadMove();
        protected abstract bool ReadJumpPressed();
        protected abstract Vector3 ChooseHitTarget(HitType type);

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
                default: return 4f;             // serve
            }
        }

        public void ResetState()
        {
            height = 0f;
            vertVel = 0f;
            hitCooldown = 0f;
        }

        protected virtual void Update()
        {
            float dt = Time.deltaTime;
            hitCooldown -= dt;

            // --- horizontal movement (clamped to own side of the net) ---
            Vector2 mv = ReadMove();
            Vector3 pos = transform.position;
            pos.x += mv.x * moveSpeed * dt;
            pos.z += mv.y * moveSpeed * dt;

            const float margin = 1f;
            pos.x = Mathf.Clamp(pos.x, -(CourtGeometry.HalfWidth + margin), CourtGeometry.HalfWidth + margin);
            if (team == TeamSide.A)
                pos.z = Mathf.Clamp(pos.z, -(CourtGeometry.HalfDepth + margin), -CourtGeometry.NetBuffer);
            else
                pos.z = Mathf.Clamp(pos.z, CourtGeometry.NetBuffer, CourtGeometry.HalfDepth + margin);

            // the server must stay behind their own back line until they serve
            if (match != null && match.IsServePhaseFor(this))
            {
                if (team == TeamSide.A) pos.z = Mathf.Min(pos.z, -CourtGeometry.HalfDepth);
                else pos.z = Mathf.Max(pos.z, CourtGeometry.HalfDepth);
            }

            // --- jump + gravity ---
            if (ReadJumpPressed() && IsGrounded)
                vertVel = jumpSpeed;
            vertVel += Physics.gravity.y * dt;
            height += vertVel * dt;
            if (height <= 0f) { height = 0f; vertVel = 0f; }
            pos.y = height;

            transform.position = pos;

            // --- blocking: jumping at the net into an opponent's attack auto-blocks it ---
            if (hitCooldown <= 0f && TryBlock())
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

        /// <summary>Per-controller skill multiplier on contact error (1 = baseline human).</summary>
        protected virtual float ContactSkill => 1f;

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

            return Mathf.Min(error * ContactSkill, cfg.maxContactError);
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
