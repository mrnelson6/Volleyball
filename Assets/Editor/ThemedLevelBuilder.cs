using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// The themed-arena counterpart to <see cref="VolleyballLevelBuilder"/>. It turns each
    /// <see cref="ThemedArenaDecorator.ArenaTheme"/> into a self-contained, playable scene — the
    /// procedural dressing plus the gameplay "keys" from <see cref="CourtKit"/> — and registers it in
    /// Build Settings so <see cref="SceneFlow"/> can load it by name.
    ///
    /// Use the window (Volleyball ▸ Themed Levels) to build one arena at a time, or the
    /// "Build All Themed Arenas" button/menu to regenerate the whole roster in one pass.
    /// </summary>
    public class ThemedLevelBuilder : EditorWindow
    {
        const string SceneDir = "Assets/Scenes";

        [MenuItem("Volleyball/Themed Levels", priority = 23)]
        public static void Open()
        {
            var win = GetWindow<ThemedLevelBuilder>(false, "Themed Levels", true);
            win.minSize = new Vector2(380f, 360f);
            win.Show();
        }

        Vector2 _scroll;

        void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Outlandish Volleyball Arenas", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Each button builds a brand-new scene: a fully-dressed themed arena with a playable " +
                "court dropped in, saved to " + SceneDir + "/<Name>.unity and added to Build Settings.\n\n" +
                "Re-running an arena overwrites its scene. Gameplay is always locked to the court at " +
                "the world origin; the theme only changes the dressing around it.",
                MessageType.Info);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Build All Themed Arenas", GUILayout.Height(34)))
                BuildAll();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Build individually", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var theme in ThemedArenaDecorator.Themes)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField(theme.displayName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(theme.blurb, EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button($"Build \"{theme.displayName}\"", GUILayout.Height(26)))
                    BuildOne(theme);
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
            EditorGUILayout.EndScrollView();
        }

        // ----------------------------------------------------------------- menu shortcuts

        [MenuItem("Volleyball/Build All Themed Arenas", priority = 24)]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string lastPath = null;
            foreach (var theme in ThemedArenaDecorator.Themes)
                lastPath = BuildScene(theme);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (lastPath != null) EditorSceneManager.OpenScene(lastPath);

            Debug.Log($"[Volleyball] Built {ThemedArenaDecorator.Themes.Length} themed arenas in {SceneDir}.");
            EditorUtility.DisplayDialog("Volleyball",
                $"Built {ThemedArenaDecorator.Themes.Length} themed arenas and added them to Build Settings.\n" +
                "Open any from " + SceneDir + " and press Play.", "OK");
        }

        // ----------------------------------------------------------------- builders

        static void BuildOne(ThemedArenaDecorator.ArenaTheme theme)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            string path = BuildScene(theme);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(path);

            Debug.Log($"[Volleyball] {theme.displayName} arena built at {path}. Press Play.");
            EditorUtility.DisplayDialog("Volleyball",
                $"{theme.displayName} built and opened.\nPress Play to test.", "OK");
        }

        /// <summary>Create, dress, populate and save one themed scene. Returns the asset path.</summary>
        static string BuildScene(ThemedArenaDecorator.ArenaTheme theme)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // environment + camera + sun first, then the playable keys (which see the existing
            // camera/light and skip their own)
            ThemedArenaDecorator.BuildArena(theme);
            CourtKit.DropInCourt(new CourtKit.Options
            {
                buildCamera = false,
                buildLight = false,
                buildUI = true,
            });

            string path = $"{SceneDir}/{theme.key}.unity";
            Directory.CreateDirectory(Path.GetDirectoryName(AbsPath(path)));
            EditorSceneManager.SaveScene(scene, path);
            AddSceneToBuildSettings(path);
            return path;
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
