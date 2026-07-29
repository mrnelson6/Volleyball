using UnityEditor;
using UnityEngine;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// One-click full regeneration of the world-tour game: bake every animal's sprites, build
    /// the beach arena, every themed/regional arena, and the main menu. This is the "fresh
    /// clone → playable game" button, and the safe way to propagate roster or theme changes
    /// into every scene. Also runnable headless:
    /// <c>Unity -batchmode -executeMethod Volleyball.EditorTools.WorldTourBuildAll.Build</c>.
    /// </summary>
    public static class WorldTourBuildAll
    {
        [MenuItem("Volleyball/Build World Tour (Everything)", priority = 19)]
        public static void Build()
        {
            CharacterArt.BakeRoster();
            NetworkKit.BuildBootstrapPrefab();          // NetworkManager prefab (online flow only)
            VolleyballLevelBuilder.BuildArenaScene();   // BeachArena (also Quick Play default)
            ThemedLevelBuilder.BuildAll();              // 6 fantasy + 8 regional courts
            MainMenuSceneBuilder.Build();               // menu last, so it stays scene 0
            // must run after every scene is SAVED — builders bake hash 0 into unsaved scenes
            NetworkKit.RefreshAllSceneNetworkHashes();
            Debug.Log("[Volleyball] World tour build complete: sprites, bootstrap, 15 arenas, main menu.");
        }
    }
}
