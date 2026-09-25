using UnityEngine;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// Sunset Beach in the 3D toon style (replaces <see cref="ArenaDecorator"/>'s primitive
    /// dressing for BeachArena). Two passes around CourtKit:
    /// <list type="number">
    /// <item><see cref="BuildEnvironment"/> — before the court drops in: lighting, camera, the
    /// generated beach (terrain, ocean, lines + net, props), solid props via DecorColliders.</item>
    /// <item><see cref="RestyleCourt"/> — after: hides CourtKit's placeholder visuals (sand plane,
    /// cube lines, cube net) while KEEPING their colliders/markers (GroundMarker scoring, the net
    /// barrier), and gives the ball a 3D beach-ball mesh child.</item>
    /// </list>
    /// Players need nothing here: CharacterModels swaps in their 3D animals at runtime.
    /// </summary>
    public static class ToonBeachDecorator
    {
        public const string DecorRootName = "Toon Beach Decor";

        public static void BuildEnvironment()
        {
            var root = new GameObject(DecorRootName).transform;
            ToonArtKit.BuildLighting(root);
            ArenaDecorator.BuildShowcaseCamera(); // same broadcast camera the game has always used
            ToonArtKit.BuildBeachEnvironment(root);
            int solids = DecorColliders.ApplyTo(root);
            Debug.Log($"[Volleyball] Toon beach dressed; {solids} props made solid.");
        }

        public static void RestyleCourt()
        {
            // placeholder court visuals: renderer off, collider + marker stay
            foreach (var g in Object.FindObjectsByType<GroundMarker>(FindObjectsSortMode.None)) HideRenderer(g.gameObject);
            foreach (var n in Object.FindObjectsByType<NetMarker>(FindObjectsSortMode.None)) HideRenderer(n.gameObject);
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                string name = r.gameObject.name;
                if (name.StartsWith("Sideline") || name.StartsWith("Baseline") || name == "Net Line")
                    r.enabled = false;
            }

            // ball: hide the billboard sprite, add a spinning beach-ball mesh (child: the ball's
            // root transform is its simulation state)
            var ball = Object.FindAnyObjectByType<BallController>();
            if (ball != null)
            {
                foreach (var sr in ball.GetComponentsInChildren<SpriteRenderer>(true)) sr.enabled = false;
                var mesh = ToonArtKit.Prop("beach_ball", Vector3.zero, 0f, 0.3f, ToonArtKit.PropsMaterial(), ball.transform);
                mesh.name = "Model";
                mesh.AddComponent<BallSpin>().radius = 0.3f;
            }
        }

        static void HideRenderer(GameObject go)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
        }
    }
}
