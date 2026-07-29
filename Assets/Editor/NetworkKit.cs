using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// Builds <c>Assets/Resources/NetworkBootstrap.prefab</c>: the NetworkManager +
    /// UnityTransport pair a session needs, instantiated ONLY by the online flow
    /// (NetworkDebugHUD now, the Online menu in Phase 2). It lives in no scene — that is the
    /// offline-dormancy guarantee: solo and campaign play never even create a NetworkManager.
    /// Tick rate is locked to 50 to match the FixedUpdate simulation step.
    /// </summary>
    public static class NetworkKit
    {
        public const string PrefabPath = "Assets/Resources/NetworkBootstrap.prefab";
        public const string LobbyPrefabPath = "Assets/Resources/LobbyState.prefab";

        [MenuItem("Volleyball/Build Network Bootstrap", priority = 21)]
        public static void BuildBootstrapPrefab()
        {
            Directory.CreateDirectory(Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Assets", "Resources"));

            var go = new GameObject("NetworkBootstrap");
            try
            {
                var transport = go.AddComponent<UnityTransport>();
                var nm = go.AddComponent<NetworkManager>();
                nm.NetworkConfig.NetworkTransport = transport;
                nm.NetworkConfig.TickRate = 50; // matches the 0.02s FixedUpdate sim step
                nm.NetworkConfig.EnableSceneManagement = true;
                go.AddComponent<NetworkSessionController>(); // the only UGS touchpoint

                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                Debug.Log($"[Volleyball] Network bootstrap prefab saved at {PrefabPath}.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            // The lobby state: dynamically spawned by the host once a session exists, so it
            // is a registered network prefab (NetworkBootstrap.Ensure adds it at runtime on
            // every machine), NOT an in-scene object.
            var lobby = new GameObject("LobbyState");
            try
            {
                lobby.AddComponent<NetworkObject>();
                lobby.AddComponent<OnlineLobbyState>();
                PrefabUtility.SaveAsPrefabAsset(lobby, LobbyPrefabPath);
                Debug.Log($"[Volleyball] Lobby state prefab saved at {LobbyPrefabPath}.");
            }
            finally
            {
                Object.DestroyImmediate(lobby);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Regenerate every in-scene NetworkObject's GlobalObjectIdHash across all buildable
        /// scenes. The scene builders add NetworkObjects while the scene is still UNSAVED —
        /// no scene GUID exists yet, so NGO's OnValidate bakes hash 0 into every object, and
        /// clients then explode on join with "already contains the same GlobalObjectIdHash
        /// value 0". This pass runs AFTER the scenes are saved: reopening each one gives the
        /// objects a valid GlobalObjectId, NGO's own OnValidate (invoked via reflection — it
        /// is private) computes the real hash, and the scene is saved again. A verify step
        /// guarantees nonzero, per-scene-unique hashes with a deterministic FNV fallback if
        /// NGO's path ever declines to fill one in.
        /// </summary>
        [MenuItem("Volleyball/Refresh Network Scene Hashes", priority = 22)]
        public static void RefreshAllSceneNetworkHashes()
        {
            MethodInfo onValidate = typeof(NetworkObject).GetMethod(
                "OnValidate", BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var entry in EditorBuildSettings.scenes)
            {
                var scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Single);
                var objects = Object.FindObjectsByType<NetworkObject>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (objects.Length == 0) continue;

                var seen = new HashSet<uint>();
                int fallbacks = 0;
                foreach (var no in objects)
                {
                    onValidate?.Invoke(no, null);

                    var so = new SerializedObject(no);
                    so.Update();
                    SerializedProperty prop = so.FindProperty("GlobalObjectIdHash");
                    uint hash = (uint)prop.longValue;
                    if (hash == 0 || seen.Contains(hash))
                    {
                        hash = Fnv1a(GlobalObjectId.GetGlobalObjectIdSlow(no).ToString());
                        while (hash == 0 || seen.Contains(hash)) hash++;
                        prop.longValue = hash;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        fallbacks++;
                    }
                    seen.Add(hash);
                    EditorUtility.SetDirty(no);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[Volleyball] {Path.GetFileName(entry.path)}: " +
                          $"{objects.Length} NetworkObjects hashed" +
                          (fallbacks > 0 ? $" ({fallbacks} via FNV fallback!)" : "") + ".");
            }
        }

        static uint Fnv1a(string s)
        {
            uint hash = 2166136261u;
            foreach (char c in s)
            {
                hash ^= c;
                hash *= 16777619u;
            }
            return hash;
        }
    }
}
