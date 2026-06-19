using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// Level-designer entry point. Lets you drop the playable volleyball "keys" into <b>any</b>
    /// open scene (so a custom-dressed scene becomes playable without hand-wiring), and offers a
    /// one-click "Sunset Beach Arena" showcase that pairs the keys with the procedural
    /// <see cref="ArenaDecorator"/> environment.
    ///
    /// This is intentionally separate from <c>PrototypeSceneBuilder</c>: the prototype builder
    /// owns the from-scratch <c>Game.unity</c>, while this composes reusable pieces additively.
    /// </summary>
    public class VolleyballLevelBuilder : EditorWindow
    {
        const string ArenaScenePath = "Assets/Scenes/BeachArena.unity";

        bool _buildCamera = true;
        bool _buildLight = true;
        bool _buildUI = true;

        [MenuItem("Volleyball/Level Designer", priority = 20)]
        public static void Open()
        {
            var win = GetWindow<VolleyballLevelBuilder>(false, "Volleyball Levels", true);
            win.minSize = new Vector2(340f, 280f);
            win.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Make any scene playable", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Drops the core gameplay 'keys' (ground + net, ball, players, MatchManager, " +
                "input and HUD) into the CURRENT scene. Safe to re-run: pieces that already " +
                "exist are skipped, so your decorations are never touched.\n\n" +
                "Gameplay is locked to a court centred on the world origin — build your set " +
                "dressing around (0,0,0).",
                MessageType.Info);

            _buildCamera = EditorGUILayout.ToggleLeft("Add camera (if the scene has none)", _buildCamera);
            _buildLight = EditorGUILayout.ToggleLeft("Add directional light (if the scene has none)", _buildLight);
            _buildUI = EditorGUILayout.ToggleLeft("Add HUD + touch controls", _buildUI);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Drop In Playable Court (Active Scene)", GUILayout.Height(34)))
                DropInToActiveScene(new CourtKit.Options
                {
                    buildCamera = _buildCamera,
                    buildLight = _buildLight,
                    buildUI = _buildUI,
                });

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Showcase", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates a brand-new scene: a golden-hour beach arena (sunset sky, ocean, " +
                "grandstands + crowd, palms, torches, umbrellas) with a fully playable court " +
                "dropped in. Saved to " + ArenaScenePath + ".",
                MessageType.None);

            if (GUILayout.Button("Build Sunset Beach Arena Scene", GUILayout.Height(34)))
                BuildArenaScene();
        }

        // ----------------------------------------------------------------- menu shortcuts

        [MenuItem("Volleyball/Drop In Playable Court (Active Scene)", priority = 21)]
        public static void DropInMenu()
            => DropInToActiveScene(new CourtKit.Options());

        [MenuItem("Volleyball/Build Sunset Beach Arena Scene", priority = 22)]
        public static void BuildArenaScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // environment + camera + sun first, then the playable keys (which see the existing
            // camera/light and skip their own)
            ArenaDecorator.BuildSunsetBeachArena();
            CourtKit.DropInCourt(new CourtKit.Options
            {
                buildCamera = false,
                buildLight = false,
                buildUI = true,
            });

            Directory.CreateDirectory(Path.GetDirectoryName(AbsPath(ArenaScenePath)));
            EditorSceneManager.SaveScene(scene, ArenaScenePath);
            AddSceneToBuildSettings(ArenaScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ArenaScenePath);

            Debug.Log("[Volleyball] Sunset Beach Arena built at " + ArenaScenePath + ". Press Play.");
            EditorUtility.DisplayDialog("Volleyball",
                "Sunset Beach Arena built and opened.\nPress Play to test.", "OK");
        }

        // ----------------------------------------------------------------- helpers

        static void DropInToActiveScene(CourtKit.Options opt)
        {
            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid())
            {
                EditorUtility.DisplayDialog("Volleyball", "Open a scene first.", "OK");
                return;
            }

            CourtKit.DropInCourt(opt);

            EditorSceneManager.MarkSceneDirty(active);
            Debug.Log($"[Volleyball] Playable court dropped into '{active.name}'. " +
                      "Save the scene to keep it.");
        }

        static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == scenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static string AbsPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
