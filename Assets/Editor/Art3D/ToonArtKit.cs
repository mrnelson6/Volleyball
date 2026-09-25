using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// Shared pieces for building toon-style scenes from the generated art (Assets/Art/, written
    /// by Tools/blender/*_gen.py): the Volleyball/Stylized materials, model placement, the
    /// lighting rig, and the Sunset Beach environment layout. Used by the toon arena builder
    /// (<see cref="ToonBeachDecorator"/>) and the look-dev scene (<see cref="LookDevBuilder"/>),
    /// so both always show the same beach.
    /// </summary>
    public static class ToonArtKit
    {
        public const string MatDir = "Assets/Art/Materials";
        public const string CharDir = "Assets/Art/Characters";
        public const string PropDir = "Assets/Art/Props";

        /// <summary>Yaw that turns a generated animal (which faces -Z after import) to face +Z.</summary>
        public const float ModelFacingYaw = 180f;

        // ------------------------------------------------------------------ materials

        static Shader Stylized => Shader.Find("Volleyball/Stylized");
        static Texture2D PropPalette => AssetDatabase.LoadAssetAtPath<Texture2D>(PropDir + "/props_palette.png");

        public static Material PropsMaterial() => Mat("VB_Props", Stylized, m =>
        {
            m.SetTexture("_PaletteTex", PropPalette);
            m.SetFloat("_OutlineWidth", 1f);
        });

        /// <summary>Terrain, ocean and the thin court lines: no outline.</summary>
        public static Material GroundMaterial() => Mat("VB_Ground", Stylized, m =>
        {
            m.SetTexture("_PaletteTex", PropPalette);
            m.SetFloat("_OutlineWidth", 0f);
        });

        /// <summary>Animals: palette comes per-renderer from AnimalLook, so no texture here.</summary>
        public static Material AnimalMaterial() => Mat("VB_Animal", Stylized, m =>
        {
            m.SetFloat("_OutlineWidth", 0.8f);                           // thinner than props
            m.SetColor("_OutlineColor", new Color(0.30f, 0.20f, 0.18f)); // warm brown, not black
            m.SetColor("_BaseColor", new Color(1.12f, 1.12f, 1.12f));   // characters a touch brighter
            m.SetFloat("_ShadowLift", 0.45f);
        });

        public static Material SkyMaterial() => Mat("VB_ToonSky", Shader.Find("Skybox/Procedural"), m =>
        {
            m.SetFloat("_SunSize", 0.03f);
            m.SetFloat("_AtmosphereThickness", 0.8f);
        });

        static Material Mat(string name, Shader shader, System.Action<Material> setup)
        {
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else m.shader = shader;
            setup(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ placement

        /// <summary>
        /// Instantiate a generated model under a placement holder. The model root carries the
        /// importer's axis-conversion rotation, so position/yaw/scale go on the holder — writing
        /// the root's rotation would tip Blender's Z-up meshes over.
        /// </summary>
        public static GameObject InstantiateModel(string assetPath, Transform parent)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (model == null)
            {
                Debug.LogError("[Volleyball] Missing generated model " + assetPath +
                               " — run Tools/blender/*_gen.py export first.");
                var missing = new GameObject(Path.GetFileNameWithoutExtension(assetPath) + " (missing)");
                if (parent != null) missing.transform.SetParent(parent, false);
                return missing;
            }
            var holder = new GameObject(model.name).transform;
            if (parent != null) holder.SetParent(parent, false);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.transform.SetParent(holder, false);
            return holder.gameObject;
        }

        public static GameObject Prop(string name, Vector3 pos, float yaw, float scale, Material mat,
                                      Transform parent, bool castShadows = true)
        {
            var go = InstantiateModel($"{PropDir}/prop_{name}.fbx", parent);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            return go;
        }

        // ------------------------------------------------------------------ lighting

        /// <summary>Sun + <see cref="ToonEnvironment"/> (applies sky, ambient probe, fog on enable).</summary>
        public static ToonEnvironment BuildLighting(Transform root)
        {
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(root, false);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;

            var env = new GameObject("Toon Environment").AddComponent<ToonEnvironment>();
            env.transform.SetParent(root, false);
            env.sun = sun;
            env.skybox = SkyMaterial();
            env.Apply();
            return env;
        }

        // ------------------------------------------------------------------ Sunset Beach

        /// <summary>
        /// Terrain, ocean, court lines + strung net, and the beach props. Everything is court-
        /// centred at the origin (CourtGeometry) with the ocean on the far side (-X) from the
        /// broadcast camera. Props sit inside the flat sand pad (|x| &lt; 16, |z| &lt; 22).
        /// </summary>
        public static void BuildBeachEnvironment(Transform root)
        {
            var props = PropsMaterial();
            var ground = GroundMaterial();

            Prop("terrain", Vector3.zero, 0f, 1f, ground, root, castShadows: false);
            Prop("ocean", Vector3.zero, 0f, 1f, ground, root, castShadows: false);
            Prop("court", Vector3.zero, 0f, 1f, ground, root); // thin lines: no outline

            Prop("palm", new Vector3(-11f, 0f, -14f), 20f, 1f, props, root);
            Prop("palm_b", new Vector3(-11.5f, 0f, 8f), 140f, 1.1f, props, root);
            Prop("palm", new Vector3(-9.5f, 0f, 15.5f), 250f, 0.9f, props, root);
            Prop("palm_b", new Vector3(-16f, 0f, -3f), 60f, 1.2f, props, root);
            Prop("umbrella", new Vector3(-7.5f, 0f, -11.5f), 0f, 1f, props, root);
            Prop("towel", new Vector3(-7.6f, 0f, -10.2f), 80f, 1f, props, root);
            Prop("cooler", new Vector3(-6.3f, 0f, -12.8f), 20f, 1f, props, root);
            Prop("lifeguard_tower", new Vector3(-9.5f, 0f, 2f), 90f, 1f, props, root);
            Prop("surfboard", new Vector3(-7.2f, 0f, 11.8f), 70f, 1f, props, root);
            Prop("rock_a", new Vector3(-12.5f, 0f, -6f), 0f, 1.4f, props, root);
            Prop("rock_b", new Vector3(-13.5f, 0f, 12.5f), 40f, 1.8f, props, root);
            Prop("rock_a", new Vector3(10f, 0f, -15f), 120f, 1.0f, props, root);
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    Prop("tiki_torch", new Vector3(sx * 6.2f, 0f, sz * 10f), 0f, 1f, props, root);
            Prop("beach_ball", new Vector3(-5.8f, 0.3f, -8.5f), 30f, 0.3f, props, root);
        }
    }
}
