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


        static readonly Color JerseyA = new Color(0.20f, 0.50f, 0.95f);
        static readonly Color JerseyA2 = new Color(0.45f, 0.80f, 1.00f);
        static readonly Color JerseyB = new Color(0.95f, 0.30f, 0.25f);
        static readonly Color JerseyB2 = new Color(0.98f, 0.60f, 0.20f);

        [MenuItem("Volleyball/3D/Build Look-Dev Scene", priority = 201)]
        public static void Build()
        {
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 400f;
            camGo.AddComponent<AudioListener>();

            // same lighting + beach the toon Sunset Beach arena uses
            var env = new GameObject("Environment").transform;
            ToonArtKit.BuildLighting(env);
            ToonArtKit.BuildBeachEnvironment(env);

            // --- the rally: fox spikes over the net at a leaping giraffe
            var animalMat = ToonArtKit.AnimalMaterial();
            var cast = new GameObject("Animals").transform;
            Animal("fox", JerseyA, new Vector3(0.6f, 0.75f, -1.3f), 0f, "Spike", 0.30f, animalMat, cast);
            Animal("bear", JerseyA2, new Vector3(-2.2f, 0f, -5.6f), 15f, "Bump", 0.2f, animalMat, cast);
            Animal("giraffe", JerseyB, new Vector3(0.3f, 0.35f, 0.9f), 180f, "Block", 0.25f, animalMat, cast);
            Animal("penguin", JerseyB2, new Vector3(-1.6f, 0f, 5.0f), 200f, "Set", 0.2f, animalMat, cast);
            var ball = ToonArtKit.Prop("beach_ball", new Vector3(0.9f, 3.35f, -0.7f), 10f, 0.3f, ToonArtKit.PropsMaterial(), null);
            ball.name = "Ball";

            // --- camera shots
            var ld = new GameObject("LookDev").AddComponent<LookDevStyle>();
            ld.cam = cam;
            ld.shots = new[]
            {
                new LookDevStyle.Shot { label = "Broadcast (current game camera)", position = new Vector3(20f, 12f, -3f), lookAt = new Vector3(0f, 1.6f, 0f), fov = 36f },
                new LookDevStyle.Shot { label = "Close-up", position = new Vector3(7.5f, 2.2f, -5.0f), lookAt = new Vector3(0f, 1.5f, -0.8f), fov = 42f },
                new LookDevStyle.Shot { label = "Character portrait", position = new Vector3(3.6f, 1.9f, -3.4f), lookAt = new Vector3(0.3f, 1.55f, -0.2f), fov = 34f },
                new LookDevStyle.Shot { label = "Behind the team", position = new Vector3(1.5f, 4.2f, -16.5f), lookAt = new Vector3(0f, 1.5f, 1f), fov = 46f },
            };
            ld.ApplyShot();

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
            Object.FindFirstObjectByType<ToonEnvironment>()?.Apply();
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
                ld.ApplyShot();
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

        static void Animal(string id, Color jersey, Vector3 pos, float yaw, string clipName, float poseTime,
                           Material mat, Transform parent)
        {
            var holder = new GameObject(id).transform;
            holder.SetParent(parent, false);
            holder.localPosition = pos;
            holder.localRotation = Quaternion.Euler(0f, yaw + ToonArtKit.ModelFacingYaw, 0f);

            string path = $"{ToonArtKit.CharDir}/animal_{id}.fbx";
            var variant = ToonArtKit.InstantiateModel(path, holder);
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
