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

            BuildMenuScenery();

            Font font = UIStyle.Body;
            GameObject canvasGO = BuildCanvas();
            // everything lives under the safe-area root, so notches/rounded corners can't clip it
            Transform ui = UIStyle.SafeRoot(canvasGO.transform);

            // Home screen root: the title and top-level buttons live under one container so
            // MainMenuController can hide the whole home screen while a panel is open.
            var homeRoot = new GameObject("HomeRoot", typeof(RectTransform));
            homeRoot.transform.SetParent(ui, false);
            Stretch(homeRoot.GetComponent<RectTransform>());

            BuildLogo(homeRoot.transform);

            // Top-level buttons: a chunky column down the left, leaving the beach scene visible
            var column = new Vector2(0f, 0.5f);
            float x = 90f + MenuBtnSize.x * 0.5f;
            Button quickPlay = MakeButton(homeRoot.transform, font, "QuickPlayButton", "Quick Play",
                column, new Vector2(x, 40f), MenuBtnSize, MenuOrange, anchorPivot: false);
            Button campaign = MakeButton(homeRoot.transform, font, "CampaignButton", "Campaign",
                column, new Vector2(x, -70f), MenuBtnSize, MenuTeal, anchorPivot: false);
            Button online = MakeButton(homeRoot.transform, font, "OnlineButton", "Online",
                column, new Vector2(x, -180f), MenuBtnSize, MenuPurple, anchorPivot: false);
            Button settings = MakeButton(homeRoot.transform, font, "SettingsButton", "Settings",
                column, new Vector2(x - 60f, -290f), MenuBtnSmall, MenuSlate, anchorPivot: false);
            Button quit = MakeButton(homeRoot.transform, font, "QuitButton", "Quit",
                column, new Vector2(x - 60f, -380f), MenuBtnSmall, MenuRed, anchorPivot: false);

            Text version = MakeText(homeRoot.transform, "Version", font,
                new Vector2(1f, 0f), new Vector2(-24f, 16f), new Vector2(500f, 40f), 24, TextAnchor.LowerRight);
            version.color = new Color(1f, 1f, 1f, 0.7f);
            version.gameObject.AddComponent<VersionLabel>();
            UIStyle.Pop(version, 1.5f);

            GameObject settingsPanel = BuildSettingsPanel(ui, font);
            GameObject campaignPanel = BuildCampaignPanel(ui, font);
            GameObject characterSelectPanel = BuildCharacterSelectPanel(ui, font);
            GameObject lobbyPanel = BuildOnlineLobbyPanel(ui, font);
            GameObject onlinePanel = BuildOnlinePanel(ui, font, lobbyPanel);

            var homeFocus = homeRoot.AddComponent<MenuFocus>();
            homeFocus.first = quickPlay;

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

        // ----------------------------------------------------------------- home screen

        /// <summary>Stacked cartoon logo, top-left, gently bobbing, with a WORLD TOUR badge.</summary>
        static void BuildLogo(Transform parent)
        {
            Font title = UIStyle.Title;
            var logo = new GameObject("Logo", typeof(RectTransform));
            logo.transform.SetParent(parent, false);
            var lrt = logo.GetComponent<RectTransform>();
            lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0f, 1f);
            lrt.sizeDelta = new Vector2(760f, 330f);
            lrt.anchoredPosition = new Vector2(70f, -40f);
            logo.AddComponent<MenuBob>();

            var dark = new Color(0.28f, 0.12f, 0.05f, 1f);
            Text animal = MakeText(logo.transform, "Animal", title,
                new Vector2(0f, 1f), new Vector2(10f, 0f), new Vector2(760f, 130f), 128, TextAnchor.UpperLeft);
            animal.text = "ANIMAL";
            animal.color = new Color(1.00f, 0.86f, 0.26f);
            UIStyle.Pop(animal, 6f, dark);

            Text volley = MakeText(logo.transform, "Volleyball", title,
                new Vector2(0f, 1f), new Vector2(0f, -112f), new Vector2(760f, 130f), 116, TextAnchor.UpperLeft);
            volley.text = "VOLLEYBALL";
            volley.color = new Color(1.00f, 0.55f, 0.18f);
            UIStyle.Pop(volley, 6f, dark);

            var badge = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(logo.transform, false);
            var brt = badge.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0f, 1f);
            brt.sizeDelta = new Vector2(300f, 58f);
            brt.anchoredPosition = new Vector2(14f, -246f);
            var bimg = badge.GetComponent<Image>();
            bimg.sprite = UISprite();
            bimg.type = Image.Type.Sliced;
            bimg.color = MenuTeal;
            Text tour = MakeText(badge.transform, "Label", UIStyle.Body,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(300f, 58f), 34, TextAnchor.MiddleCenter);
            tour.text = "WORLD TOUR";
            UIStyle.Pop(tour, 2f, new Color(0.05f, 0.25f, 0.22f, 0.9f));
        }

        /// <summary>
        /// The live backdrop: the toon Sunset Beach, a gang of 3D animals hanging out on the far
        /// court (idling, cheering now and then), and a slow drifting camera framed so the button
        /// column on the left doesn't cover them.
        /// </summary>
        static void BuildMenuScenery()
        {
            ToonBeachDecorator.BuildEnvironment();

            var cam = Camera.main;
            if (cam != null)
            {
                // framed from the +Z end so the net runs off behind the button column
                var look = new Vector3(-0.8f, 1.3f, 4.6f);
                cam.transform.position = new Vector3(8.8f, 3.3f, -0.6f);
                cam.transform.LookAt(look);
                cam.fieldOfView = 42f;
                cam.gameObject.AddComponent<MenuCameraDrift>().lookAt = look;
            }

            var gang = new GameObject("Menu Animals").transform;
            (string id, Vector3 pos, Color jersey)[] cast =
            {
                ("fox", new Vector3(1.2f, 0f, 3.0f), new Color(0.20f, 0.50f, 0.95f)),
                ("bear", new Vector3(-1.4f, 0f, 4.6f), new Color(0.45f, 0.80f, 1.00f)),
                ("penguin", new Vector3(2.6f, 0f, 5.2f), new Color(0.95f, 0.30f, 0.25f)),
                ("giraffe", new Vector3(-2.4f, 0f, 3.4f), new Color(0.98f, 0.60f, 0.20f)),
                ("lion", new Vector3(-0.6f, 0f, 7.0f), new Color(0.30f, 0.80f, 0.42f)),
            };
            for (int i = 0; i < cast.Length; i++)
            {
                var (id, pos, jersey) = cast[i];
                var spot = new GameObject("Menu " + id).transform;
                spot.SetParent(gang, false);
                spot.position = pos;
                Vector3 toCam = cam != null ? cam.transform.position - pos : Vector3.right;
                toCam.y = 0f;
                spot.rotation = Quaternion.LookRotation(toCam.normalized) * Quaternion.Euler(0f, (i % 3 - 1) * 18f, 0f);
                var a = spot.gameObject.AddComponent<MenuAnimal>();
                a.characterId = id;
                a.jersey = jersey;
            }
        }

        // ----------------------------------------------------------------- palette / sizes

        static readonly Vector2 MenuBtnSize = new Vector2(460f, 96f);
        static readonly Vector2 MenuBtnSmall = new Vector2(340f, 76f);
        static readonly Color MenuBlue = new Color(0.22f, 0.55f, 0.95f, 1f);
        static readonly Color MenuRed = new Color(0.92f, 0.34f, 0.30f, 1f);
        static readonly Color MenuOrange = new Color(1.00f, 0.58f, 0.18f, 1f);
        static readonly Color MenuTeal = new Color(0.16f, 0.72f, 0.64f, 1f);
        static readonly Color MenuPurple = new Color(0.56f, 0.42f, 0.95f, 1f);
        static readonly Color MenuSlate = new Color(0.36f, 0.46f, 0.62f, 1f);
        static readonly Color PanelDim = new Color(0.05f, 0.07f, 0.16f, 0.86f);

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
            var spFocus = panel.AddComponent<MenuFocus>();
            spFocus.first = rows.Count > 0 ? (Selectable)rows[0].slider : sp.backButton;
            spFocus.back = sp.backButton;

            panel.SetActive(false);
            return panel;
        }

        /// <summary>
        /// The World Tour screen. Left: the toon 3D world map (baked by WorldTourArtBaker from
        /// Tools/blender/map_gen.py) with chunky numbered pins, a dotted travel path and the
        /// protagonist's headshot hopping between stops. Right: a region card — arena postcard,
        /// name, blurb, the duo you'll face with their 3D portraits, and the local quirk. Pin
        /// positions come from the map bake (the map is a tilted perspective render), falling
        /// back to <see cref="RegionDef.mapSpot"/>.
        /// </summary>
        static GameObject BuildCampaignPanel(Transform parent, Font font)
        {
            GameObject panel = MakeDimPanel(parent, "CampaignPanel");
            var cp = panel.AddComponent<CampaignPanel>();

            Text title = MakeText(panel.transform, "Title", UIStyle.Title,
                new Vector2(0.5f, 0.5f), new Vector2(-330f, 445f), new Vector2(1000f, 90f), 76,
                TextAnchor.MiddleCenter);
            title.text = "WORLD TOUR";
            title.color = new Color(1.00f, 0.86f, 0.26f);
            UIStyle.Pop(title, 5f, new Color(0.28f, 0.12f, 0.05f, 1f));

            cp.statusLabel = MakeText(panel.transform, "Status", font,
                new Vector2(0.5f, 0.5f), new Vector2(-330f, 372f), new Vector2(1100f, 44f), 28,
                TextAnchor.MiddleCenter);
            cp.statusLabel.text = "";

            // ---- the map, framed like a board-game card ----
            Vector2 mapSize = new Vector2(1180f, 590f); // 2:1, like the baked render
            Vector2 mapPos = new Vector2(-330f, 30f);
            MakeCard(panel.transform, "MapFrame", mapPos, mapSize + new Vector2(28f, 28f), new Color(0.10f, 0.16f, 0.30f, 1f));

            var mapGO = new GameObject("WorldMap", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            mapGO.transform.SetParent(panel.transform, false);
            var mapRt = mapGO.GetComponent<RectTransform>();
            mapRt.anchorMin = mapRt.anchorMax = mapRt.pivot = new Vector2(0.5f, 0.5f);
            mapRt.sizeDelta = mapSize;
            mapRt.anchoredPosition = mapPos;
            var mapImg = mapGO.GetComponent<Image>();
            mapImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WorldTourArtBaker.MapPath) ?? WorldMapArt.GetSprite();
            mapImg.raycastTarget = false;

            var baked = WorldTourArtBaker.ReadPins();
            Vector2 PinPos(RegionDef r)
            {
                Vector2 uv = baked != null && baked.TryGetValue(r.id, out var p) ? p : r.mapSpot;
                return new Vector2((uv.x - 0.5f) * mapSize.x, (uv.y - 0.5f) * mapSize.y) + new Vector2(0f, 30f);
            }

            // ---- dotted travel path (under the pins) ----
            var legs = new List<CampaignPanel.PathLeg>();
            for (int i = 0; i < RegionRoster.All.Length - 1; i++)
            {
                Vector2 a = PinPos(RegionRoster.All[i]);
                Vector2 b = PinPos(RegionRoster.All[i + 1]);
                int n = Mathf.Max(2, Mathf.RoundToInt(Vector2.Distance(a, b) / 30f) - 1);
                var dots = new List<Image>();
                for (int j = 1; j <= n; j++)
                {
                    var dot = new GameObject($"Leg{i}Dot{j}", typeof(RectTransform), typeof(Image));
                    dot.transform.SetParent(mapGO.transform, false);
                    var drt = dot.GetComponent<RectTransform>();
                    drt.anchorMin = drt.anchorMax = drt.pivot = new Vector2(0.5f, 0.5f);
                    drt.sizeDelta = new Vector2(12f, 12f);
                    // a gentle arc, like a flight path
                    float t = j / (n + 1f);
                    drt.anchoredPosition = Vector2.Lerp(a, b, t) + new Vector2(0f, Mathf.Sin(t * Mathf.PI) * 40f);
                    var dimg = dot.GetComponent<Image>();
                    dimg.sprite = UIKnob();
                    dimg.color = new Color(1f, 1f, 1f, 0.18f); // CampaignPanel re-tints by state
                    dimg.raycastTarget = false;
                    dots.Add(dimg);
                }
                legs.Add(new CampaignPanel.PathLeg { dots = dots.ToArray() });
            }
            cp.legs = legs.ToArray();

            // ---- region pins: numbered tokens ----
            var pins = new List<CampaignPanel.MapPin>();
            for (int i = 0; i < RegionRoster.All.Length; i++)
            {
                RegionDef region = RegionRoster.All[i];
                var pinGO = new GameObject(region.displayName + " Pin",
                    typeof(RectTransform), typeof(Image), typeof(Button));
                pinGO.transform.SetParent(mapGO.transform, false);
                var prt = pinGO.GetComponent<RectTransform>();
                prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
                prt.sizeDelta = new Vector2(54f, 54f);
                prt.anchoredPosition = PinPos(region);
                var pinImg = pinGO.GetComponent<Image>();
                pinImg.sprite = UIKnob();
                pinImg.color = new Color(0.55f, 0.58f, 0.66f, 1f); // panel re-tints by state
                pinGO.GetComponent<Button>().targetGraphic = pinImg;
                UIStyle.Pop(pinImg, 2.5f, new Color(0.10f, 0.08f, 0.14f, 0.9f));
                pinGO.AddComponent<MenuButtonJuice>();

                Text label = MakeText(pinGO.transform, "Number", font,
                    new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(54f, 54f), 30,
                    TextAnchor.MiddleCenter);
                label.text = (i + 1).ToString();
                label.raycastTarget = false;
                UIStyle.Pop(label, 2f);

                pins.Add(new CampaignPanel.MapPin
                {
                    regionId = region.id,
                    button = pinGO.GetComponent<Button>(),
                    pin = pinImg,
                    label = label,
                });
            }
            cp.pins = pins.ToArray();

            // ---- the travelling protagonist: a 3D headshot in a white token ----
            CharacterDef protagonist = CharacterRoster.Get(CharacterRoster.ProtagonistId);
            var markerGO = new GameObject("TourMarker", typeof(RectTransform), typeof(Image));
            markerGO.transform.SetParent(mapGO.transform, false);
            var mrt = markerGO.GetComponent<RectTransform>();
            mrt.anchorMin = mrt.anchorMax = mrt.pivot = new Vector2(0.5f, 0.5f);
            mrt.sizeDelta = new Vector2(78f, 78f);
            var backing = markerGO.GetComponent<Image>();
            backing.sprite = UIKnob();
            backing.color = new Color(1f, 1f, 1f, 0.95f);
            backing.raycastTarget = false;
            UIStyle.Pop(backing, 2.5f, new Color(0.10f, 0.08f, 0.14f, 0.9f));
            var face = new GameObject("Face", typeof(RectTransform), typeof(Image));
            face.transform.SetParent(markerGO.transform, false);
            var frt = face.GetComponent<RectTransform>();
            frt.anchorMin = frt.anchorMax = frt.pivot = new Vector2(0.5f, 0.5f);
            frt.sizeDelta = new Vector2(86f, 86f);
            frt.anchoredPosition = new Vector2(0f, 4f);
            var faceImg = face.GetComponent<Image>();
            faceImg.sprite = PortraitFor(protagonist);
            faceImg.preserveAspect = true;
            faceImg.raycastTarget = false;
            cp.marker = mrt;

            // ---- region card (right) ----
            var card = MakeCard(panel.transform, "RegionCard", new Vector2(640f, 30f), new Vector2(540f, 830f),
                                new Color(0.10f, 0.16f, 0.30f, 0.96f)).transform;
            cp.regionName = MakeText(card, "RegionName", font,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 372f), new Vector2(520f, 56f), 40, TextAnchor.MiddleCenter);
            UIStyle.Pop(cp.regionName, 2.5f);

            var thumbGO = new GameObject("Postcard", typeof(RectTransform), typeof(Image));
            thumbGO.transform.SetParent(card, false);
            var trt = thumbGO.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(496f, 279f);
            trt.anchoredPosition = new Vector2(0f, 190f);
            cp.regionThumb = thumbGO.GetComponent<Image>();
            cp.regionThumb.raycastTarget = false;
            UIStyle.Pop(cp.regionThumb, 3f, new Color(1f, 1f, 1f, 0.9f));

            cp.regionBlurb = MakeText(card, "Blurb", font,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(490f, 70f), 22, TextAnchor.MiddleCenter);
            cp.regionBlurb.horizontalOverflow = HorizontalWrapMode.Wrap;
            cp.regionBlurb.color = new Color(1f, 1f, 1f, 0.85f);

            cp.stateLabel = MakeText(card, "State", font,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -58f), new Vector2(500f, 40f), 28, TextAnchor.MiddleCenter);
            UIStyle.Pop(cp.stateLabel, 2f);
            cp.teamName = MakeText(card, "Team", font,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -98f), new Vector2(500f, 44f), 34, TextAnchor.MiddleCenter);
            UIStyle.Pop(cp.teamName, 2f);

            cp.oppPortraits = new Image[2];
            cp.oppNames = new Text[2];
            for (int k = 0; k < 2; k++)
            {
                float x = k == 0 ? -120f : 120f;
                var disc = new GameObject("OppDisc" + k, typeof(RectTransform), typeof(Image));
                disc.transform.SetParent(card, false);
                var drt = disc.GetComponent<RectTransform>();
                drt.anchorMin = drt.anchorMax = drt.pivot = new Vector2(0.5f, 0.5f);
                drt.sizeDelta = new Vector2(150f, 150f);
                drt.anchoredPosition = new Vector2(x, -210f);
                var dimg = disc.GetComponent<Image>();
                dimg.sprite = UIKnob();
                dimg.color = new Color(0.95f, 0.36f, 0.30f, 0.9f); // the opposition wears red
                dimg.raycastTarget = false;

                var opp = new GameObject("Opp" + k, typeof(RectTransform), typeof(Image));
                opp.transform.SetParent(disc.transform, false);
                var ort = opp.GetComponent<RectTransform>();
                ort.anchorMin = ort.anchorMax = ort.pivot = new Vector2(0.5f, 0.5f);
                ort.sizeDelta = new Vector2(160f, 160f);
                ort.anchoredPosition = new Vector2(0f, 6f);
                cp.oppPortraits[k] = opp.GetComponent<Image>();
                cp.oppPortraits[k].preserveAspect = true;
                cp.oppPortraits[k].raycastTarget = false;

                cp.oppNames[k] = MakeText(card, "OppName" + k, font,
                    new Vector2(0.5f, 0.5f), new Vector2(x, -305f), new Vector2(240f, 32f), 20, TextAnchor.MiddleCenter);
            }

            cp.quirkLabel = MakeText(card, "Quirk", font,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -365f), new Vector2(500f, 60f), 22, TextAnchor.MiddleCenter);
            cp.quirkLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            cp.quirkLabel.color = new Color(1f, 0.85f, 0.4f);

            // ---- buttons ----
            cp.playButton = MakeButton(panel.transform, font, "PlayButton", "Play Next Match",
                new Vector2(0.5f, 0.5f), new Vector2(640f, -470f), new Vector2(540f, 90f), MenuOrange, anchorPivot: false);
            cp.playButtonLabel = cp.playButton.GetComponentInChildren<Text>();
            cp.newGameButton = MakeButton(panel.transform, font, "NewGameButton", "New Game",
                new Vector2(0.5f, 0.5f), new Vector2(-470f, -400f), new Vector2(320f, 72f), MenuSlate, anchorPivot: false);
            cp.newGameButtonLabel = cp.newGameButton.GetComponentInChildren<Text>();
            cp.backButton = MakeButton(panel.transform, font, "BackButton", "Back",
                new Vector2(0.5f, 0.5f), new Vector2(-790f, -400f), new Vector2(240f, 72f), MenuRed, anchorPivot: false);
            var cpFocus = panel.AddComponent<MenuFocus>();
            cpFocus.first = cp.playButton;
            cpFocus.back = cp.backButton;

            panel.SetActive(false);
            return panel;
        }

        /// <summary>A rounded card with a darker lip underneath (the menu's panel style).</summary>
        static GameObject MakeCard(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var lip = new GameObject(name + "Lip", typeof(RectTransform), typeof(Image));
            lip.transform.SetParent(parent, false);
            var lrt = lip.GetComponent<RectTransform>();
            lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = size;
            lrt.anchoredPosition = pos + new Vector2(0f, -8f);
            var limg = lip.GetComponent<Image>();
            limg.sprite = UISprite();
            limg.type = Image.Type.Sliced;
            limg.color = new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, color.a);
            limg.raycastTarget = false;

            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            img.sprite = UISprite();
            img.type = Image.Type.Sliced;
            img.color = color;
            return go;
        }

        /// <summary>
        /// The Quick Play character-select screen. Left: a scrollable grid of roster tiles — a 3D
        /// headshot (PortraitBaker) on a card tinted by the animal's home region. Right: a live 3D
        /// preview of the highlighted animal (<see cref="CharacterShowcase"/>, rendered from a
        /// pedestal stage built below the menu beach), its name and blurb, and stat bars; then
        /// venue, Play and Back. The selection logic is <see cref="CharacterSelectPanel"/>.
        /// </summary>
        static GameObject BuildCharacterSelectPanel(Transform parent, Font font)
        {
            GameObject panel = MakeDimPanel(parent, "CharacterSelectPanel");
            var cs = panel.AddComponent<CharacterSelectPanel>();

            Text title = MakeText(panel.transform, "Title", font,
                new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1200f, 90f), 64,
                TextAnchor.MiddleCenter);
            title.text = "Choose Your Animal";
            title.fontStyle = FontStyle.Bold;

            // ---- roster grid (left): a vertical scroll view, since the roster is far
            //      bigger than one screen ----
            var scrollGO = new GameObject("RosterScroll",
                typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            scrollGO.transform.SetParent(panel.transform, false);
            var scrollRt = scrollGO.GetComponent<RectTransform>();
            scrollRt.anchorMin = scrollRt.anchorMax = scrollRt.pivot = new Vector2(0.5f, 0.5f);
            scrollRt.sizeDelta = new Vector2(860f, 790f);
            scrollRt.anchoredPosition = new Vector2(-460f, -40f);
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
            grid.cellSize = new Vector2(158f, 186f);
            grid.spacing = new Vector2(8f, 8f);
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
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

                Color tint = RegionTint(ch.region);
                var frame = go.GetComponent<Image>();
                frame.sprite = UISprite();
                frame.type = Image.Type.Sliced;
                frame.color = tint;
                go.GetComponent<Button>().targetGraphic = frame;

                // a lighter disc behind the head so dark animals (penguin, bear) still pop
                var discGO = new GameObject("Disc", typeof(RectTransform), typeof(Image));
                discGO.transform.SetParent(go.transform, false);
                var drt = discGO.GetComponent<RectTransform>();
                drt.anchorMin = drt.anchorMax = drt.pivot = new Vector2(0.5f, 0.5f);
                drt.sizeDelta = new Vector2(132f, 132f);
                drt.anchoredPosition = new Vector2(0f, 18f);
                var disc = discGO.GetComponent<Image>();
                disc.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                disc.color = new Color(1f, 1f, 1f, 0.22f);
                disc.raycastTarget = false;

                var portraitGO = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                portraitGO.transform.SetParent(go.transform, false);
                var prt = portraitGO.GetComponent<RectTransform>();
                prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
                prt.sizeDelta = new Vector2(146f, 146f);
                prt.anchoredPosition = new Vector2(0f, 18f);
                var portrait = portraitGO.GetComponent<Image>();
                portrait.sprite = PortraitFor(ch);
                portrait.preserveAspect = true;
                portrait.raycastTarget = false;

                Text nameLabel = MakeText(go.transform, "Name", font,
                    new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(156f, 32f), 20,
                    TextAnchor.MiddleCenter);
                nameLabel.text = ShortName(ch.displayName);
                nameLabel.fontStyle = FontStyle.Bold;
                nameLabel.raycastTarget = false;

                entries.Add(new CharacterSelectPanel.Entry
                {
                    characterId = ch.id,
                    button = go.GetComponent<Button>(),
                    frame = frame,
                    portrait = portrait,
                    baseColor = tint,
                });
            }
            cs.entries = entries.ToArray();

            // ---- preview pane (right): live 3D showcase on a sky card ----
            var cardGO = new GameObject("ShowcaseCard", typeof(RectTransform), typeof(Image));
            cardGO.transform.SetParent(panel.transform, false);
            var cardRt = cardGO.GetComponent<RectTransform>();
            cardRt.anchorMin = cardRt.anchorMax = cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(500f, 470f);
            cardRt.anchoredPosition = new Vector2(440f, 130f);
            var card = cardGO.GetComponent<Image>();
            card.sprite = UISprite();
            card.type = Image.Type.Sliced;
            card.color = new Color(0.45f, 0.72f, 0.98f, 0.45f);

            var showGO = new GameObject("Showcase", typeof(RectTransform), typeof(RawImage));
            showGO.transform.SetParent(cardGO.transform, false);
            var showRt = showGO.GetComponent<RectTransform>();
            showRt.anchorMin = showRt.anchorMax = showRt.pivot = new Vector2(0.5f, 0.5f);
            showRt.sizeDelta = new Vector2(470f, 470f);
            var raw = showGO.GetComponent<RawImage>();
            raw.raycastTarget = false;

            var showcase = panel.AddComponent<CharacterShowcase>();
            showcase.target = raw;
            BuildShowcaseStage(showcase);
            cs.showcase = showcase;

            cs.previewName = MakeText(panel.transform, "PreviewName", font,
                new Vector2(0.5f, 0.5f), new Vector2(440f, -130f), new Vector2(620f, 60f), 48,
                TextAnchor.MiddleCenter);
            cs.previewName.fontStyle = FontStyle.Bold;

            cs.previewBlurb = MakeText(panel.transform, "PreviewBlurb", font,
                new Vector2(0.5f, 0.5f), new Vector2(440f, -192f), new Vector2(700f, 64f), 24,
                TextAnchor.MiddleCenter);

            cs.heightBar = BuildStatBar(panel.transform, font, "Height", -248f);
            cs.speedBar = BuildStatBar(panel.transform, font, "Speed", -286f);
            cs.powerBar = BuildStatBar(panel.transform, font, "Power", -324f);
            cs.controlBar = BuildStatBar(panel.transform, font, "Control", -362f);
            cs.jumpBar = BuildStatBar(panel.transform, font, "Jump", -400f);

            cs.venueButton = MakeButton(panel.transform, font, "VenueButton", "Venue",
                new Vector2(0.5f, 0.5f), new Vector2(290f, -474f), new Vector2(440f, 60f),
                new Color(0.16f, 0.30f, 0.50f, 0.92f));
            cs.venueLabel = cs.venueButton.GetComponentInChildren<Text>();
            cs.venueLabel.fontSize = 26;

            cs.playButton = MakeButton(panel.transform, font, "PlayButton", "Play",
                new Vector2(0.5f, 0.5f), new Vector2(660f, -474f), new Vector2(260f, 76f),
                new Color(0.30f, 0.80f, 0.40f, 0.95f));
            cs.backButton = MakeButton(panel.transform, font, "BackButton", "Back",
                new Vector2(0.5f, 0.5f), new Vector2(-790f, -474f), new Vector2(240f, 70f), MenuRed);
            var csFocus = panel.AddComponent<MenuFocus>();
            csFocus.first = cs.entries.Length > 0 ? cs.entries[0].button : cs.playButton;
            csFocus.back = cs.backButton;
            cs.scroll = scroll;

            panel.SetActive(false);
            return panel;
        }

        /// <summary>
        /// The showcase's little world: a sand pedestal and its own camera, parked far below the
        /// menu's beach so neither sees the other (the camera's far clip ends well short of it).
        /// Lit by the scene's sun and ambient like everything else.
        /// </summary>
        static void BuildShowcaseStage(CharacterShowcase showcase)
        {
            var stage = new GameObject("Character Showcase Stage").transform;
            stage.position = new Vector3(0f, -80f, 0f);

            ToonArtKit.Prop("pedestal", Vector3.zero, 0f, 0.7f, ToonArtKit.PropsMaterial(), stage);
            var point = new GameObject("Stand Point").transform;
            point.SetParent(stage, false);
            point.localPosition = new Vector3(0f, 0.02f, 0f);

            var cam = new GameObject("Showcase Camera").AddComponent<Camera>();
            cam.transform.SetParent(stage, false);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // the UI card shows through
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 25f;
            cam.enabled = false; // CharacterShowcase runs it only while the panel is open

            showcase.stageCamera = cam;
            showcase.stagePoint = point;
        }

        static Sprite PortraitFor(CharacterDef ch)
        {
            var head = AssetDatabase.LoadAssetAtPath<Sprite>(
                $"Assets/Resources/{CharacterPortraits.ResourceDir}/animal_{ch.id}.png");
            return head != null ? head : CharacterArt.GetCharacterFrames(PlayerColors.Human, ch)[0];
        }

        /// <summary>"Finn the Fox" → "Finn" keeps tiles uncluttered; the preview shows the full name.</summary>
        static string ShortName(string displayName)
        {
            int i = displayName.IndexOf(" the ", System.StringComparison.Ordinal);
            return i > 0 ? displayName.Substring(0, i) : displayName;
        }

        /// <summary>Card tint per home region, so the grid reads as the world tour's teams.</summary>
        static Color RegionTint(string region)
        {
            switch (region)
            {
                case "savanna": return new Color(0.95f, 0.72f, 0.30f, 0.35f);
                case "amazon": return new Color(0.30f, 0.80f, 0.42f, 0.35f);
                case "outback": return new Color(0.92f, 0.45f, 0.25f, 0.35f);
                case "himalaya": return new Color(0.62f, 0.52f, 0.92f, 0.35f);
                case "forest": return new Color(0.35f, 0.62f, 0.32f, 0.35f);
                case "sahara": return new Color(0.96f, 0.64f, 0.36f, 0.35f);
                case "rockies": return new Color(0.45f, 0.62f, 0.82f, 0.35f);
                case "arctic": return new Color(0.62f, 0.86f, 1.00f, 0.35f);
                default: return new Color(0.30f, 0.65f, 1.00f, 0.35f); // the home duo
            }
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
            var opFocus = panel.AddComponent<MenuFocus>();
            opFocus.first = serverMatch;
            opFocus.back = back;
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
            UIStyle.ConfigureScaler(canvasGO.GetComponent<CanvasScaler>());
            return canvasGO;
        }

        /// <summary>
        /// A chunky toy button: rounded pill in <paramref name="color"/> sitting on a darker "lip"
        /// (reads as a raised key), outlined label, and <see cref="MenuButtonJuice"/> bounce.
        /// With anchorPivot=false the pivot stays centred, so pos is the button's centre.
        /// </summary>
        static Button MakeButton(Transform parent, Font font, string name, string label,
                                 Vector2 anchor, Vector2 pos, Vector2 size, Color color,
                                 bool anchorPivot = true)
        {
            // the Button lives on the root (clicks bubble up from the face), so hiding the
            // button's GameObject hides the whole thing — lip included
            var go = new GameObject(name, typeof(RectTransform), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchorPivot ? anchor : new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            float lip = Mathf.Clamp(size.y * 0.09f, 4f, 9f);
            var lipGO = new GameObject("Lip", typeof(RectTransform), typeof(Image));
            lipGO.transform.SetParent(go.transform, false);
            var lrt = lipGO.GetComponent<RectTransform>();
            Stretch(lrt);
            lrt.offsetMin = new Vector2(0f, -lip);
            lrt.offsetMax = new Vector2(0f, -lip);
            var lipImg = lipGO.GetComponent<Image>();
            lipImg.sprite = UISprite();
            lipImg.type = Image.Type.Sliced;
            lipImg.color = new Color(color.r * 0.55f, color.g * 0.55f, color.b * 0.55f, color.a);
            lipImg.raycastTarget = false;

            var face = new GameObject("Face", typeof(RectTransform), typeof(Image));
            face.transform.SetParent(go.transform, false);
            Stretch(face.GetComponent<RectTransform>());
            var img = face.GetComponent<Image>();
            img.sprite = UISprite();
            img.type = Image.Type.Sliced;
            img.color = color;
            var button = go.GetComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            button.colors = colors;

            // a soft highlight across the top half gives the pill some volume
            var shine = new GameObject("Shine", typeof(RectTransform), typeof(Image));
            shine.transform.SetParent(face.transform, false);
            var srt = shine.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 0.52f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.offsetMin = new Vector2(8f, 0f);
            srt.offsetMax = new Vector2(-8f, -4f);
            var shineImg = shine.GetComponent<Image>();
            shineImg.sprite = UISprite();
            shineImg.type = Image.Type.Sliced;
            shineImg.color = new Color(1f, 1f, 1f, 0.16f);
            shineImg.raycastTarget = false;

            int fontSize = Mathf.RoundToInt(Mathf.Clamp(size.y * 0.44f, 22f, 44f));
            Text t = MakeText(face.transform, "Label", font,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), size, fontSize, TextAnchor.MiddleCenter);
            t.text = label;
            t.raycastTarget = false;
            UIStyle.Pop(t, 2f, new Color(color.r * 0.35f, color.g * 0.35f, color.b * 0.35f, 0.9f));

            // focus ring for controller/keyboard navigation (MenuButtonJuice toggles it)
            var ring = new GameObject("FocusRing", typeof(RectTransform), typeof(Image));
            ring.transform.SetParent(go.transform, false);
            ring.transform.SetAsFirstSibling(); // behind the lip and face
            var rrt = ring.GetComponent<RectTransform>();
            Stretch(rrt);
            rrt.offsetMin = new Vector2(-14f, -14f - lip);
            rrt.offsetMax = new Vector2(14f, 14f);
            var ringImg = ring.GetComponent<Image>();
            ringImg.sprite = UISprite();
            ringImg.type = Image.Type.Sliced;
            ringImg.color = new Color(1f, 0.93f, 0.25f, 1f); // bold yellow: unmissable on every panel
            ringImg.raycastTarget = false;
            ring.SetActive(false);
            var colorsSel = button.colors;
            colorsSel.selectedColor = colorsSel.highlightedColor;
            button.colors = colorsSel;

            go.AddComponent<MenuButtonJuice>().focusRing = ring;
            return button;
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
