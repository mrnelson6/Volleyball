using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Volleyball
{
    /// <summary>
    /// Look-dev camera shots: C cycles them in play mode. Lighting comes from the scene's
    /// <see cref="ToonEnvironment"/> (the same component the toon arenas use). Lives only in the
    /// generated LookDev scene (Volleyball → 3D → Build Look-Dev Scene).
    /// </summary>
    [ExecuteAlways]
    public class LookDevStyle : MonoBehaviour
    {
        [Serializable]
        public class Shot
        {
            public string label;
            public Vector3 position;
            public Vector3 lookAt;
            public float fov = 36f;
        }

        public Camera cam;
        public Shot[] shots = new Shot[0];
        public int shot;

        void OnEnable() => ApplyShot();
        void OnValidate() => ApplyShot();

        public void ApplyShot()
        {
            if (cam == null || shots == null || shots.Length == 0) return;
            var s = shots[Mathf.Clamp(shot, 0, shots.Length - 1)];
            cam.transform.position = s.position;
            cam.transform.LookAt(s.lookAt);
            cam.fieldOfView = s.fov;
        }

        void Update()
        {
            if (!Application.isPlaying) return;
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k != null && k.cKey.wasPressedThisFrame && shots.Length > 0)
            {
                shot = (shot + 1) % shots.Length;
                ApplyShot();
            }
#endif
        }

        void OnGUI()
        {
            if (!Application.isPlaying || shots.Length == 0) return;
            GUI.Label(new Rect(12, 10, 600, 24), $"Shot: {shots[Mathf.Clamp(shot, 0, shots.Length - 1)].label}  [C] next");
        }
    }
}
