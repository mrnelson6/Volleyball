using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// Builds the entire playable prototype scene from code so it can be assembled without
    /// hand-wiring GameObjects in the editor. Run via the "Volleyball" menu.
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Game.unity";
        const string SpritePath = "Assets/Sprites/circle.png";
        const string BeachBallPath = "Assets/Sprites/beachball.png";
        const string MaterialDir = "Assets/Materials";

        // placeholder team colours
        static readonly Color ColPlayer = new Color(0.20f, 0.50f, 0.95f); // human  (blue)
        static readonly Color ColMate = new Color(0.45f, 0.80f, 1.00f);   // teammate (cyan)
        static readonly Color ColOpp1 = new Color(0.95f, 0.30f, 0.25f);   // opponent (red)
        static readonly Color ColOpp2 = new Color(0.98f, 0.60f, 0.20f);   // opponent (orange)

        [MenuItem("Volleyball/Build Prototype Scene")]
        public static void Build()
        {
            EnsureFolders();
            GameConfigCreator.EnsureExists(); // make the global tuning asset if it's missing
            Sprite circle = GetCircleSprite();
            Sprite beachBall = GetBeachBallSprite();
            Material sand = MakeUnlitMaterial("Sand", new Color(0.93f, 0.85f, 0.62f));
            Material line = MakeUnlitMaterial("Line", Color.white);
            Material netMat = MakeUnlitMaterial("Net", new Color(0.9f, 0.9f, 0.9f));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera();
            BuildLight();
            BuildCourt(sand, line, netMat);

            BallController ball = BuildBall(beachBall, circle);

            var players = new List<VolleyPlayer>();
            // team A — human listed first so it serves for team A
            players.Add(MakePlayer("Player (You)", TeamSide.A, -1f, ColPlayer, true, circle));
            players.Add(MakePlayer("Teammate (AI)", TeamSide.A, 1f, ColMate, false, circle));
            // team B — opponents
            players.Add(MakePlayer("Opponent 1 (AI)", TeamSide.B, -1f, ColOpp1, false, circle));
            players.Add(MakePlayer("Opponent 2 (AI)", TeamSide.B, 1f, ColOpp2, false, circle));

            new GameObject("GameInput", typeof(GameInput));

            var match = new GameObject("MatchManager").AddComponent<MatchManager>();
            match.ball = ball;
            match.players = players;

            BuildUI(match, circle);
            BuildEventSystem();

            Directory.CreateDirectory(Path.GetDirectoryName(AbsPath(ScenePath)));
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            ConfigurePlayerSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath);

            Debug.Log("[Volleyball] Prototype scene built at " + ScenePath + ". Press Play.");
            EditorUtility.DisplayDialog("Volleyball",
                "Prototype scene built and opened.\nPress Play to test.", "OK");
        }

        // ----------------------------------------------------------------- court

        static void BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.45f, 0.75f, 0.95f);
            cam.fieldOfView = 36f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            go.AddComponent<AudioListener>();
            // Side-of-the-net broadcast view: off to one sideline, elevated, looking across
            // so the net is centred and the court's depth/length reads left-to-right.
            go.transform.position = new Vector3(20f, 12f, -3f);
            go.transform.LookAt(new Vector3(0f, 1.6f, 0f));
        }

        static void BuildLight()
        {
            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = Color.white;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        static void BuildCourt(Material sand, Material line, Material netMat)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(3.2f, 1f, 4.4f); // plane is 10u → ~32 x 44
            ground.transform.position = Vector3.zero;
            ground.GetComponent<MeshRenderer>().sharedMaterial = sand;
            ground.AddComponent<GroundMarker>();

            float w = CourtGeometry.HalfWidth;
            float d = CourtGeometry.HalfDepth;
            MakeLine("Sideline -X", new Vector3(-w, 0.02f, 0f), new Vector3(0.1f, 0.02f, d * 2f), line);
            MakeLine("Sideline +X", new Vector3(w, 0.02f, 0f), new Vector3(0.1f, 0.02f, d * 2f), line);
            MakeLine("Baseline A", new Vector3(0f, 0.02f, -d), new Vector3(w * 2f, 0.02f, 0.1f), line);
            MakeLine("Baseline B", new Vector3(0f, 0.02f, d), new Vector3(w * 2f, 0.02f, 0.1f), line);
            MakeLine("Net Line", new Vector3(0f, 0.02f, 0f), new Vector3(w * 2f, 0.02f, 0.1f), line);

            var net = GameObject.CreatePrimitive(PrimitiveType.Cube);
            net.name = "Net";
            net.transform.position = new Vector3(0f, CourtGeometry.NetHeight * 0.5f, 0f);
            net.transform.localScale = new Vector3(w * 2f + 1f, CourtGeometry.NetHeight, 0.1f);
            net.GetComponent<MeshRenderer>().sharedMaterial = netMat;
        }

        static void MakeLine(string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // lines never collide
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // ----------------------------------------------------------------- ball / players

        static BallController BuildBall(Sprite ballSprite, Sprite shadowSprite)
        {
            var go = new GameObject("Ball");
            go.transform.position = new Vector3(0f, 3f, -4f);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.3f;
            rb.useGravity = true;

            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.3f;

            var spr = new GameObject("Sprite");
            spr.transform.SetParent(go.transform, false);
            spr.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
            var sr = spr.AddComponent<SpriteRenderer>();
            sr.sprite = ballSprite;
            sr.color = Color.white;
            var bb = spr.AddComponent<BillboardSprite>();
            bb.yAxisOnly = false;

            var bc = go.AddComponent<BallController>();
            bb.spinSource = bc; // sprite rolls to show the ball's spin

            // ground shadow — flat (not billboarded), tracks the ball and scales with height
            var shadow = new GameObject("Ball Shadow");
            shadow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var ssr = shadow.AddComponent<SpriteRenderer>();
            ssr.sprite = shadowSprite;
            ssr.color = new Color(0f, 0f, 0f, 0.4f);
            var ds = shadow.AddComponent<DropShadow>();
            ds.target = go.transform;
            ds.baseSize = 0.75f;
            ds.maxHeight = 6f;

            return bc;
        }

        static VolleyPlayer MakePlayer(string name, TeamSide team, float halfSign,
                                       Color color, bool human, Sprite circle)
        {
            var root = new GameObject(name);

            var spr = new GameObject("Sprite");
            spr.transform.SetParent(root.transform, false);
            spr.transform.localPosition = CharacterArt.SpriteLocalPos;
            var sr = spr.AddComponent<SpriteRenderer>();
            spr.AddComponent<BillboardSprite>().yAxisOnly = true;
            // procedural human figure (idle/run/jump/swing) baked in this player's team colour
            CharacterArt.AttachCharacter(spr, sr, color);

            VolleyPlayer vp = human
                ? root.AddComponent<PlayerController>()
                : root.AddComponent<AIController>();
            vp.team = team;
            vp.halfSign = halfSign;

            float x = halfSign * CourtGeometry.HalfWidth * 0.45f;
            float z = CourtGeometry.SideSign(team) * CourtGeometry.HalfDepth * 0.55f;
            root.transform.position = new Vector3(x, 0f, z);

            // ground shadow that shrinks as the player jumps
            var shadow = new GameObject(name + " Shadow");
            shadow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var ssr = shadow.AddComponent<SpriteRenderer>();
            ssr.sprite = circle;
            ssr.color = new Color(0f, 0f, 0f, 0.32f);
            var ds = shadow.AddComponent<DropShadow>();
            ds.target = root.transform;
            ds.baseSize = 1.0f;
            ds.maxHeight = 2.4f;

            return vp;
        }

        // ----------------------------------------------------------------- UI

        static void BuildUI(MatchManager match, Sprite circle)
        {
            var canvasGO = new GameObject("HUD Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Text score = MakeText(canvasGO.transform, "Score", font,
                new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(800f, 80f), 48,
                TextAnchor.UpperCenter);
            Text banner = MakeText(canvasGO.transform, "Banner", font,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(1200f, 100f), 54,
                TextAnchor.MiddleCenter);

            var hud = canvasGO.AddComponent<ScoreHUD>();
            hud.match = match;
            hud.scoreText = score;
            hud.bannerText = banner;

            // touch controls (auto-hidden on non-touch platforms)
            var panel = new GameObject("TouchPanel", typeof(RectTransform));
            panel.transform.SetParent(canvasGO.transform, false);
            var prt = panel.GetComponent<RectTransform>();
            Stretch(prt);
            panel.AddComponent<SafeArea>();

            BuildJoystick(panel.transform, circle);
            // bottom-right action cluster
            BuildButton(panel.transform, circle, "JumpButton", "JUMP", VirtualButtonKind.Jump,
                new Vector2(1f, 0f), new Vector2(-330f, 360f), new Color(0.30f, 0.55f, 1f, 0.5f), font);
            BuildButton(panel.transform, circle, "SpikeButton", "SPIKE", VirtualButtonKind.Spike,
                new Vector2(1f, 0f), new Vector2(-150f, 360f), new Color(1f, 0.35f, 0.30f, 0.5f), font);
            BuildButton(panel.transform, circle, "BumpButton", "BUMP", VirtualButtonKind.Bump,
                new Vector2(1f, 0f), new Vector2(-330f, 180f), new Color(0.30f, 0.80f, 0.40f, 0.5f), font);
            BuildButton(panel.transform, circle, "SetButton", "SET", VirtualButtonKind.Set,
                new Vector2(1f, 0f), new Vector2(-150f, 180f), new Color(1f, 0.85f, 0.20f, 0.5f), font);

            var touch = canvasGO.AddComponent<TouchControls>();
            touch.panel = panel;
        }

        static void BuildJoystick(Transform parent, Sprite circle)
        {
            var go = new GameObject("Joystick", typeof(RectTransform), typeof(Image), typeof(VirtualJoystick));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(280f, 280f);
            rt.anchoredPosition = new Vector2(220f, 220f);
            var bg = go.GetComponent<Image>();
            bg.sprite = circle;
            bg.color = new Color(1f, 1f, 1f, 0.18f);

            var handleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGO.transform.SetParent(go.transform, false);
            var hrt = handleGO.GetComponent<RectTransform>();
            hrt.sizeDelta = new Vector2(120f, 120f);
            hrt.anchoredPosition = Vector2.zero;
            handleGO.GetComponent<Image>().sprite = circle;
            handleGO.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.5f);
            handleGO.GetComponent<Image>().raycastTarget = false;

            var joy = go.GetComponent<VirtualJoystick>();
            joy.background = rt;
            joy.handle = hrt;
            joy.radius = 110f;
        }

        static void BuildButton(Transform parent, Sprite circle, string name, string label,
                                 VirtualButtonKind kind, Vector2 anchor, Vector2 pos, Color color, Font font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VirtualButton));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(150f, 150f);
            rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            img.sprite = circle;
            img.color = color;
            go.GetComponent<VirtualButton>().kind = kind;

            Text t = MakeText(go.transform, "Label", font,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160f, 60f), 32, TextAnchor.MiddleCenter);
            t.text = label;
            t.raycastTarget = false;
        }

        static Text MakeText(Transform parent, string name, Font font, Vector2 anchor,
                             Vector2 pos, Vector2 size, int fontSize, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void BuildEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        // ----------------------------------------------------------------- assets

        static void EnsureFolders()
        {
            foreach (var dir in new[]
            {
                "Assets/Scenes", "Assets/Sprites", "Assets/Materials",
                "Assets/Prefabs", "Assets/Scripts"
            })
                Directory.CreateDirectory(AbsPath(dir));
            AssetDatabase.Refresh();
        }

        static Sprite GetCircleSprite()
        {
            if (!File.Exists(AbsPath(SpritePath)))
            {
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Vector2 c = new Vector2(size / 2f, size / 2f);
                float r = size / 2f - 1f;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                        tex.SetPixel(x, y, dist <= r ? Color.white : Color.clear);
                    }
                tex.Apply();
                File.WriteAllBytes(AbsPath(SpritePath), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);
            }

            var imp = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.spritePixelsPerUnit = 64f;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        static Sprite GetBeachBallSprite()
        {
            if (!File.Exists(AbsPath(BeachBallPath)))
            {
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Vector2 c = new Vector2(size / 2f, size / 2f);
                float r = size / 2f - 1f;

                Color yellow = new Color(1.00f, 0.83f, 0.10f);
                Color blue = new Color(0.12f, 0.40f, 0.85f);
                Color white = new Color(0.96f, 0.96f, 0.96f);
                Color[] palette = { yellow, blue, white };

                Vector2 highlight = new Vector2(c.x - r * 0.35f, c.y + r * 0.35f);

                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x + 0.5f - c.x;
                        float dy = y + 0.5f - c.y;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        if (dist > r) { tex.SetPixel(x, y, Color.clear); continue; }

                        float rn = dist / r;
                        float theta = Mathf.Atan2(dy, dx);            // -pi..pi
                        float swirl = theta / (2f * Mathf.PI) + 0.5f * rn; // pinwheel panels
                        swirl -= Mathf.Floor(swirl);
                        Color col = palette[Mathf.Clamp((int)(swirl * 3f), 0, 2)];

                        float shade = Mathf.Lerp(1f, 0.72f, rn);      // darken toward the rim (sphere look)
                        col *= shade;

                        float hd = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), highlight);
                        float hl = Mathf.Clamp01(1f - hd / (r * 0.6f)) * 0.3f; // soft sheen
                        col += new Color(hl, hl, hl, 0f);

                        col.a = 1f;
                        tex.SetPixel(x, y, col);
                    }

                tex.Apply();
                File.WriteAllBytes(AbsPath(BeachBallPath), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(BeachBallPath, ImportAssetOptions.ForceUpdate);
            }

            var imp = (TextureImporter)AssetImporter.GetAtPath(BeachBallPath);
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.spritePixelsPerUnit = 128f;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(BeachBallPath);
        }

        static Material MakeUnlitMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var mat = new Material(shader) { name = name };
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            string path = $"{MaterialDir}/{name}.mat";
            AssetDatabase.CreateAsset(mat, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        static void ConfigurePlayerSettings()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
        }

        static string AbsPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
