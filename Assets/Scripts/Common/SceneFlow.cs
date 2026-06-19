using UnityEngine;
using UnityEngine.SceneManagement;

namespace Volleyball
{
    /// <summary>
    /// Single place that knows the scene names and the transitions between them, so screens
    /// don't hard-code <c>SceneManager.LoadScene</c> calls. Scene names must match the entries
    /// in Build Settings (set up by the editor scene builders).
    /// </summary>
    public static class SceneFlow
    {
        public const string MainMenu = "MainMenu";
        public const string BeachArena = "BeachArena";

        // Outlandish themed arenas, built by the editor's ThemedLevelBuilder. Each constant is the
        // scene name (matching the .unity file and its Build Settings entry).
        public const string VolcanoArena = "VolcanoArena";
        public const string LunarArena = "LunarArena";
        public const string AtlantisArena = "AtlantisArena";
        public const string SkyArena = "SkyArena";
        public const string GraveyardArena = "GraveyardArena";
        public const string NeonArena = "NeonArena";

        /// <summary>
        /// Every playable arena, in ladder order (beach first). The campaign walks this list and a
        /// future level-select can drive its buttons from it; <see cref="LoadArena"/> loads by index.
        /// </summary>
        public static readonly string[] Arenas =
        {
            BeachArena, VolcanoArena, LunarArena, AtlantisArena, SkyArena, GraveyardArena, NeonArena,
        };

        /// <summary>Human-readable names parallel to <see cref="Arenas"/>, for menus/HUD.</summary>
        public static readonly string[] ArenaNames =
        {
            "Sunset Beach", "Volcano Rim", "Lunar Base", "Atlantis Deep",
            "Cloud Kingdom", "Haunted Graveyard", "Neon Rooftop",
        };

        /// <summary>Load an arena by its index into <see cref="Arenas"/> (clamped, wraps safely).</summary>
        public static void LoadArena(int index)
        {
            Time.timeScale = 1f;
            int i = ((index % Arenas.Length) + Arenas.Length) % Arenas.Length;
            SceneManager.LoadScene(Arenas[i]);
        }

        /// <summary>Return to the main menu. Resets time scale in case we left a paused match.</summary>
        public static void LoadMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(MainMenu);
        }

        /// <summary>Quick Play — drop straight into a beach match.</summary>
        public static void LoadQuickPlay()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(BeachArena);
        }

        /// <summary>
        /// Launch a campaign match: the stage index in the saved <see cref="CampaignSave"/> selects
        /// the arena from the <see cref="Arenas"/> ladder (beach → … → neon, then wrapping). With no
        /// save present it falls back to the first arena.
        /// </summary>
        public static void LoadCampaignMatch()
        {
            int stage = SaveSystem.Load()?.stage ?? 0;
            LoadArena(stage);
        }
    }
}
