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
            GameObject characterSelectPanel = BuildCharacterSelectPanel(canvasGO.transform, font);

            var ctrl = canvasGO.AddComponent<MainMenuController>();
            ctrl.quickPlayButton = quickPlay;
            ctrl.campaignButton = campaign;
            ctrl.settingsButton = settings;
            ctrl.quitButton = quit;
            ctrl.settingsPanel = settingsPanel;
            ctrl.campaignPanel = campaignPanel;
            ctrl.characterSelectPanel = characterSelectPanel;

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

        /// <summary>
        /// The Quick Play character-select screen: an entry per roster character (baked idle
        /// portrait + name) in a 4×2 grid on the left, and a preview pane on the right — big
        /// portrait, name, blurb and height/speed/control stat bars — plus Play and Back.
        /// Portrait sprites are the human-blue baked idle frames, assigned at build time; the
        /// live selection logic is <see cref="CharacterSelectPanel"/>.
        /// </summary>
        static GameObject BuildCharacterSelectPanel(Transform parent, Font font)
        {
            GameObject panel = MakeDimPanel(parent, "CharacterSelectPanel");
            var cs = panel.AddComponent<CharacterSelectPanel>();

            Text title = MakeText(panel.transform, "Title", font,
                new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(1200f, 100f), 72,
                TextAnchor.MiddleCenter);
            title.text = "Choose Your Character";

            // ---- roster grid (left) ----
            var entries = new List<CharacterSelectPanel.Entry>();
            for (int i = 0; i < CharacterRoster.All.Length; i++)
            {
                CharacterDef ch = CharacterRoster.All[i];
                var pos = new Vector2(-810f + (i % 4) * 190f, i < 4 ? 140f : -100f);

                var go = new GameObject(ch.displayName + " Entry",
                    typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(panel.transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(180f, 220f);
                rt.anchoredPosition = pos;

                var frame = go.GetComponent<Image>();
                frame.sprite = UISprite();
                frame.type = Image.Type.Sliced;
                frame.color = new Color(1f, 1f, 1f, 0.10f); // panel re-tints on selection
                go.GetComponent<Button>().targetGraphic = frame;

                var portraitGO = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                portraitGO.transform.SetParent(go.transform, false);
                var prt = portraitGO.GetComponent<RectTransform>();
                prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
                prt.sizeDelta = new Vector2(150f, 160f);
                prt.anchoredPosition = new Vector2(0f, 22f);
                var portrait = portraitGO.GetComponent<Image>();
                portrait.sprite = CharacterArt.GetCharacterFrames(PlayerColors.Human, ch)[0]; // idle
                portrait.preserveAspect = true; // heights differ per character — don't stretch
                portrait.raycastTarget = false;

                Text nameLabel = MakeText(go.transform, "Name", font,
                    new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(180f, 40f), 28,
                    TextAnchor.MiddleCenter);
                nameLabel.text = ch.displayName;
                nameLabel.raycastTarget = false;

                entries.Add(new CharacterSelectPanel.Entry
                {
                    characterId = ch.id,
                    button = go.GetComponent<Button>(),
                    frame = frame,
                    portrait = portrait,
                });
            }
            cs.entries = entries.ToArray();

            // ---- preview pane (right) ----
            cs.previewName = MakeText(panel.transform, "PreviewName", font,
                new Vector2(0.5f, 0.5f), new Vector2(430f, 320f), new Vector2(500f, 60f), 52,
                TextAnchor.MiddleCenter);

            var previewGO = new GameObject("PreviewPortrait", typeof(RectTransform), typeof(Image));
            previewGO.transform.SetParent(panel.transform, false);
            var pvRt = previewGO.GetComponent<RectTransform>();
            pvRt.anchorMin = pvRt.anchorMax = pvRt.pivot = new Vector2(0.5f, 0.5f);
            pvRt.sizeDelta = new Vector2(250f, 330f);
            pvRt.anchoredPosition = new Vector2(430f, 120f);
            cs.previewPortrait = previewGO.GetComponent<Image>();
            cs.previewPortrait.preserveAspect = true; // sprite comes from the selected entry

            cs.previewBlurb = MakeText(panel.transform, "PreviewBlurb", font,
                new Vector2(0.5f, 0.5f), new Vector2(430f, -80f), new Vector2(680f, 60f), 26,
                TextAnchor.MiddleCenter);

            cs.heightBar = BuildStatBar(panel.transform, font, "Height", -150f);
            cs.speedBar = BuildStatBar(panel.transform, font, "Speed", -210f);
            cs.controlBar = BuildStatBar(panel.transform, font, "Control", -270f);

            cs.playButton = MakeButton(panel.transform, font, "PlayButton", "Play",
                new Vector2(0.5f, 0.5f), new Vector2(430f, -370f), new Vector2(360f, 90f), MenuBlue);
            cs.backButton = MakeButton(panel.transform, font, "BackButton", "Back",
                new Vector2(0.5f, 0.5f), new Vector2(-525f, -370f), new Vector2(300f, 80f), MenuRed);

            panel.SetActive(false);
            return panel;
        }

        // One preview stat row: right-aligned label, a bar whose fill the panel resizes, and a
        // value label ("×1.16"). Returns the pieces CharacterSelectPanel drives at runtime.
        static CharacterSelectPanel.StatBar BuildStatBar(Transform parent, Font font,
                                                         string label, float y)
        {
            Text name = MakeText(parent, label + " Label", font,
                new Vector2(0.5f, 0.5f), new Vector2(190f, y), new Vector2(180f, 40f), 30,
                TextAnchor.MiddleRight);
            name.text = label;

            var bg = new GameObject(label + " Bar", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(parent, false);
            var rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(330f, 28f);
            rt.anchoredPosition = new Vector2(475f, y);
            var bgImg = bg.GetComponent<Image>();
            bgImg.sprite = UIBackground();
            bgImg.type = Image.Type.Sliced;
            bgImg.color = new Color(0.10f, 0.12f, 0.16f, 0.95f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(bg.transform, false);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0.5f, 1f); // panel sets the real fraction
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var fillImg = fill.GetComponent<Image>();
            fillImg.sprite = UISprite();
            fillImg.type = Image.Type.Sliced;
            fillImg.color = new Color(0.30f, 0.65f, 1f, 1f);

            Text value = MakeText(parent, label + " Value", font,
                new Vector2(0.5f, 0.5f), new Vector2(665f, y), new Vector2(120f, 40f), 28,
                TextAnchor.MiddleLeft);
            value.text = "";

            return new CharacterSelectPanel.StatBar { fill = fillRt, valueLabel = value };
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

        // Delegates to CourtKit so the (version-fragile) UI input wiring lives in one place.
        static void BuildEventSystem() => CourtKit.EnsureEventSystem();

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
