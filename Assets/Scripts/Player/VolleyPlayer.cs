using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Shared movement/jump/hit behaviour for both the human player and the AI.
    /// Movement is code-driven (no Rigidbody) on the X/Z plane with a manual vertical
    /// jump integration, so players never physically shove the ball — hits are explicit.
    /// Subclasses supply the input (keyboard vs AI decision) and the aim target.
    /// </summary>
    public abstract class VolleyPlayer : MonoBehaviour
    {
        [Header("Team")]
        public TeamSide team = TeamSide.A;

        [Tooltip("Half of the court this player favours: -1 = left (x<0), +1 = right.")]
        public float halfSign = -1f;

        [Header("Movement")]
        public float moveSpeed = 6f;
        public float jumpSpeed = 6.5f;

        [Header("Hitting")]
        public float reach = 1.9f;
        public float hitReachHeight = 2.4f;

        protected float height;   // current height above the ground (from jumping)
        protected float vertVel;
        protected float hitCooldown;

        protected MatchManager match;
        protected BallController ball;

        public bool IsGrounded => height <= 0.001f;
        public Vector3 GroundPosition => new Vector3(transform.position.x, 0f, transform.position.z);

        protected virtual void Start()
        {
            match = FindFirstObjectByType<MatchManager>();
            ball = FindFirstObjectByType<BallController>();
        }

        protected abstract Vector2 ReadMove();
        protected abstract bool ReadJumpPressed();
        protected abstract bool ReadHitPressed();
        protected abstract Vector3 ChooseHitTarget(bool spike);

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

            // --- jump + gravity ---
            if (ReadJumpPressed() && IsGrounded)
                vertVel = jumpSpeed;
            vertVel += Physics.gravity.y * dt;
            height += vertVel * dt;
            if (height <= 0f) { height = 0f; vertVel = 0f; }
            pos.y = height;

            transform.position = pos;

            // --- hitting ---
            if (ReadHitPressed() && hitCooldown <= 0f)
                TryHit();
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

        protected virtual void TryHit()
        {
            if (ball == null || !ball.CanBeHit || !BallInReach()) return;
            if (match != null && !match.CanTeamTouch(team)) return;

            bool spike = !IsGrounded;
            Vector3 target = ChooseHitTarget(spike);
            float apex = spike ? 1.3f : 2.6f; // spikes are flatter & faster, bumps loftier
            ball.LaunchTo(target, apex, team, this);
            match?.RegisterTouch(team, this);
            hitCooldown = 0.25f;
        }
    }
}
