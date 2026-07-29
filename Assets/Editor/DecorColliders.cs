using UnityEngine;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// Makes an arena's set dressing solid, so a ball hit into the stands bounces off the
    /// concrete instead of sailing through it.
    ///
    /// It runs as a POST-PASS over a finished decor hierarchy rather than at each prop's
    /// creation, because the decision needs every prop's final world bounds and the builders set
    /// position/scale after they spawn the primitive. Three kinds of decoration deliberately
    /// stay non-colliding:
    ///
    ///  - anything overlapping the play volume (the court plus a margin, up to a height no ball
    ///    reaches) — gameplay must never be obstructed, and that includes the net posts right at
    ///    the sideline, which would otherwise deflect balls back into a live rally;
    ///  - the horizon-wide ground sheets (ocean, surrounding floor). They sit just BELOW the
    ///    sand, so a collider there would catch every ball that lands outside the court and
    ///    hide it from the GroundMarker plane that does the scoring;
    ///  - props floating above the ball's reach, where a collider could never be met.
    ///
    /// Anything the ball can wedge itself into is covered by MatchManager's rally watchdog.
    /// </summary>
    public static class DecorColliders
    {
        /// <summary>Court + margin, from below the sand to above any possible ball height.</summary>
        static readonly Bounds PlayVolume = new Bounds(
            new Vector3(0f, 6.5f, 0f),
            new Vector3((CourtGeometry.HalfWidth + 1.5f) * 2f, 17f, (CourtGeometry.HalfDepth + 2f) * 2f));

        const float SheetSize = 40f;   // wider than this horizontally = a horizon sheet, not a prop
        const float OutOfReachY = 15f; // a prop starting above this can never be hit

        /// <summary>Give every eligible mesh under <paramref name="root"/> a collider.
        /// Idempotent — props that already have one are left alone. Returns how many it added.</summary>
        public static int ApplyTo(Transform root)
        {
            if (root == null) return 0;

            int added = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var go = mf.gameObject;
                if (go.GetComponent<Collider>() != null) continue;

                var rend = go.GetComponent<Renderer>();
                if (rend == null || mf.sharedMesh == null) continue;
                if (!ShouldCollide(rend.bounds)) continue;

                AddColliderFor(go, mf.sharedMesh);
                added++;
            }
            return added;
        }

        static bool ShouldCollide(Bounds b)
        {
            if (b.Intersects(PlayVolume)) return false;
            if (b.size.x > SheetSize || b.size.z > SheetSize) return false;
            if (b.min.y > OutOfReachY) return false;
            return true;
        }

        /// <summary>The cheapest collider that matches the primitive we're looking at. The
        /// builders make everything from Unity primitives, whose mesh names are stable.</summary>
        static void AddColliderFor(GameObject go, Mesh mesh)
        {
            string n = mesh.name;
            if (n.StartsWith("Cube") || n.StartsWith("Plane") || n.StartsWith("Quad"))
            {
                go.AddComponent<BoxCollider>();
            }
            else if (n.StartsWith("Sphere"))
            {
                go.AddComponent<SphereCollider>();
            }
            else if (n.StartsWith("Cylinder") || n.StartsWith("Capsule"))
            {
                // Unity's cylinder/capsule meshes are 2 units tall with radius 0.5 — the capsule
                // collider's defaults already match, and it scales with the transform.
                go.AddComponent<CapsuleCollider>();
            }
            else
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
            }
        }
    }
}
