using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Capsule-vs-world collision for the player simulation: the net and the arena's set
    /// dressing are solid, and their tops are standable, so you can be stopped by a wall or
    /// hop up onto the bleachers.
    ///
    /// Hand-rolled sweeps rather than a CharacterController or a Rigidbody, deliberately.
    /// Both of those step PhysX and write the transform, and here the transform is only the
    /// rendered view — the authority is <see cref="PlayerSimState"/>, which a predicting
    /// client rewinds and replays several times per correction. Sweeps are queries: they read
    /// the world and return an answer, so replaying a tick gives the same answer as the first
    /// run. That only holds against STATIC geometry (every machine loads the same generated
    /// scene), which is why anything hanging off a moving body — the ball — is filtered out.
    ///
    /// Note what this deliberately does NOT do: it never resolves an existing overlap. Nothing
    /// pushes a player, so the only way to end up inside geometry is to be spawned there, and
    /// the sweeps keep you out from then on. That avoids giving players their own colliders,
    /// which would put them in the ball's path.
    /// </summary>
    public static class WorldCollision
    {
        const int MaxHits = 16;
        const float Skin = 0.02f;         // leave a sliver of air so the next sweep starts outside
        const int SlideIterations = 3;    // a wall, the corner it wedges into, then give up
        const float GroundProbe = 0.5f;   // ground cast starts this far above the feet — must
                                          // stay above GameConfig.stepHeight or a step you can
                                          // walk over is too tall to then be found underfoot
        const float MinRise = 0.5f;       // normal.y below this is a wall, not a floor

        // Queries never overlap or nest, so one scratch buffer serves the whole class.
        static readonly RaycastHit[] _hits = new RaycastHit[MaxHits];

        /// <summary>Static scenery only: no triggers, and nothing carried by a moving body.
        /// The ball is the one dynamic collider in the scene and its position is not part of
        /// any player's replayable state — colliding with it would desync prediction.</summary>
        static bool IsScenery(Collider c)
        {
            if (c == null || c.isTrigger) return false;
            Rigidbody rb = c.attachedRigidbody;
            return rb == null || rb.isKinematic;
        }

        /// <summary>The two sphere centres of an upright capsule standing on <paramref name="foot"/>.</summary>
        static void Capsule(Vector3 foot, float radius, float height, out Vector3 p0, out Vector3 p1)
        {
            p0 = new Vector3(foot.x, foot.y + radius, foot.z);
            p1 = new Vector3(foot.x, foot.y + Mathf.Max(height - radius, radius), foot.z);
        }

        /// <summary>
        /// Move the body from <paramref name="from"/> toward <paramref name="to"/> horizontally,
        /// stopping at whatever it runs into and sliding the leftover motion along that surface.
        /// <paramref name="stepHeight"/> lifts the sweep so low ledges (bleacher treads, kerbs)
        /// are walked over rather than caught on — pass 0 while airborne, where a ledge should
        /// stop you. The returned y is <paramref name="to"/>'s, untouched.
        /// </summary>
        public static Vector3 SlideHorizontal(Vector3 from, Vector3 to, float radius, float height,
                                              float stepHeight)
        {
            Vector3 delta = new Vector3(to.x - from.x, 0f, to.z - from.z);
            float remaining = delta.magnitude;
            if (remaining < 1e-5f) return to;
            Vector3 dir = delta / remaining;

            Vector3 foot = new Vector3(from.x, from.y + stepHeight, from.z);

            for (int i = 0; i < SlideIterations && remaining > 1e-5f; i++)
            {
                Capsule(foot, radius, height, out Vector3 p0, out Vector3 p1);
                int n = Physics.CapsuleCastNonAlloc(p0, p1, radius, dir, _hits, remaining + Skin,
                                                    Physics.DefaultRaycastLayers,
                                                    QueryTriggerInteraction.Ignore);
                int best = -1;
                float bestDist = float.PositiveInfinity;
                for (int h = 0; h < n; h++)
                {
                    if (!IsScenery(_hits[h].collider)) continue;
                    if (_hits[h].distance <= 0f) continue; // already inside it: no usable normal
                    // Floors and ceilings are the vertical pass's business. Without this, a
                    // capsule standing on the sand grazes it on every sweep and horizontal
                    // movement stops dead; a tread's TOP is skipped here while its front face
                    // (a horizontal normal) still blocks, which is exactly what we want.
                    if (Mathf.Abs(_hits[h].normal.y) > 0.9f) continue;
                    if (_hits[h].distance < bestDist) { bestDist = _hits[h].distance; best = h; }
                }

                if (best < 0) { foot += dir * remaining; break; } // clear run

                float travel = Mathf.Max(bestDist - Skin, 0f);
                foot += dir * travel;
                remaining -= travel;

                // Slide what's left along the surface. Flattening the normal first keeps a
                // sloped face from launching or burying the body — a wall may only ever steer
                // you sideways, never up or down; height is the vertical pass's business.
                Vector3 wall = new Vector3(_hits[best].normal.x, 0f, _hits[best].normal.z);
                if (wall.sqrMagnitude < 1e-6f) break; // hit a pure floor/ceiling face — done
                Vector3 left = Vector3.ProjectOnPlane(dir * remaining, wall.normalized);
                remaining = left.magnitude;
                if (remaining > 1e-5f) dir = left / remaining;
            }

            return new Vector3(foot.x, to.y, foot.z);
        }

        /// <summary>
        /// Height of whatever is holding the body up at this position — the sand, a bleacher
        /// tread, the top of a crate. Looks from just above the feet downward and takes the
        /// highest surface flat enough to stand on. <paramref name="defaultY"/> (the court
        /// floor) is both the fallback when nothing is found and a hard floor, so a gap in the
        /// set dressing can never drop anyone through the world.
        /// </summary>
        public static float GroundHeightAt(Vector3 foot, float radius, float defaultY = 0f)
        {
            Vector3 origin = new Vector3(foot.x, foot.y + GroundProbe, foot.z);
            float reach = GroundProbe + Mathf.Max(foot.y - defaultY, 0f) + 2f;

            int n = Physics.SphereCastNonAlloc(origin, radius * 0.9f, Vector3.down, _hits, reach,
                                               Physics.DefaultRaycastLayers,
                                               QueryTriggerInteraction.Ignore);
            float best = defaultY;
            for (int h = 0; h < n; h++)
            {
                if (!IsScenery(_hits[h].collider)) continue;
                if (_hits[h].distance <= 0f) continue;     // started inside it
                if (_hits[h].normal.y < MinRise) continue; // a wall, not a floor
                float y = _hits[h].point.y;
                if (y > origin.y) continue;                // above the probe: not underfoot
                if (y > best) best = y;
            }
            return best;
        }
    }
}
