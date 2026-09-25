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
            BuildBroadcastCamera();
            ToonArtKit.BuildBeachEnvironment(root);
            int solids = DecorColliders.ApplyTo(root);
            Debug.Log($"[Volleyball] Toon beach dressed; {solids} props made solid.");
        }

        /// <summary>
        /// The broadcast camera, pulled in along the classic sideline sightline (same yaw, so
        /// camera-relative controls are unchanged) so the 3D animals read bigger on screen.
        /// </summary>
        static void BuildBroadcastCamera()
        {
            if (Camera.main != null || GameObject.FindGameObjectWithTag("MainCamera") != null) return;

            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = CameraFov;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 400f;
            go.AddComponent<AudioListener>();
            go.transform.position = CameraPosition;
            go.transform.LookAt(CameraTarget);
        }

        // classic view was (20, 12, -3) looking at (0, 1.6, 0) with FOV 36
        public static readonly Vector3 CameraTarget = new Vector3(0f, 1.4f, 0f);
        public static readonly Vector3 CameraPosition = CameraTarget + new Vector3(20f, 10.4f, -3f) * 0.72f;
        public const float CameraFov = 40f;

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
