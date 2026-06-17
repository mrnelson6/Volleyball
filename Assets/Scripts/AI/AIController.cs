using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Simple volleyball AI used for the teammate and both opponents. Each frame it
    /// predicts where the ball will descend to hitting height, and if that lands on its
    /// own half it moves under it and returns the ball; otherwise it falls back to a
    /// defensive home position. It "owns" the left or right half of its side (halfSign)
    /// so a team's two players don't chase the same ball.
    /// </summary>
    public class AIController : VolleyPlayer
    {
        [Header("AI")]
        public float aimError = 1.2f;
        public float spikeHeightThreshold = 1.7f;

        Vector3 _home;
        Vector2 _desiredMove;
        bool _wantJump;
        bool _wantHit;

        protected override void Start()
        {
            base.Start();
            _home = new Vector3(
                halfSign * CourtGeometry.HalfWidth * 0.45f,
                0f,
                CourtGeometry.SideSign(team) * CourtGeometry.HalfDepth * 0.5f);
        }

        protected override void Update()
        {
            Decide();
            base.Update();
        }

        void Decide()
        {
            _desiredMove = Vector2.zero;
            _wantJump = false;
            _wantHit = false;
            if (ball == null) return;

            Vector3 bp = ball.transform.position;
            Vector3 landing = PredictLanding();

            bool landsMySide = CourtGeometry.SideOf(landing) == team
                               && Mathf.Abs(landing.x) <= CourtGeometry.HalfWidth + 1.5f
                               && Mathf.Abs(landing.z) <= CourtGeometry.HalfDepth + 1.5f;
            bool myHalf = Mathf.Abs(landing.x) < 0.6f || Mathf.Sign(landing.x) == Mathf.Sign(halfSign);

            Vector3 target = (landsMySide && myHalf) ? landing : _home;

            Vector3 to = target - GroundPosition;
            Vector2 dir = new Vector2(to.x, to.z);
            _desiredMove = dir.magnitude > 0.15f ? Vector2.ClampMagnitude(dir, 1f) : Vector2.zero;

            if (BallInReach())
            {
                _wantHit = true;
                if (IsGrounded && bp.y > spikeHeightThreshold) _wantJump = true;
            }
        }

        /// <summary>Project the ball forward to where it next descends to hitting height.</summary>
        Vector3 PredictLanding()
        {
            Vector3 p = ball.transform.position;
            Vector3 v = ball.Body.linearVelocity;
            float g = -Physics.gravity.y;
            const float targetY = 1f;

            // solve  p.y + v.y*t - 0.5*g*t^2 = targetY  for the later (descending) root
            float a = 0.5f * g;
            float b = -v.y;
            float c = targetY - p.y;
            float disc = b * b - 4f * a * c;

            float t;
            if (disc <= 0f) t = Mathf.Max(v.y / g, 0.2f);
            else t = (-b + Mathf.Sqrt(disc)) / (2f * a);
            t = Mathf.Clamp(t, 0.05f, 4f);

            return new Vector3(p.x + v.x * t, 0f, p.z + v.z * t);
        }

        protected override Vector2 ReadMove() => _desiredMove;
        protected override bool ReadJumpPressed() => _wantJump;
        protected override bool ReadHitPressed() => _wantHit;

        protected override Vector3 ChooseHitTarget(bool spike)
        {
            TeamSide opp = team.Other();
            float sign = CourtGeometry.SideSign(opp);

            float x = Random.Range(-CourtGeometry.HalfWidth * 0.8f, CourtGeometry.HalfWidth * 0.8f)
                      + Random.Range(-aimError, aimError);
            float z = sign * CourtGeometry.HalfDepth * Random.Range(0.45f, 0.85f);

            x = Mathf.Clamp(x, -CourtGeometry.HalfWidth + 0.3f, CourtGeometry.HalfWidth - 0.3f);
            return new Vector3(x, 0.6f, z);
        }
    }
}
