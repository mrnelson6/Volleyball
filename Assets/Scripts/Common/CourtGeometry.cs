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

        // The outer leash on where a player may walk — everything INSIDE it is decided by the
        // world's colliders, not by rules. Deliberately one box for everybody rather than a
        // per-team half: nobody is fenced into their own court, so you can walk to the other
        // side, going around the end of the net like a person.
        //
        // Sized to reach a useful way into the grandstands, which start 3m outside the lines
        // and step outward in 1.4m treads (see ArenaDecorator.BuildGrandstand): this covers
        // the first four tiers, whose 1.2m rises are each one jump apart. The ground sheet
        // runs to about x +-16, z +-22 if you want to open it up all the way.
        public const float RoamHalfWidth = HalfWidth + 8f;  // x +-12: four tiers of sideline stand
        public const float RoamHalfDepth = HalfDepth + 8f;  // z +-16: four tiers of end stand

        /// <summary>Half-width of the net barrier: the mesh CourtKit builds spans HalfWidth*2+1,
        /// plus a body's width so you have to properly clear the post to get around the end.</summary>
        public const float NetBlockHalfWidth = HalfWidth + 0.5f + 0.35f;
        /// <summary>How close a body can press against the tape.</summary>
        public const float NetStandoff = 0.35f;

        public static TeamSide SideOf(float z) => z < NetZ ? TeamSide.A : TeamSide.B;
        public static TeamSide SideOf(Vector3 p) => SideOf(p.z);

        /// <summary>+1 for team B (positive Z), -1 for team A (negative Z).</summary>
        public static float SideSign(TeamSide side) => side == TeamSide.A ? -1f : 1f;

        public static bool InBounds(Vector3 p)
            => Mathf.Abs(p.x) <= HalfWidth && Mathf.Abs(p.z) <= HalfDepth;

        /// <summary>A point near the middle of a team's half of the court.</summary>
        public static Vector3 CourtCenter(TeamSide side)
            => new Vector3(0f, 0f, SideSign(side) * HalfDepth * 0.5f);

        /// <summary>
        /// The net as a solid wall for bodies: slides <paramref name="to"/> back out if a move
        /// from <paramref name="from"/> would end inside the tape or pass through it. Beyond the
        /// posts (|x| > NetBlockHalfWidth) it lets you straight through — that gap is the whole
        /// point, it's how you get to the other side. Pure geometry, safe inside Simulate.
        /// </summary>
        public static Vector3 BlockNetCrossing(Vector3 from, Vector3 to)
        {
            if (Mathf.Abs(to.x) > NetBlockHalfWidth) return to; // round the post — free passage

            bool wentThrough = (from.z < NetZ) != (to.z < NetZ); // caught a whole tick's step
            bool insideTape = Mathf.Abs(to.z - NetZ) < NetStandoff;
            if (!wentThrough && !insideTape) return to;

            // Push back to the side they came FROM, so a body pressed against the net slides
            // along it instead of popping out the far side.
            float side = from.z != NetZ ? Mathf.Sign(from.z - NetZ)
                                        : (to.z < NetZ ? -1f : 1f);
            to.z = NetZ + side * NetStandoff;
            return to;
        }
    }
}
