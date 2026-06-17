using UnityEngine;

namespace Volleyball
{
    /// <summary>The human-controlled player. Reads unified input from <see cref="GameInput"/>.</summary>
    public class PlayerController : VolleyPlayer
    {
        protected override Vector2 ReadMove()
            => GameInput.Instance != null ? GameInput.Instance.Move : Vector2.zero;

        protected override bool ReadJumpPressed()
            => GameInput.Instance != null && GameInput.Instance.JumpPressed;

        protected override bool ReadHitPressed()
            => GameInput.Instance != null && GameInput.Instance.HitPressed;

        protected override Vector3 ChooseHitTarget(bool spike)
        {
            TeamSide opp = team.Other();
            float sign = CourtGeometry.SideSign(opp);

            // steer the shot with the current movement input
            Vector2 steer = GameInput.Instance != null ? GameInput.Instance.Move : Vector2.zero;

            float x = steer.x * CourtGeometry.HalfWidth;
            float depthFrac = (spike ? 0.55f : 0.7f) + steer.y * 0.25f;
            depthFrac = Mathf.Clamp(depthFrac, 0.2f, 0.95f);
            float z = sign * CourtGeometry.HalfDepth * depthFrac;

            x = Mathf.Clamp(x, -CourtGeometry.HalfWidth + 0.3f, CourtGeometry.HalfWidth - 0.3f);
            return new Vector3(x, 0.6f, z);
        }
    }
}
