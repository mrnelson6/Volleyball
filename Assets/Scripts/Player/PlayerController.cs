using UnityEngine;

namespace Volleyball
{
    /// <summary>The human-controlled player. Reads unified input from <see cref="GameInput"/>.</summary>
    public class PlayerController : VolleyPlayer
    {
        protected override Vector2 ReadMove()
        {
            Vector3 w = CamRelativeDir(GameInput.Instance != null ? GameInput.Instance.Move : Vector2.zero);
            return new Vector2(w.x, w.z);
        }

        protected override bool ReadJumpPressed()
            => GameInput.Instance != null && GameInput.Instance.JumpPressed;

        protected override bool TryGetDesiredHit(out HitType type)
        {
            var gi = GameInput.Instance;
            if (gi != null)
            {
                if (gi.SpikePressed) { type = HitType.Spike; return true; }
                if (gi.SetPressed) { type = HitType.Set; return true; }
                if (gi.BumpPressed) { type = HitType.Bump; return true; }
            }
            type = HitType.Bump;
            return false;
        }

        protected override Vector3 ChooseHitTarget(HitType type)
        {
            Vector3 steer = CamRelativeDir(GameInput.Instance != null ? GameInput.Instance.Move : Vector2.zero);

            if (type == HitType.Set)
            {
                // keep it on our own side, up near the net, to set up a spike
                float sx = Mathf.Clamp(transform.position.x + steer.x * 3f,
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
                    float px = Mathf.Clamp(transform.position.x + steer.x * 3f,
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

        /// <summary>Convert screen-relative input into a world XZ direction using the camera.</summary>
        static Vector3 CamRelativeDir(Vector2 input)
        {
            Camera cam = Camera.main;
            if (cam == null) return new Vector3(input.x, 0f, input.y);

            Vector3 f = cam.transform.forward; f.y = 0f; f.Normalize();
            Vector3 r = cam.transform.right; r.y = 0f; r.Normalize();
            return r * input.x + f * input.y;
        }
    }
}
