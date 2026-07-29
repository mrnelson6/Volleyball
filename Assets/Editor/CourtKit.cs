using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// Reusable builder for the playable "keys" of a volleyball match — the minimum set of
    /// GameObjects gameplay code needs to run (ground + net, ball, players, MatchManager,
    /// input, HUD/touch UI, and optionally a camera + light).
    ///
    /// Unlike <c>PrototypeSceneBuilder</c> (which generates a whole scene from scratch), this
    /// drops the keys <b>additively</b> into the currently open scene and is <b>idempotent</b>:
    /// every piece is skipped if an equivalent already exists, so level designers can decorate a
    /// scene however they like and then inject playability without anything being clobbered or
    /// duplicated.
    ///
    /// NOTE: gameplay is locked to a court centred on the world origin (player clamping, ball
    /// in/out detection and camera-relative controls all read <see cref="CourtGeometry"/>'s
    /// origin-centred constants). The keys are therefore always built at the origin.
    /// </summary>
    public static class CourtKit
    {
        public const string SpritePath = "Assets/Sprites/circle.png";
        public const string BeachBallPath = "Assets/Sprites/beachball.png";
        public const string MaterialDir = "Assets/Materials";

        // per-player jersey colours — the runtime constants, so baked sprites match what
        // CharacterSprites loads back for runtime character swaps
        static Color ColPlayer => PlayerColors.Human;
        static Color ColMate => PlayerColors.Mate;
        static Color ColOpp1 => PlayerColors.Opp1;
        static Color ColOpp2 => PlayerColors.Opp2;

        /// <summary>Which optional pieces to include when dropping the keys into a scene.</summary>
        public class Options
        {
            /// <summary>Build a side-on broadcast camera if the scene has no MainCamera.</summary>
            public bool buildCamera = true;
            /// <summary>Build a directional light if the scene has no directional light.</summary>
            public bool buildLight = true;
            /// <summary>Build the HUD canvas + on-screen touch controls + EventSystem.</summary>
            public bool buildUI = true;
            /// <summary>Parent the court geometry + actors under a single tidy root object.</summary>
            public bool groupUnderRoot = true;
        }

        /// <summary>
        /// Ensure a fully playable volleyball match exists in the active scene, creating only the
        /// pieces that are missing. Returns the (existing or newly created) MatchManager.
        /// </summary>
        public static MatchManager DropInCourt(Options opt = null)
        {
            opt ??= new Options();
            EnsureFolders();

            Sprite circle = GetCircleSprite();
            Sprite beachBall = GetBeachBallSprite();
            Material sand = MakeUnlitMaterial("Sand", new Color(0.93f, 0.85f, 0.62f));
            Material line = MakeUnlitMaterial("Line", Color.white);
            Material netMat = MakeUnlitMaterial("Net", new Color(0.9f, 0.9f, 0.9f));

            Transform root = opt.groupUnderRoot ? GetOrCreateRoot("Volleyball Court").transform : null;

            if (opt.buildCamera) EnsureCamera();
            if (opt.buildLight) EnsureLight();

            EnsureCourt(root, sand, line, netMat);

            BallController ball = EnsureBall(root, beachBall, circle);
            List<VolleyPlayer> players = EnsurePlayers(root, circle);
            EnsureGameInput();

            MatchManager match = EnsureMatchManager(root, ball, players);
            EnsureNetworking(ball, players, match);

            if (opt.buildUI)
            {
                EnsureUI(match, circle);
                EnsureEventSystem();
            }

            return match;
        }

        /// <summary>
        /// Attach the multiplayer adapters to the playable keys: NetworkObject + NetworkPlayer
        /// on each player, NetworkObject + NetworkBall on the ball, and NetworkObject +
        /// NetworkMatchState + SnapshotSync + SimClock beside the MatchManager. Runs on
        /// already-built scenes too (separate from the create-if-missing steps above), so a
        /// world-tour rebuild upgrades every existing arena. All are in-scene placed objects —
        /// their GlobalObjectIdHash bakes into the saved scene, which is why any change here
        /// requires "Build World Tour (Everything)" + committing the regenerated scenes.
        /// Offline these components are dormant: no NetworkManager ever spawns them.
        /// </summary>
        static void EnsureNetworking(BallController ball, List<VolleyPlayer> players, MatchManager match)
        {
            foreach (var p in players)
                if (p != null) EnsureNetComponents(p.gameObject, typeof(NetworkPlayer));
            if (ball != null) EnsureNetComponents(ball.gameObject, typeof(NetworkBall));
            if (match != null)
            {
                EnsureNetComponents(match.gameObject, typeof(NetworkMatchState), typeof(SnapshotSync));
                if (match.GetComponent<SimClock>() == null)
                    match.gameObject.AddComponent<SimClock>();
            }
        }

        static void EnsureNetComponents(GameObject go, params System.Type[] behaviours)
        {
            if (go.GetComponent<Unity.Netcode.NetworkObject>() == null)
                go.AddComponent<Unity.Netcode.NetworkObject>();
            foreach (var t in behaviours)
                if (go.GetComponent(t) == null) go.AddComponent(t);
        }

        /// <summary>
        /// Build only the visual court (sand ground, lines, net) — no ball, players, match or UI.
        /// Used by non-gameplay scenes (e.g. the main menu backdrop) that want the same court look
        /// as a playable arena. Idempotent: skips if a court already exists.
        /// </summary>
        public static void BuildCourtVisual(Transform root = null)
        {
            EnsureFolders();
            Material sand = MakeUnlitMaterial("Sand", new Color(0.93f, 0.85f, 0.62f));
            Material line = MakeUnlitMaterial("Line", Color.white);
            Material netMat = MakeUnlitMaterial("Net", new Color(0.9f, 0.9f, 0.9f));
            EnsureCourt(root, sand, line, netMat);
        }

        // ----------------------------------------------------------------- scene queries

        static T FindInScene<T>() where T : Object
            => Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

        static bool Exists<T>() where T : Object => FindInScene<T>() != null;

        static GameObject GetOrCreateRoot(string name)
        {
            var existing = GameObject.Find(name);
            return existing != null ? existing : new GameObject(name);
        }

        static void Parent(GameObject go, Transform root)
        {
            if (root != null) go.transform.SetParent(root, true);
        }

        // ----------------------------------------------------------------- camera / light

        static void EnsureCamera()
        {
            if (Camera.main != null || GameObject.FindGameObjectWithTag("MainCamera") != null)
                return;

            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.45f, 0.75f, 0.95f);
            cam.fieldOfView = 36f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            go.AddComponent<AudioListener>();
            // Side-of-the-net broadcast view: off to one sideline, elevated, looking across so
            // the net is centred and the court's depth reads left-to-right. Controls are
            // camera-relative, so this angle is what makes the stick map intuitively.
            go.transform.position = new Vector3(20f, 12f, -3f);
            go.transform.LookAt(new Vector3(0f, 1.6f, 0f));
        }

        static void EnsureLight()
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.type == LightType.Directional) return;

            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = Color.white;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // ----------------------------------------------------------------- court geometry

        static void EnsureCourt(Transform root, Material sand, Material line, Material netMat)
        {
            // The GroundMarker plane is what tells the ball a contact is "the floor" (scoring),
            // so its presence is the signal that a court has already been built here.
            if (Exists<GroundMarker>()) return;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(3.2f, 1f, 4.4f); // plane is 10u → ~32 x 44
            ground.transform.position = Vector3.zero;
            ground.GetComponent<MeshRenderer>().sharedMaterial = sand;
            ground.AddComponent<GroundMarker>();
            Parent(ground, root);

            float w = CourtGeometry.HalfWidth;
            float d = CourtGeometry.HalfDepth;
            MakeLine(root, "Sideline -X", new Vector3(-w, 0.02f, 0f), new Vector3(0.1f, 0.02f, d * 2f), line);
            MakeLine(root, "Sideline +X", new Vector3(w, 0.02f, 0f), new Vector3(0.1f, 0.02f, d * 2f), line);
            MakeLine(root, "Baseline A", new Vector3(0f, 0.02f, -d), new Vector3(w * 2f, 0.02f, 0.1f), line);
            MakeLine(root, "Baseline B", new Vector3(0f, 0.02f, d), new Vector3(w * 2f, 0.02f, 0.1f), line);
            MakeLine(root, "Net Line", new Vector3(0f, 0.02f, 0f), new Vector3(w * 2f, 0.02f, 0.1f), line);

            var net = GameObject.CreatePrimitive(PrimitiveType.Cube);
            net.name = "Net";
            net.transform.position = new Vector3(0f, CourtGeometry.NetHeight * 0.5f, 0f);
            net.transform.localScale = new Vector3(w * 2f + 1f, CourtGeometry.NetHeight, 0.1f);
            net.GetComponent<MeshRenderer>().sharedMaterial = netMat;
            Parent(net, root);
        }

        static void MakeLine(Transform root, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // lines never collide
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            Parent(go, root);
        }

        // ----------------------------------------------------------------- ball / players

        static BallController EnsureBall(Transform root, Sprite ballSprite, Sprite shadowSprite)
        {
            var existing = FindInScene<BallController>();
            if (existing != null) return existing;

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

            Parent(go, root);
            Parent(shadow, root);
            return bc;
        }

        static List<VolleyPlayer> EnsurePlayers(Transform root, Sprite circle)
        {
            var existing = new List<VolleyPlayer>(
                Object.FindObjectsByType<VolleyPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            if (existing.Count > 0) return existing;

            // bake the whole roster in every jersey colour up front — runtime character swaps
            // (menu pick, random AI draws) load these from Resources
            CharacterArt.BakeRoster();

            var players = new List<VolleyPlayer>
            {
                // team A — human listed first so it serves for team A
                MakePlayer(root, "Player (You)", TeamSide.A, -1f, ColPlayer, true, circle,
                           CharacterRoster.ProtagonistId),
                MakePlayer(root, "Teammate (AI)", TeamSide.A, 1f, ColMate, false, circle,
                           CharacterRoster.TeammateId),
                // team B — opponents
                MakePlayer(root, "Opponent 1 (AI)", TeamSide.B, -1f, ColOpp1, false, circle, "lion"),
                MakePlayer(root, "Opponent 2 (AI)", TeamSide.B, 1f, ColOpp2, false, circle, "jaguar"),
            };
            return players;
        }

        static VolleyPlayer MakePlayer(Transform root, string name, TeamSide team, float halfSign,
                                       Color color, bool human, Sprite circle, string characterId)
        {
            CharacterDef character = CharacterRoster.Get(characterId);
            var go = new GameObject(name);

            var spr = new GameObject("Sprite");
            spr.transform.SetParent(go.transform, false);
            spr.transform.localPosition = CharacterArt.SpriteLocalPosFor(character);
            var sr = spr.AddComponent<SpriteRenderer>();
            spr.AddComponent<BillboardSprite>().yAxisOnly = true;
            // procedural human figure (idle/run/jump/swing) baked in this player's team colour,
            // at the roster character's height and with their skin/hair
            CharacterArt.AttachCharacter(spr, sr, color, character);

            VolleyPlayer vp = human
                ? go.AddComponent<PlayerController>()
                : go.AddComponent<AIController>();
            vp.team = team;
            vp.halfSign = halfSign;
            vp.characterId = character.id;
            vp.jerseyColor = color; // lets runtime swaps load the matching baked sprite set

            float x = halfSign * CourtGeometry.HalfWidth * 0.45f;
            float z = CourtGeometry.SideSign(team) * CourtGeometry.HalfDepth * 0.55f;
            go.transform.position = new Vector3(x, 0f, z);

            // ground shadow that shrinks as the player jumps
            var shadow = new GameObject(name + " Shadow");
            shadow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var ssr = shadow.AddComponent<SpriteRenderer>();
            ssr.sprite = circle;
            ssr.color = new Color(0f, 0f, 0f, 0.32f);
            var ds = shadow.AddComponent<DropShadow>();
            ds.target = go.transform;
            ds.baseSize = 1.0f * character.height; // a bigger body casts a bigger shadow
            ds.maxHeight = 2.4f;

            Parent(go, root);
            Parent(shadow, root);
            return vp;
        }

        static void EnsureGameInput()
        {
            // GameInput bootstraps itself at runtime, but placing one keeps the scene explicit.
            if (Exists<GameInput>()) return;
            new GameObject("GameInput", typeof(GameInput));
        }

        static MatchManager EnsureMatchManager(Transform root, BallController ball, List<VolleyPlayer> players)
        {
            var match = FindInScene<MatchManager>();
            if (match == null)
            {
                var go = new GameObject("MatchManager");
                match = go.AddComponent<MatchManager>();
                Parent(go, root);
            }

            // wire references (MatchManager also auto-finds, but be explicit)
            if (match.ball == null) match.ball = ball;
            if (match.players == null || match.players.Count == 0) match.players = players;
            return match;
        }

        // ----------------------------------------------------------------- UI

        static void EnsureUI(MatchManager match, Sprite circle)
        {
            if (Exists<ScoreHUD>()) return;

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
            Text matchLabel = MakeText(canvasGO.transform, "MatchLabel", font,
                new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(1400f, 44f), 28,
                TextAnchor.UpperCenter);
            matchLabel.color = new Color(1f, 1f, 1f, 0.85f);

            // human power-up meter: bottom-centre, clear of the joystick (bottom-left) and
            // the touch action cluster (bottom-right). ScoreHUD drives the fill each frame.
            var meterGO = new GameObject("PowerMeter", typeof(RectTransform), typeof(Image));
            meterGO.transform.SetParent(canvasGO.transform, false);
            var meterRT = meterGO.GetComponent<RectTransform>();
            meterRT.anchorMin = meterRT.anchorMax = new Vector2(0.5f, 0f);
            meterRT.pivot = new Vector2(0.5f, 0f);
            meterRT.sizeDelta = new Vector2(300f, 26f);
            meterRT.anchoredPosition = new Vector2(0f, 40f);
            var meterBg = meterGO.GetComponent<Image>();
            meterBg.sprite = circle;
            meterBg.type = Image.Type.Sliced;
            meterBg.color = new Color(0.10f, 0.12f, 0.16f, 0.8f);
            meterBg.raycastTarget = false;

            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(meterGO.transform, false);
            var fillRT = fillGO.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = new Vector2(0f, 1f); // ScoreHUD widens this with the charge
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            var fillImg = fillGO.GetComponent<Image>();
            fillImg.sprite = circle;
            fillImg.type = Image.Type.Sliced;
            fillImg.color = new Color(1f, 0.72f, 0.10f);
            fillImg.raycastTarget = false;

            Text powerLabel = MakeText(meterGO.transform, "PowerLabel", font,
                new Vector2(0.5f, 1f), new Vector2(0f, 32f), new Vector2(600f, 30f), 24,
                TextAnchor.LowerCenter);
            powerLabel.raycastTarget = false;
            powerLabel.color = new Color(1f, 1f, 1f, 0.9f);

            var hud = canvasGO.AddComponent<ScoreHUD>();
            hud.match = match;
            hud.scoreText = score;
            hud.bannerText = banner;
            hud.matchLabelText = matchLabel;
            hud.powerFill = fillImg;
            hud.powerLabel = powerLabel;

            // touch controls (auto-hidden on non-touch platforms)
            var panel = new GameObject("TouchPanel", typeof(RectTransform));
            panel.transform.SetParent(canvasGO.transform, false);
            Stretch(panel.GetComponent<RectTransform>());
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
            BuildButton(panel.transform, circle, "PowerButton", "POWER", VirtualButtonKind.Power,
                new Vector2(1f, 0f), new Vector2(-510f, 270f), new Color(0.75f, 0.30f, 1f, 0.5f), font);

            var touch = canvasGO.AddComponent<TouchControls>();
            touch.panel = panel;

            EnsurePauseMenu(canvasGO, circle, font);
        }

        // Pause/back overlay: an always-visible corner button that opens a dimmed panel with
        // Resume / Main Menu. Reachable via Esc too (handled by PauseMenu). Gives every playable
        // scene a path back to the main menu, including after the match is over.
        static void EnsurePauseMenu(GameObject canvasGO, Sprite circle, Font font)
        {
            if (Exists<PauseMenu>()) return;

            // small "MENU" button, top-left, clear of the centred score
            Button open = MakeButton(canvasGO.transform, circle, font, "PauseOpenButton", "MENU",
                new Vector2(0f, 1f), new Vector2(120f, -60f), new Vector2(160f, 70f),
                new Color(0f, 0f, 0f, 0.45f));

            // full-screen dimmed panel (starts hidden)
            var panel = new GameObject("PausePanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasGO.transform, false);
            Stretch(panel.GetComponent<RectTransform>());
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

            Text title = MakeText(panel.transform, "Title", font,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(800f, 100f), 64,
                TextAnchor.MiddleCenter);
            title.text = "Paused";

            Button resume = MakeButton(panel.transform, circle, font, "ResumeButton", "Resume",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(360f, 90f),
                new Color(0.30f, 0.55f, 1f, 0.85f));
            Button menu = MakeButton(panel.transform, circle, font, "MenuButton", "Main Menu",
                new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(360f, 90f),
                new Color(0.85f, 0.35f, 0.30f, 0.85f));

            var pm = canvasGO.AddComponent<PauseMenu>();
            pm.panel = panel;
            pm.openButton = open;
            pm.resumeButton = resume;
            pm.menuButton = menu;
        }

        // Reusable UI button: an Image background with a centred Text label and a Button component.
        static Button MakeButton(Transform parent, Sprite sprite, Font font, string name, string label,
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
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            go.GetComponent<Button>().targetGraphic = img;

            Text t = MakeText(go.transform, "Label", font,
                new Vector2(0.5f, 0.5f), Vector2.zero, size, 30, TextAnchor.MiddleCenter);
            t.text = label;
            t.raycastTarget = false;

            return go.GetComponent<Button>();
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

        public const string UIActionsPath = "Assets/InputSystem_Actions.inputactions";

        /// <summary>
        /// Create the EventSystem + UI input module if the scene lacks one. Shared by every scene
        /// builder so the (sometimes fragile) input wiring lives in one place. Idempotent.
        /// </summary>
        public static void EnsureEventSystem()
        {
            if (Exists<EventSystem>()) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();
            WireUIActions(module);
        }

        // InputSystemUIInputModule.AssignDefaultActions() throws on some Input System package
        // versions ("Action 'UI/Point' must be part of an InputActionAsset"), because the default
        // actions it builds aren't backed by an asset. So wire the module from the project's saved
        // actions asset — using its persistent InputActionReference sub-assets, which serialise
        // cleanly into the scene — and only fall back to the default actions if it's missing.
        static void WireUIActions(InputSystemUIInputModule module)
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(UIActionsPath);
            if (asset == null)
            {
                module.AssignDefaultActions();
                return;
            }

            module.actionsAsset = asset;

            var refs = new Dictionary<string, InputActionReference>();
            foreach (var obj in AssetDatabase.LoadAllAssetRepresentationsAtPath(UIActionsPath))
                if (obj is InputActionReference r && r.action != null && r.action.actionMap != null)
                    refs[$"{r.action.actionMap.name}/{r.action.name}"] = r;

            module.move = Ref(refs, "UI/Navigate");
            module.submit = Ref(refs, "UI/Submit");
            module.cancel = Ref(refs, "UI/Cancel");
            module.point = Ref(refs, "UI/Point");
            module.leftClick = Ref(refs, "UI/Click");
            module.rightClick = Ref(refs, "UI/RightClick");
            module.middleClick = Ref(refs, "UI/MiddleClick");
            module.scrollWheel = Ref(refs, "UI/ScrollWheel");
            module.trackedDevicePosition = Ref(refs, "UI/TrackedDevicePosition");
            module.trackedDeviceOrientation = Ref(refs, "UI/TrackedDeviceOrientation");
        }

        static InputActionReference Ref(Dictionary<string, InputActionReference> refs, string path)
            => refs.TryGetValue(path, out var r) ? r : null;

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

                        float shade = Mathf.Lerp(1f, 0.72f, rn);      // darken toward the rim
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
            string path = $"{MaterialDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var mat = new Material(shader) { name = name };
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        static string AbsPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
