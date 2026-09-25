using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Volleyball
{
    /// <summary>
    /// Look-dev controller for the 3D overhaul: applies the toon lighting preset (sun, trilight
    /// ambient, sky, fog) and cycles camera shots with C in play mode. Lives only in the generated
    /// LookDev scene (Volleyball → 3D → Build Look-Dev Scene); game code never depends on it.
    /// </summary>
    [ExecuteAlways]
    public class LookDevStyle : MonoBehaviour
    {
        [Serializable]
        public class Preset
        {
            public Color sunColor = new Color(1.00f, 0.95f, 0.86f);
            public float sunIntensity = 1.15f;
            public Vector3 sunEuler = new Vector3(42f, -45f, 0f);
            public Color ambientSky = new Color(0.62f, 0.72f, 0.95f);
            public Color ambientEquator = new Color(0.70f, 0.68f, 0.72f);
            public Color ambientGround = new Color(0.55f, 0.48f, 0.45f);
            public Color skyTint = new Color(0.46f, 0.56f, 0.70f);
            public Color skyGround = new Color(0.62f, 0.64f, 0.68f);
            public float skyExposure = 1.25f;
            public Color fogColor = new Color(0.70f, 0.85f, 1.00f);
        }

        [Serializable]
        public class Shot
        {
            public string label;
            public Vector3 position;
            public Vector3 lookAt;
            public float fov = 36f;
        }

        public Light sun;
        public Camera cam;
        public Material skybox;
        public Preset preset = new Preset();
        public Shot[] shots = new Shot[0];
        public int shot;

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        public void Apply()
        {
            var p = preset ??= new Preset();
            if (sun != null)
            {
                sun.color = p.sunColor;
                sun.intensity = p.sunIntensity;
                sun.transform.rotation = Quaternion.Euler(p.sunEuler);
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = p.ambientSky;
            RenderSettings.ambientEquatorColor = p.ambientEquator;
            RenderSettings.ambientGroundColor = p.ambientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 190f;
            RenderSettings.fogColor = p.fogColor;

            if (skybox != null)
            {
                skybox.SetColor("_SkyTint", p.skyTint);
                skybox.SetColor("_GroundColor", p.skyGround);
                skybox.SetFloat("_Exposure", p.skyExposure);
                RenderSettings.skybox = skybox;
            }

            ApplyShot();
        }

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
