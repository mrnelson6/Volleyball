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

            // Home screen root: the title and top-level buttons live under one container so
            // MainMenuController can hide the whole home screen while a panel is open.
            var homeRoot = new GameObject("HomeRoot", typeof(RectTransform));
            homeRoot.transform.SetParent(canvasGO.transform, false);
            Stretch(homeRoot.GetComponent<RectTransform>());

            // Title
            Text title = MakeText(homeRoot.transform, "Title", font,
                new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(1500f, 130f), 96,
                TextAnchor.MiddleCenter);
            title.text = "ANIMAL VOLLEYBALL";

            Text subtitle = MakeText(homeRoot.transform, "Subtitle", font,
                new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(1000f, 60f), 40,
                TextAnchor.MiddleCenter);
            subtitle.text = "— WORLD TOUR —";
            subtitle.color = new Color(1f, 1f, 1f, 0.85f);

            // Top-level buttons (lower-centre stack)
            Button quickPlay = MakeButton(homeRoot.transform, font, "QuickPlayButton", "Quick Play",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), MenuBtnSize, MenuBlue);
            Button campaign = MakeButton(homeRoot.transform, font, "CampaignButton", "Campaign",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), MenuBtnSize, MenuBlue);
            Button online = MakeButton(homeRoot.transform, font, "OnlineButton", "Online",
                new Vector2(0.5f, 0.5f), new Vector2(0f, -100f), MenuBtnSize, MenuBlue);
            Button settings = MakeButton(homeRoot.transform, font, "SettingsButton", "Settings",
                new Vector2(0.5f, 0.5f), new Vector2(0f, -210f), MenuBtnSize, MenuBlue);
            Button quit = MakeButton(homeRoot.transform, font, "QuitButton", "Quit",
                new Vector2(0.5f, 0.5f), new Vector2(0f, -320f), MenuBtnSize, MenuRed);

            GameObject settingsPanel = BuildSettingsPanel(canvasGO.transform, font);
            GameObject campaignPanel = BuildCampaignPanel(canvasGO.transform, font);
            GameObject characterSelectPanel = BuildCharacterSelectPanel(canvasGO.transform, font);
            GameObject lobbyPanel = BuildOnlineLobbyPanel(canvasGO.transform, font);
            GameObject onlinePanel = BuildOnlinePanel(canvasGO.transform, font, lobbyPanel);

            var ctrl = canvasGO.AddComponent<MainMenuController>();
            ctrl.quickPlayButton = quickPlay;
            ctrl.campaignButton = campaign;
            ctrl.onlineButton = online;
            ctrl.settingsButton = settings;
            ctrl.quitButton = quit;
            ctrl.settingsPanel = settingsPanel;
            ctrl.campaignPanel = campaignPanel;
            ctrl.characterSelectPanel = characterSelectPanel;
            ctrl.onlinePanel = onlinePanel;
            ctrl.onlineLobbyPanel = lobbyPanel;
            ctrl.homeRoot = homeRoot;

            BuildEventSystem();

            // Dev-only online entry point (host/join + net stats); disables itself in
            // release builds and hands off to the real Online menu in Phase 2.
            new GameObject("NetworkDebugHUD", typeof(NetworkDebugHUD));

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

        /// <summary>
        /// The world-tour map: the baked <see cref="WorldMapArt"/> world with a pin per
        /// <see cref="RegionRoster"/> region at its <see cref="RegionDef.mapSpot"/>, a dotted
        /// travel path between consecutive stops, the protagonist fox as a travelling marker,
        /// an info line, and Play / New Game / Back. All state tinting, the travel animation
        /// and click handling live in <see cref="CampaignPanel"/>.
        /// </summary>
        static GameObject BuildCampaignPanel(Transform parent, Font font)
        {
            GameObject panel = MakeDimPanel(parent, "CampaignPanel");
            var cp = panel.AddComponent<CampaignPanel>();

            Text title = MakeText(panel.transform, "Title", font,
                new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(800f, 90f), 64,
                TextAnchor.MiddleCenter);
            title.text = "World Tour";

            cp.statusLabel = MakeText(panel.transform, "Status", font,
                new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(1200f, 44f), 30,
                TextAnchor.MiddleCenter);
            cp.statusLabel.text = "";

            // ---- the map itself ----
            Vector2 mapSize = new Vector2(1440f, 720f); // 2:1, same as the baked texture
            var mapGO = new GameObject("WorldMap", typeof(RectTransform), typeof(Image));
            mapGO.transform.SetParent(panel.transform, false);
            var mapRt = mapGO.GetComponent<RectTransform>();
            mapRt.anchorMin = mapRt.anchorMax = mapRt.pivot = new Vector2(0.5f, 0.5f);
            mapRt.sizeDelta = mapSize;
            mapRt.anchoredPosition = new Vector2(0f, 10f);
            var mapImg = mapGO.GetComponent<Image>();
            mapImg.sprite = WorldMapArt.GetSprite();
            mapImg.raycastTarget = false;

            // ---- dotted travel path (under the pins) ----
            var legs = new List<CampaignPanel.PathLeg>();
            for (int i = 0; i < RegionRoster.All.Length - 1; i++)
            {
                Vector2 a = SpotToMapPos(RegionRoster.All[i].mapSpot, mapSize);
                Vector2 b = SpotToMapPos(RegionRoster.All[i + 1].mapSpot, mapSize);
                int n = Mathf.Max(2, Mathf.RoundToInt(Vector2.Distance(a, b) / 34f) - 1);
                var dots = new List<Image>();
                for (int j = 1; j <= n; j++)
                {
                    var dot = new GameObject($"Leg{i}Dot{j}", typeof(RectTransform), typeof(Image));
                    dot.transform.SetParent(mapGO.transform, false);
                    var drt = dot.GetComponent<RectTransform>();
                    drt.anchorMin = drt.anchorMax = drt.pivot = new Vector2(0.5f, 0.5f);
                    drt.sizeDelta = new Vector2(10f, 10f);
                    drt.anchoredPosition = Vector2.Lerp(a, b, j / (n + 1f));
                    var dimg = dot.GetComponent<Image>();
                    dimg.sprite = UIKnob();
                    dimg.color = new Color(1f, 1f, 1f, 0.18f); // CampaignPanel re-tints by state
                    dimg.raycastTarget = false;
                    dots.Add(dimg);
                }
                legs.Add(new CampaignPanel.PathLeg { dots = dots.ToArray() });
            }
            cp.legs = legs.ToArray();

            // ---- region pins ----
            var pins = new List<CampaignPanel.MapPin>();
            for (int i = 0; i < RegionRoster.All.Length; i++)
            {
                RegionDef region = RegionRoster.All[i];
                var pinGO = new GameObject(region.displayName + " Pin",
                    typeof(RectTransform), typeof(Image), typeof(Button));
                pinGO.transform.SetParent(mapGO.transform, false);
                var prt = pinGO.GetComponent<RectTransform>();
                prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
                prt.sizeDelta = new Vector2(42f, 42f);
                prt.anchoredPosition = SpotToMapPos(region.mapSpot, mapSize);
                var pinImg = pinGO.GetComponent<Image>();
                pinImg.sprite = UIKnob();
                pinImg.color = new Color(0.70f, 0.70f, 0.70f, 0.45f); // panel re-tints by state
                pinGO.GetComponent<Button>().targetGraphic = pinImg;

                Text label = MakeText(pinGO.transform, "Label", font,
                    new Vector2(0.5f, 0.5f), PinLabelOffset(region.id), new Vector2(320f, 32f), 22,
                    TextAnchor.MiddleCenter);
                label.text = $"{i + 1}. {region.displayName}";
                label.raycastTarget = false;

                pins.Add(new CampaignPanel.MapPin
                {
                    regionId = region.id,
                    button = pinGO.GetComponent<Button>(),
                    pin = pinImg,
                    label = label,
                });
            }
            cp.pins = pins.ToArray();

            // ---- the travelling fox (last child of the map, so it draws over pins) ----
            CharacterDef protagonist = CharacterRoster.Get(CharacterRoster.ProtagonistId);
            var markerGO = new GameObject("TourMarker", typeof(RectTransform), typeof(Image));
            markerGO.transform.SetParent(mapGO.transform, false);
            var mrt = markerGO.GetComponent<RectTransform>();
            mrt.anchorMin = mrt.anchorMax = mrt.pivot = new Vector2(0.5f, 0.5f);
            mrt.sizeDelta = new Vector2(57f, 76f);
            var markerImg = markerGO.GetComponent<Image>();
            markerImg.sprite = CharacterArt.GetCharacterFrames(PlayerColors.Human, protagonist)[0];
            markerImg.preserveAspect = true;
            markerImg.raycastTarget = false;
            cp.marker = mrt;

            // ---- info line + buttons ----
            cp.infoLabel = MakeText(panel.transform, "Info", font,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -400f), new Vector2(1500f, 64f), 24,
                TextAnchor.MiddleCenter);
            cp.infoLabel.color = new Color(1f, 1f, 1f, 0.9f);

            cp.playButton = MakeButton(panel.transform, font, "PlayButton", "Play Next Match",
                new Vector2(0.5f, 0.5f), new Vector2(0f, -485f), new Vector2(520f, 90f), MenuBlue);
            cp.playButtonLabel = cp.playButton.GetComponentInChildren<Text>();
            cp.newGameButton = MakeButton(panel.transform, font, "NewGameButton", "New Game",
                new Vector2(0.5f, 0.5f), new Vector2(-640f, -485f), new Vector2(340f, 80f), MenuRed);
            cp.newGameButtonLabel = cp.newGameButton.GetComponentInChildren<Text>();
            cp.backButton = MakeButton(panel.transform, font, "BackButton", "Back",
                new Vector2(0.5f, 0.5f), new Vector2(640f, -485f), new Vector2(300f, 80f), MenuRed);

            panel.SetActive(false);
            return panel;
        }

        /// <summary>Normalized map UV → anchored position on the centred map rect.</summary>
        static Vector2 SpotToMapPos(Vector2 spot, Vector2 mapSize)
            => new Vector2((spot.x - 0.5f) * mapSize.x, (spot.y - 0.5f) * mapSize.y);

        /// <summary>Where each pin's name label sits relative to its pin — hand-placed so
        /// labels stay off the travel path and each other on the drawn continents.</summary>
        static Vector2 PinLabelOffset(string regionId)
        {
            switch (regionId)
            {
                case "himalaya":
                case "forest":
                case "sahara":
                case "rockies":
                case "arctic": return new Vector2(0f, 34f);   // label above the pin
                case "skyfinals": return new Vector2(0f, -40f);
                default: return new Vector2(0f, -34f);        // label below the pin
            }
        }

        /// <summary>
        /// The Quick Play character-select screen: an entry per roster animal (baked idle
        /// portrait + name) in a scrollable grid on the left, and a preview pane on the right —
        /// big portrait, name, blurb and height/speed/power/control/jump stat bars — plus Play
        /// and Back. Portrait sprites are the human-blue baked idle frames, assigned at build
        /// time; the live selection logic is <see cref="CharacterSelectPanel"/>.
        /// </summary>
        static GameObject BuildCharacterSelectPanel(Transform parent, Font font)
        {
            GameObject panel = MakeDimPanel(parent, "CharacterSelectPanel");
            var cs = panel.AddComponent<CharacterSelectPanel>();

            Text title = MakeText(panel.transform, "Title", font,
                new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(1200f, 100f), 72,
                TextAnchor.MiddleCenter);
            title.text = "Choose Your Animal";

            // ---- roster grid (left): a vertical scroll view, since the roster is far
            //      bigger than one screen ----
            var scrollGO = new GameObject("RosterScroll",
                typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            scrollGO.transform.SetParent(panel.transform, false);
            var scrollRt = scrollGO.GetComponent<RectTransform>();
            scrollRt.anchorMin = scrollRt.anchorMax = scrollRt.pivot = new Vector2(0.5f, 0.5f);
            scrollRt.sizeDelta = new Vector2(780f, 660f);
            scrollRt.anchoredPosition = new Vector2(-460f, -30f);
            var scrollBg = scrollGO.GetComponent<Image>();
            scrollBg.sprite = UIBackground();
            scrollBg.type = Image.Type.Sliced;
            scrollBg.color = new Color(0f, 0f, 0f, 0.25f); // subtle well; also catches drags

            var contentGO = new GameObject("Content",
                typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentGO.transform.SetParent(scrollGO.transform, false);
            var contentRt = contentGO.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;
            var grid = contentGO.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(180f, 220f);
            grid.spacing = new Vector2(10f, 10f);
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            var fitter = contentGO.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGO.GetComponent<ScrollRect>();
            scroll.content = contentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            var entries = new List<CharacterSelectPanel.Entry>();
            foreach (CharacterDef ch in CharacterRoster.All)
            {
                var go = new GameObject(ch.displayName + " Entry",
                    typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(contentGO.transform, false); // grid lays it out

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
                    new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(180f, 40f), 24,
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

            cs.heightBar = BuildStatBar(panel.transform, font, "Height", -140f);
            cs.speedBar = BuildStatBar(panel.transform, font, "Speed", -188f);
            cs.powerBar = BuildStatBar(panel.transform, font, "Power", -236f);
            cs.controlBar = BuildStatBar(panel.transform, font, "Control", -284f);
            cs.jumpBar = BuildStatBar(panel.transform, font, "Jump", -332f);

            cs.venueButton = MakeButton(panel.transform, font, "VenueButton", "Venue",
                new Vector2(0.5f, 0.5f), new Vector2(430f, -392f), new Vector2(520f, 54f),
                new Color(0.16f, 0.30f, 0.50f, 0.92f));
            cs.venueLabel = cs.venueButton.GetComponentInChildren<Text>();
            cs.venueLabel.fontSize = 26;

            cs.playButton = MakeButton(panel.transform, font, "PlayButton", "Play",
                new Vector2(0.5f, 0.5f), new Vector2(430f, -460f), new Vector2(360f, 80f), MenuBlue);
            cs.backButton = MakeButton(panel.transform, font, "BackButton", "Back",
                new Vector2(0.5f, 0.5f), new Vector2(-820f, -460f), new Vector2(300f, 80f), MenuRed);

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

        // ----------------------------------------------------------------- online panels

        static GameObject BuildOnlinePanel(Transform parent, Font font, GameObject lobbyPanel)
        {
            GameObject panel = MakeDimPanel(parent, "OnlinePanel");

            Text title = MakeText(panel.transform, "Title", font,
                new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(900f, 90f), 72,
                TextAnchor.MiddleCenter);
            title.text = "ONLINE";

            // Server Match first — the one-click path onto the dedicated box; hosting on
            // this machine is the fallback when the box is unreachable.
            Button serverMatch = MakeButton(panel.transform, font, "ServerMatchButton", "Server Match",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(520f, 110f),
                new Color(0.30f, 0.80f, 0.40f, 0.92f));

            Button host = MakeButton(panel.transform, font, "HostButton", "Host a Match",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(520f, 110f), MenuBlue);

            Text or = MakeText(panel.transform, "Or", font,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -15f), new Vector2(400f, 50f), 30,
                TextAnchor.MiddleCenter);
            or.text = "— or join a friend —";
            or.color = new Color(1f, 1f, 1f, 0.7f);

            InputField codeInput = MakeInputField(panel.transform, font, "CodeInput",
                "enter join code", new Vector2(0.5f, 0.5f), new Vector2(-110f, -100f),
                new Vector2(360f, 90f));
            Button join = MakeButton(panel.transform, font, "JoinButton", "Join",
                new Vector2(0.5f, 0.5f), new Vector2(190f, -100f), new Vector2(220f, 90f), MenuBlue);

            Text status = MakeText(panel.transform, "Status", font,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -200f), new Vector2(1200f, 50f), 28,
                TextAnchor.MiddleCenter);
            status.color = new Color(1f, 0.9f, 0.6f);

            Button back = MakeButton(panel.transform, font, "BackButton", "Back",
                new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(300f, 80f), MenuRed);

            var op = panel.AddComponent<OnlinePanel>();
            op.serverMatchButton = serverMatch;
            op.hostButton = host;
            op.joinButton = join;
            op.backButton = back;
            op.codeInput = codeInput;
            op.statusText = status;
            op.lobbyPanel = lobbyPanel;

            panel.SetActive(false);
            return panel;
        }

        static GameObject BuildOnlineLobbyPanel(Transform parent, Font font)
        {
            GameObject panel = MakeDimPanel(parent, "OnlineLobbyPanel");
            var lobby = panel.AddComponent<OnlineLobbyPanel>();

            Text code = MakeText(panel.transform, "CodeText", font,
                new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1200f, 70f), 54,
                TextAnchor.MiddleCenter);
            code.color = new Color(1f, 0.85f, 0.3f);
            lobby.codeText = code;

            // four slot cards: Team A column left, Team B column right
            string[] titles = { "TEAM A — LEFT", "TEAM A — RIGHT", "TEAM B — LEFT", "TEAM B — RIGHT" };
            lobby.cards = new OnlineLobbyPanel.SlotCard[4];
            for (int i = 0; i < 4; i++)
            {
                float x = i < 2 ? -420f : 420f;
                float y = i % 2 == 0 ? 150f : -140f;
                lobby.cards[i] = BuildSlotCard(panel.transform, font, i, titles[i], new Vector2(x, y));
            }

            // bottom bar: ready (guests) / arena + start (host) / leave
            Button ready = MakeButton(panel.transform, font, "ReadyButton", "READY",
                new Vector2(0.5f, 0f), new Vector2(-360f, 60f), new Vector2(280f, 90f),
                new Color(0.30f, 0.80f, 0.40f, 0.92f));
            lobby.readyButton = ready;
            lobby.readyButtonLabel = ready.GetComponentInChildren<Text>();

            Button arenaPrev = MakeButton(panel.transform, font, "ArenaPrev", "◀",
                new Vector2(0.5f, 0f), new Vector2(-160f, 60f), new Vector2(80f, 90f), MenuBlue);
            Text arenaName = MakeText(panel.transform, "ArenaName", font,
                new Vector2(0.5f, 0f), new Vector2(60f, 82f), new Vector2(340f, 50f), 30,
                TextAnchor.MiddleCenter);
            Button arenaNext = MakeButton(panel.transform, font, "ArenaNext", "▶",
                new Vector2(0.5f, 0f), new Vector2(280f, 60f), new Vector2(80f, 90f), MenuBlue);
            lobby.arenaPrevButton = arenaPrev;
            lobby.arenaText = arenaName;
            lobby.arenaNextButton = arenaNext;

            Button start = MakeButton(panel.transform, font, "StartButton", "START",
                new Vector2(0.5f, 0f), new Vector2(500f, 60f), new Vector2(280f, 90f),
                new Color(0.30f, 0.80f, 0.40f, 0.92f));
            lobby.startButton = start;

            Button leave = MakeButton(panel.transform, font, "LeaveButton", "Leave",
                new Vector2(0f, 0f), new Vector2(140f, 60f), new Vector2(220f, 80f), MenuRed);
            lobby.leaveButton = leave;

            Text status = MakeText(panel.transform, "Status", font,
                new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(1200f, 44f), 26,
                TextAnchor.MiddleCenter);
            status.color = new Color(1f, 1f, 1f, 0.8f);
            lobby.statusText = status;

            panel.SetActive(false);
            return panel;
        }

        static OnlineLobbyPanel.SlotCard BuildSlotCard(Transform parent, Font font, int index,
                                                       string title, Vector2 pos)
        {
            var card = new OnlineLobbyPanel.SlotCard();

            var go = new GameObject($"SlotCard{index}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(680f, 250f);
            rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            img.sprite = UIBackground();
            img.type = Image.Type.Sliced;
            img.color = new Color(0.10f, 0.14f, 0.20f, 0.92f);
            go.GetComponent<Button>().targetGraphic = img;
            card.claimButton = go.GetComponent<Button>();

            Text t = MakeText(go.transform, "Title", font,
                new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(640f, 40f), 26,
                TextAnchor.MiddleCenter);
            t.text = title;
            t.color = new Color(1f, 1f, 1f, 0.7f);
            t.raycastTarget = false;
            card.title = t;

            var portraitGO = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGO.transform.SetParent(go.transform, false);
            var prt = portraitGO.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 0.5f);
            prt.pivot = new Vector2(0f, 0.5f);
            prt.sizeDelta = new Vector2(150f, 150f);
            prt.anchoredPosition = new Vector2(30f, -16f);
            portraitGO.GetComponent<Image>().raycastTarget = false;
            card.portrait = portraitGO.GetComponent<Image>();

            Text occupant = MakeText(go.transform, "Occupant", font,
                new Vector2(0.5f, 0.5f), new Vector2(80f, 20f), new Vector2(400f, 44f), 32,
                TextAnchor.MiddleCenter);
            occupant.raycastTarget = false;
            card.occupantText = occupant;

            Text character = MakeText(go.transform, "Character", font,
                new Vector2(0.5f, 0.5f), new Vector2(80f, -40f), new Vector2(400f, 40f), 28,
                TextAnchor.MiddleCenter);
            character.color = new Color(1f, 0.85f, 0.3f);
            character.raycastTarget = false;
            card.characterText = character;

            card.prevCharButton = MakeButton(go.transform, font, "PrevChar", "◀",
                new Vector2(1f, 0f), new Vector2(-160f, 46f), new Vector2(70f, 70f),
                new Color(0.25f, 0.45f, 0.75f, 0.9f));
            card.nextCharButton = MakeButton(go.transform, font, "NextChar", "▶",
                new Vector2(1f, 0f), new Vector2(-70f, 46f), new Vector2(70f, 70f),
                new Color(0.25f, 0.45f, 0.75f, 0.9f));

            return card;
        }

        static InputField MakeInputField(Transform parent, Font font, string name, string placeholder,
                                         Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            img.sprite = UIBackground();
            img.type = Image.Type.Sliced;
            img.color = new Color(0.10f, 0.12f, 0.16f, 0.95f);

            Text ph = MakeText(go.transform, "Placeholder", font,
                new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(30f, 10f), 30,
                TextAnchor.MiddleCenter);
            ph.text = placeholder;
            ph.fontStyle = FontStyle.Italic;
            ph.color = new Color(1f, 1f, 1f, 0.4f);
            ph.raycastTarget = false;

            Text text = MakeText(go.transform, "Text", font,
                new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(30f, 10f), 34,
                TextAnchor.MiddleCenter);
            text.supportRichText = false;
            text.raycastTarget = false;

            var input = go.GetComponent<InputField>();
            input.targetGraphic = img;
            input.textComponent = text;
            input.placeholder = ph;
            input.characterLimit = 12;
            return input;
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
