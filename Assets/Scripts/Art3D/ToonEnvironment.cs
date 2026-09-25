using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Volleyball
{
    /// <summary>
    /// The toon look's lighting: sun, trilight ambient, procedural sky tint and fog. Applied on
    /// enable (edit + play) by the arena's environment object, so a generated scene carries its
    /// own lighting instead of relying on baked lighting data. The ambient probe is pushed by
    /// hand from the trilight colours — batch-built scenes never get one baked, and without it
    /// every Volleyball/Stylized surface would lose its ambient term.
    /// </summary>
    [ExecuteAlways]
    public class ToonEnvironment : MonoBehaviour
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
            public float fogStart = 45f;
            public float fogEnd = 190f;
        }

        public Light sun;
        public Material skybox;
        public Preset preset = new Preset();

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        public void Apply() => Apply(preset, sun, skybox);

        public static void Apply(Preset p, Light sun, Material skybox)
        {
            p ??= new Preset();
            if (sun != null)
            {
                sun.color = p.sunColor;
                sun.intensity = p.sunIntensity;
                sun.transform.rotation = Quaternion.Euler(p.sunEuler);
                RenderSettings.sun = sun;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = p.ambientSky;
            RenderSettings.ambientEquatorColor = p.ambientEquator;
            RenderSettings.ambientGroundColor = p.ambientGround;

            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(p.ambientEquator * 0.55f);
            sh.AddDirectionalLight(Vector3.up, p.ambientSky, 0.55f);
            sh.AddDirectionalLight(Vector3.down, p.ambientGround, 0.45f);
            RenderSettings.ambientProbe = sh;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = p.fogStart;
            RenderSettings.fogEndDistance = p.fogEnd;
            RenderSettings.fogColor = p.fogColor;

            if (skybox != null)
            {
                skybox.SetColor("_SkyTint", p.skyTint);
                skybox.SetColor("_GroundColor", p.skyGround);
                skybox.SetFloat("_Exposure", p.skyExposure);
                RenderSettings.skybox = skybox;
            }
        }
    }
}
