using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Shared court dimensions and helpers. Court is centred on the origin:
    /// X spans [-HalfWidth, HalfWidth], Z spans [-HalfDepth, HalfDepth], net at Z = 0.
    /// Team A occupies negative Z (near the camera); Team B occupies positive Z.
    /// </summary>
    public static class CourtGeometry
    {
        public const float HalfWidth = 4f;   // X extent of the court
        public const float HalfDepth = 8f;   // Z extent of the court
        public const float NetZ = 0f;
        public const float NetHeight = 2.2f;
        public const float NetBuffer = 0.4f; // players cannot get closer than this to the net

        public static TeamSide SideOf(float z) => z < NetZ ? TeamSide.A : TeamSide.B;
        public static TeamSide SideOf(Vector3 p) => SideOf(p.z);

        /// <summary>+1 for team B (positive Z), -1 for team A (negative Z).</summary>
        public static float SideSign(TeamSide side) => side == TeamSide.A ? -1f : 1f;

        public static bool InBounds(Vector3 p)
            => Mathf.Abs(p.x) <= HalfWidth && Mathf.Abs(p.z) <= HalfDepth;

        /// <summary>A point near the middle of a team's half of the court.</summary>
        public static Vector3 CourtCenter(TeamSide side)
            => new Vector3(0f, 0f, SideSign(side) * HalfDepth * 0.5f);
    }
}
