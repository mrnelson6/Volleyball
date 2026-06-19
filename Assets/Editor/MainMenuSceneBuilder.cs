using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// Builds the front-end <c>MainMenu.unity</c> scene from code, mirroring the other builders
    /// (<see cref="PrototypeSceneBuilder"/>, <see cref="VolleyballLevelBuilder"/>). The menu uses
    /// the live sunset beach arena as its backdrop and overlays a title, the top-level buttons
    /// (Quick Play / Campaign / Settings / Quit) and two hidden panels (Settings, Campaign).
    ///
    /// It also sets Build Settings so the menu is scene index 0 — the game boots into the menu.
    /// </summary>
    public static class MainMenuSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/MainMenu.unity";
        const string ArenaScenePath = "Assets/Scenes/BeachArena.unity";
        const string GameScenePath = "Assets/Scenes/Game.unity";

        [MenuItem("Volleyball/Build Main Menu Scene", priority = 0)]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Live beach arena backdrop (environment + camera + sun) plus the visual court
            // (sand ground, lines, net) so it matches the playable arena. No ball/players/match —
            // it's purely scenic.
            ArenaDecorator.BuildSunsetBeachArena();
            CourtKit.BuildCourtVisual();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject canvasGO = BuildCanvas();

            // Title
            Text title = MakeText(canvasGO.transform, "Title", font,
                new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1400f, 160f), 96,
                TextAnchor.MiddleCenter);
            title.text = "BEACH VOLLEYBALL";

            // Top-level buttons (lower-centre stack)
            Button quickPlay = MakeButton(canvasGO.transform, font, "QuickPlayButton", "Quick Play",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), MenuBtnSize, MenuBlue);
            Button campaign = MakeButton(canvasGO.transform, font, "CampaignButton", "Campaign",
                new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), MenuBtnSize, MenuBlue);
            Button settings = MakeButton(canvasGO.transform, font, "SettingsButton", "Settings",
                new Vector2(0.5f, 0.5f), new Vector2(0f, -180f), MenuBtnSize, MenuBlue);
            Button quit = MakeButton(canvasGO.transform, font, "QuitButton", "Quit",
                new Vector2(0.5f, 0.5f), new Vector2(0f, -300f), MenuBtnSize, MenuRed);

            GameObject settingsPanel = BuildSettingsPanel(canvasGO.transform, font);
            GameObject campaignPanel = BuildCampaignPanel(canvasGO.transform, font);

            var ctrl = canvasGO.AddComponent<MainMenuController>();
            ctrl.quickPlayButton = quickPlay;
            ctrl.campaignButton = campaign;
            ctrl.settingsButton = settings;
            ctrl.quitButton = quit;
            ctrl.settingsPanel = settingsPanel;
            ctrl.campaignPanel = campaignPanel;

            BuildEventSystem();

            Directory.CreateDirectory(Path.GetDirectoryName(AbsPath(ScenePath)));
            EditorSceneManager.SaveScene(scene, ScenePath);
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath);

            Debug.Log("[Volleyball] Main menu scene built at " + ScenePath + " (set as scene 0).");
            EditorUtility.DisplayDialog("Volleyball",
                "Main menu built and opened.\nIt is now scene 0, so the game boots into the menu.\n\n" +
                "Re-run 'Build Sunset Beach Arena Scene' so Quick Play's arena picks up the pause menu.",
                "OK");
        }

        // ----------------------------------------------------------------- palette / sizes

        static readonly Vector2 MenuBtnSize = new Vector2(440f, 96f);
        static readonly Color MenuBlue = new Color(0.20f, 0.45f, 0.85f, 0.92f);
        static readonly Color MenuRed = new Color(0.80f, 0.32f, 0.28f, 0.92f);
        static readonly Color PanelDim = new Color(0.04f, 0.06f, 0.10f, 0.88f);

        static readonly (AudioChannel ch, string label)[] VolumeRows =
        {
            (AudioChannel.Master, "Master"),
            (AudioChannel.Sfx, "Effects"),
            (AudioChannel.Ambient, "Ambient"),
            (AudioChannel.Movement, "Movement"),
            (AudioChannel.Crowd, "Crowd"),
        };

        // ----------------------------------------------------------------- panels

        static GameObject BuildSettingsPanel(Transform parent, Font font)
        {
            GameObject panel = MakeDimPanel(parent, "SettingsPanel");
            var sp = panel.AddComponent<SettingsPanel>();

            Text title = MakeText(panel.transform, "Title", font,
                new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(800f, 100f), 72,
                TextAnchor.MiddleCenter);
            title.text = "Settings";

            var rows = new List<SettingsPanel.Row>();
            float y = 170f;
            foreach (var (ch, label) in VolumeRows)
            {
                Text name = MakeText(panel.transform, label + " Label", font,
                    new Vector2(0.5f, 0.5f), new Vector2(-430f, y), new Vector2(300f, 50f), 34,
                    TextAnchor.MiddleRight);
                name.text = label;

                Slider slider = MakeSlider(panel.transform, label + " Slider",
                    new Vector2(0.5f, 0.5f), new Vector2(40f, y), new Vector2(480f, 36f));

                Text value = MakeText(panel.transform, label + " Value", font,
                    new Vector2(0.5f, 0.5f), new Vector2(360f, y), new Vector2(120f, 50f), 34,
                    TextAnchor.MiddleLeft);
                value.text = "";

                rows.Add(new SettingsPanel.Row { channel = ch, slider = slider, valueLabel = value });
                y -= 90f;
            }
            sp.rows = rows.ToArray();

            sp.backButton = MakeButton(panel.transform, font, "BackButton", "Back",
                new Vector2(0.5f, 0.5f), new Vector2(0f, y - 30f), new Vector2(300f, 80f), MenuRed);

            panel.SetActive(false);
            return panel;
        }

        static GameObject BuildCampaignPanel(Transform parent, Font font)
        {
            GameObject panel = MakeDimPanel(parent, "CampaignPanel");
            var cp = panel.AddComponent<CampaignPanel>();

            Text title = MakeText(panel.transform, "Title", font,
                new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(800f, 100f), 72,
                TextAnchor.MiddleCenter);
            title.text = "Campaign";

            cp.statusLabel = MakeText(panel.transform, "Status", font,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(1000f, 70f), 34,
                TextAnchor.MiddleCenter);
            cp.statusLabel.text = "";

            cp.newGameButton = MakeButton(panel.transform, font, "NewGameButton", "New Game",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(440f, 90f), MenuBlue);
            cp.continueButton = MakeButton(panel.transform, font, "ContinueButton", "Continue",
                new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(440f, 90f), MenuBlue);
            cp.backButton = MakeButton(panel.transform, font, "BackButton", "Back",
                new Vector2(0.5f, 0.5f), new Vector2(0f, -200f), new Vector2(300f, 80f), MenuRed);

            panel.SetActive(false);
            return panel;
        }

        static GameObject MakeDimPanel(Transform parent, string name)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Stretch(panel.GetComponent<RectTransform>());
            panel.GetComponent<Image>().color = PanelDim; // also blocks clicks to the menu behind
            return panel;
        }

        // ----------------------------------------------------------------- UI primitives

        static GameObject BuildCanvas()
        {
            var canvasGO = new GameObject("Menu Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvasGO;
        }

        static Button MakeButton(Transform parent, Font font, string name, string label,
                                 Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            var img = go.GetComponent<Image>();
            img.sprite = UISprite();
            img.type = Image.Type.Sliced;
            img.color = color;
            go.GetComponent<Button>().targetGraphic = img;

            Text t = MakeText(go.transform, "Label", font,
                new Vector2(0.5f, 0.5f), Vector2.zero, size, 36, TextAnchor.MiddleCenter);
            t.text = label;
            t.raycastTarget = false;
            return go.GetComponent<Button>();
        }

        static Slider MakeSlider(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            // Background
            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(go.transform, false);
            Stretch(bg.GetComponent<RectTransform>());
            var bgImg = bg.GetComponent<Image>();
            bgImg.sprite = UIBackground();
            bgImg.type = Image.Type.Sliced;
            bgImg.color = new Color(0.10f, 0.12f, 0.16f, 0.95f);

            // Fill Area > Fill
            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = new Vector2(0f, 0.25f);
            faRt.anchorMax = new Vector2(1f, 0.75f);
            faRt.offsetMin = new Vector2(8f, 0f);
            faRt.offsetMax = new Vector2(-8f, 0f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.sizeDelta = new Vector2(16f, 0f);
            var fillImg = fill.GetComponent<Image>();
            fillImg.sprite = UISprite();
            fillImg.type = Image.Type.Sliced;
            fillImg.color = new Color(0.30f, 0.65f, 1f, 1f);

            // Handle Slide Area > Handle
            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            var haRt = handleArea.GetComponent<RectTransform>();
            haRt.anchorMin = Vector2.zero;
            haRt.anchorMax = Vector2.one;
            haRt.offsetMin = new Vector2(10f, 0f);
            haRt.offsetMax = new Vector2(-10f, 0f);

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            var hRt = handle.GetComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(34f, 34f);
            handle.GetComponent<Image>().sprite = UIKnob();

            var slider = go.GetComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = hRt;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            return slider;
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
            if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        // Built-in Unity UI sprites (always available) keep the menu self-contained.
        static Sprite UISprite() => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        static Sprite UIBackground() => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        static Sprite UIKnob() => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        // ----------------------------------------------------------------- build settings

        static void ConfigureBuildSettings()
        {
            // Menu first (index 0), then the playable scenes that exist on disk.
            var ordered = new List<string> { ScenePath };
            foreach (var p in new[] { ArenaScenePath, GameScenePath })
                if (File.Exists(AbsPath(p))) ordered.Add(p);

            // Preserve any other already-registered scenes after ours.
            foreach (var s in EditorBuildSettings.scenes)
                if (!ordered.Contains(s.path)) ordered.Add(s.path);

            var scenes = new List<EditorBuildSettingsScene>();
            foreach (var p in ordered)
                scenes.Add(new EditorBuildSettingsScene(p, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static string AbsPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
