using System.IO;
using UnityEditor;
using UnityEngine;

namespace Volleyball.EditorTools
{
    /// <summary>Creates the editable GameConfig asset under Assets/Resources.</summary>
    public static class GameConfigCreator
    {
        const string Dir = "Assets/Resources";
        const string Path = "Assets/Resources/GameConfig.asset";

        [MenuItem("Volleyball/Create Game Config")]
        public static void Create()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameConfig>(Path);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                Debug.Log("[Volleyball] GameConfig already exists at " + Path + " (selected it).");
                return;
            }

            EnsureExists();
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(Path);
            Selection.activeObject = cfg;
            EditorGUIUtility.PingObject(cfg);
            Debug.Log("[Volleyball] Created GameConfig at " + Path);
        }

        /// <summary>Create the asset if it doesn't exist yet (safe to call repeatedly).</summary>
        public static void EnsureExists()
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(Path) != null) return;
            if (!Directory.Exists(Dir)) { Directory.CreateDirectory(Dir); AssetDatabase.Refresh(); }
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(cfg, Path);
            AssetDatabase.SaveAssets();
        }
    }
}
