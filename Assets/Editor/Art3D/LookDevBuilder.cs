using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// Phase-0 look-dev for the 3D overhaul: assembles a beach court from the generated models
    /// (Assets/Art/, see Tools/blender/) into Assets/Scenes/LookDev.unity, and renders each
    /// camera shot in the chosen toon style into lookdev_sheet.png at the project root.
    ///
    /// Deliberately NOT part of the World Tour build and not in Build Settings: no network
    /// objects, so it can't disturb the in-scene GlobalObjectIdHash invariant.
    /// </summary>
    public static class LookDevBuilder
    {
        public const string ScenePath = "Assets/Scenes/LookDev.unity";
        const string MatDir = "Assets/Art/Materials";
        const string CharDir = "Assets/Art/Characters";
        const string PropDir = "Assets/Art/Props";

        /// <summary>Yaw that turns a generated model to face Unity +Z (Blender -Y after import).</summary>
        const float ModelFacingYaw = 0f;

        static readonly Color JerseyA = new Color(0.20f, 0.50f, 0.95f);
        static readonly Color JerseyA2 = new Color(0.45f, 0.80f, 1.00f);
        static readonly Color JerseyB = new Color(0.95f, 0.30f, 0.25f);
        static readonly Color JerseyB2 = new Color(0.98f, 0.60f, 0.20f);

        [MenuItem("Volleyball/3D/Build Look-Dev Scene", priority = 201)]
        public static void Build()
        {
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var shader = Shader.Find("Volleyball/Stylized");
            var propPalette = AssetDatabase.LoadAssetAtPath<Texture2D>(PropDir + "/props_palette.png");
            var propsMat = Mat("VB_Props", shader, m => { m.SetTexture("_PaletteTex", propPalette); m.SetFloat("_OutlineWidth", 1f); });
            var groundMat = Mat("VB_Ground", shader, m => { m.SetTexture("_PaletteTex", propPalette); m.SetFloat("_OutlineWidth", 0f); });
            var animalMat = Mat("VB_Animal", shader, m =>
            {
                m.SetFloat("_OutlineWidth", 0.8f);                           // thinner than props
                m.SetColor("_OutlineColor", new Color(0.30f, 0.20f, 0.18f)); // warm brown, not black
                m.SetColor("_BaseColor", new Color(1.12f, 1.12f, 1.12f));   // characters a touch brighter
                m.SetFloat("_ShadowLift", 0.45f);
            });
            var sky = Mat("VB_LookDevSky", Shader.Find("Skybox/Procedural"), m =>
            {
                m.SetFloat("_SunSize", 0.03f);
                m.SetFloat("_AtmosphereThickness", 0.8f);
            });

            // --- light + camera
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            RenderSettings.sun = sun;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 400f;
            camGo.AddComponent<AudioListener>();

            // --- environment
            var env = new GameObject("Environment").transform;
            Prop("terrain", Vector3.zero, 0f, 1f, groundMat, env);
            Prop("ocean", Vector3.zero, 0f, 1f, groundMat, env);
            Prop("court", Vector3.zero, 0f, 1f, groundMat, env); // thin lines: no outline

            Prop("palm", new Vector3(-11f, 0f, -14f), 20f, 1f, propsMat, env);
            Prop("palm_b", new Vector3(-11.5f, 0f, 8f), 140f, 1.1f, propsMat, env);
            Prop("palm", new Vector3(-9.5f, 0f, 15.5f), 250f, 0.9f, propsMat, env);
            Prop("palm_b", new Vector3(-16f, 0f, -3f), 60f, 1.2f, propsMat, env);
            Prop("umbrella", new Vector3(-7.5f, 0f, -11.5f), 0f, 1f, propsMat, env);
            Prop("towel", new Vector3(-7.6f, 0f, -10.2f), 80f, 1f, propsMat, env);
            Prop("cooler", new Vector3(-6.3f, 0f, -12.8f), 20f, 1f, propsMat, env);
            Prop("lifeguard_tower", new Vector3(-9.5f, 0f, 2f), 90f, 1f, propsMat, env);
            Prop("surfboard", new Vector3(-7.2f, 0f, 11.8f), 70f, 1f, propsMat, env);
            Prop("rock_a", new Vector3(-12.5f, 0f, -6f), 0f, 1.4f, propsMat, env);
            Prop("rock_b", new Vector3(-13.5f, 0f, 12.5f), 40f, 1.8f, propsMat, env);
            Prop("rock_a", new Vector3(10f, 0f, -15f), 120f, 1.0f, propsMat, env);
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    Prop("tiki_torch", new Vector3(sx * 6.2f, 0f, sz * 10f), 0f, 1f, propsMat, env);
            var sandBall = Prop("beach_ball", new Vector3(-5.8f, 0.3f, -8.5f), 30f, 0.3f, propsMat, env);

            // --- the rally: fox spikes over the net at a leaping giraffe
            var cast = new GameObject("Animals").transform;
            Animal("fox", JerseyA, new Vector3(0.6f, 0.75f, -1.3f), 0f, "Spike", 0.30f, animalMat, cast);
            Animal("bear", JerseyA2, new Vector3(-2.2f, 0f, -5.6f), 15f, "Bump", 0.2f, animalMat, cast);
            Animal("giraffe", JerseyB, new Vector3(0.3f, 0.35f, 0.9f), 180f, "Block", 0.25f, animalMat, cast);
            Animal("penguin", JerseyB2, new Vector3(-1.6f, 0f, 5.0f), 200f, "Set", 0.2f, animalMat, cast);
            var ball = Prop("beach_ball", new Vector3(0.9f, 3.35f, -0.7f), 10f, 0.3f, propsMat, null);
            ball.name = "Ball";

            // --- style controller
            var ld = new GameObject("LookDev").AddComponent<LookDevStyle>();
            ld.sun = sun;
            ld.cam = cam;
            ld.skybox = sky;
            ld.shots = new[]
            {
                new LookDevStyle.Shot { label = "Broadcast (current game camera)", position = new Vector3(20f, 12f, -3f), lookAt = new Vector3(0f, 1.6f, 0f), fov = 36f },
                new LookDevStyle.Shot { label = "Close-up", position = new Vector3(7.5f, 2.2f, -5.0f), lookAt = new Vector3(0f, 1.5f, -0.8f), fov = 42f },
                new LookDevStyle.Shot { label = "Character portrait", position = new Vector3(3.6f, 1.9f, -3.4f), lookAt = new Vector3(0.3f, 1.55f, -0.2f), fov = 34f },
                new LookDevStyle.Shot { label = "Behind the team", position = new Vector3(1.5f, 4.2f, -16.5f), lookAt = new Vector3(0f, 1.5f, 1f), fov = 46f },
            };
            ld.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Volleyball] Look-dev scene saved to " + ScenePath);
        }

        [MenuItem("Volleyball/3D/Save Look-Dev Contact Sheet", priority = 202)]
        public static void SaveContactSheet()
        {
            if (!File.Exists(ScenePath)) Build();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var ld = Object.FindFirstObjectByType<LookDevStyle>();
            foreach (var c in Object.FindObjectsByType<LookDevAnimalCycler>(FindObjectsSortMode.None)) c.Pose();
            foreach (var a in Object.FindObjectsByType<AnimalLook>(FindObjectsSortMode.None)) a.Apply();

            const int W = 800, H = 450;
            int cols = 2, rows = (ld.shots.Length + 1) / 2;
            var sheet = new Texture2D(W * cols, H * rows, TextureFormat.RGB24, false);
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var grab = new Texture2D(W, H, TextureFormat.RGB24, false);

            for (int i = 0; i < ld.shots.Length; i++)
            {
                ld.shot = i;
                ld.Apply();
                PushAmbientProbe(ld.preset);
                ld.cam.targetTexture = rt;
                ld.cam.Render();
                RenderTexture.active = rt;
                grab.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                grab.Apply();
                sheet.SetPixels((i % cols) * W, (rows - 1 - i / cols) * H, W, H, grab.GetPixels());
            }
            RenderTexture.active = null;
            ld.cam.targetTexture = null;
            sheet.Apply();

            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "lookdev_sheet.png");
            byte[] png = sheet.EncodeToPNG();
            try { File.WriteAllBytes(path, png); }
            catch (IOException) // open in an image viewer — don't lose the render
            {
                path = path.Replace(".png", $"_{System.DateTime.Now:HHmmss}.png");
                File.WriteAllBytes(path, png);
            }
            Object.DestroyImmediate(sheet);
            Object.DestroyImmediate(grab);
            rt.Release();
            Debug.Log($"[Volleyball] Look-dev contact sheet ({ld.shots.Length} shots) saved to {path}");
        }

        /// <summary>Headless entry point: build the scene, then render the sheet.</summary>
        public static void BuildAndRender()
        {
            Build();
            SaveContactSheet();
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Batch mode doesn't re-bake the ambient probe from trilight colours, so do it by hand.</summary>
        static void PushAmbientProbe(LookDevStyle.Preset p)
        {
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(p.ambientEquator * 0.55f);
            sh.AddDirectionalLight(Vector3.up, p.ambientSky, 0.55f);
            sh.AddDirectionalLight(Vector3.down, p.ambientGround, 0.45f);
            RenderSettings.ambientProbe = sh;
        }

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

        static GameObject Instantiate(string assetPath, Transform parent)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (model == null)
            {
                Debug.LogError("[Volleyball] Missing generated model " + assetPath +
                               " — run Tools/blender/*_gen.py export first.");
                return new GameObject(Path.GetFileNameWithoutExtension(assetPath) + " (missing)");
            }
            // The model root carries the importer's axis-conversion rotation, so placement goes on
            // a wrapper — writing the root's rotation would tip Blender's Z-up meshes over.
            var holder = new GameObject(model.name).transform;
            if (parent != null) holder.SetParent(parent, false);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.transform.SetParent(holder, false);
            return holder.gameObject;
        }

        static GameObject Prop(string name, Vector3 pos, float yaw, float scale, Material mat, Transform parent)
        {
            var go = Instantiate($"{PropDir}/prop_{name}.fbx", parent);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = name == "terrain" || name == "ocean" ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }
            return go;
        }

        static void Animal(string id, Color jersey, Vector3 pos, float yaw, string clipName, float poseTime,
                           Material mat, Transform parent)
        {
            var holder = new GameObject(id).transform;
            holder.SetParent(parent, false);
            holder.localPosition = pos;
            holder.localRotation = Quaternion.Euler(0f, yaw + ModelFacingYaw, 0f);

            string path = $"{CharDir}/animal_{id}.fbx";
            var variant = Instantiate(path, holder);
            if (variant.transform.childCount == 0) return; // missing model, already logged
            var go = variant.transform.GetChild(0).gameObject; // the imported model root owns the Animator
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = mat;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }

            var look = go.AddComponent<AnimalLook>();
            look.characterId = id;
            look.jersey = jersey;

            if (go.GetComponent<Animator>() == null) go.AddComponent<Animator>();
            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToArray();
            var cycler = go.AddComponent<LookDevAnimalCycler>();
            cycler.clips = clips;
            cycler.poseClip = System.Array.FindIndex(clips, c => c.name == clipName);
            if (cycler.poseClip < 0) cycler.poseClip = 0;
            cycler.poseTime = poseTime;
            cycler.Pose();
            look.Apply();
        }
    }
}
