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
        /// Every playable venue for Quick Play's venue cycler (beach first, then the world-tour
        /// regional courts, then the fantasy arenas). Campaign matches don't use this list —
        /// they load each region's <see cref="RegionDef.sceneName"/> directly.
        /// </summary>
        public static readonly string[] Arenas =
        {
            BeachArena,
            "SavannaArena", "AmazonArena", "OutbackArena", "HimalayaArena",
            "ForestArena", "SaharaArena", "RockiesArena", "ArcticArena",
            VolcanoArena, LunarArena, AtlantisArena, SkyArena, GraveyardArena, NeonArena,
        };

        /// <summary>Human-readable names parallel to <see cref="Arenas"/>, for menus/HUD.</summary>
        public static readonly string[] ArenaNames =
        {
            "Sunset Beach",
            "Sunny Savanna", "Amazon Rainforest", "Australian Outback", "Himalayan Peaks",
            "Black Forest", "Sahara Dunes", "Rocky Mountains", "Polar Ice",
            "Volcano Rim", "Lunar Base", "Atlantis Deep",
            "Cloud Kingdom", "Haunted Graveyard", "Neon Rooftop",
        };

        /// <summary>Load an arena by its index into <see cref="Arenas"/> (clamped, wraps safely).</summary>
        public static void LoadArena(int index)
        {
            Time.timeScale = 1f;
            int i = ((index % Arenas.Length) + Arenas.Length) % Arenas.Length;
            SceneManager.LoadScene(Arenas[i]);
        }

        /// <summary>Return to the main menu. Resets time scale in case we left a paused match,
        /// and tears down any live online session first — leaving a match means leaving the
        /// session (the server sees the disconnect; no lingering ghost connection).</summary>
        public static void LoadMenu()
        {
            Time.timeScale = 1f;
            if (NetworkSession.IsOnline) NetworkSessionController.LeaveEverything();
            SceneManager.LoadScene(MainMenu);
        }

        /// <summary>Quick Play — drop into a match at any venue from <see cref="Arenas"/>
        /// (default: the beach). With a character id the human plays as that character and the
        /// two opposing AIs draw random roster characters (the teammate stays your usual
        /// partner); with null the scene's built-in characters are kept. Regional venues keep
        /// their environment quirks (wind, thin air…) in Quick Play too. The random draws
        /// happen HERE, so the config carries only concrete ids — online, the host draws once
        /// and every client dresses the same match.</summary>
        public static void LoadQuickPlay(string characterId = null, int arenaIndex = 0)
        {
            MatchSetup.Clear();
            if (characterId != null)
            {
                var pool = new System.Collections.Generic.List<CharacterDef>(CharacterRoster.All);
                pool.Remove(CharacterRoster.Get(characterId));
                string opp1 = DrawFrom(pool);
                string opp2 = DrawFrom(pool);
                MatchSetup.Current = MatchConfig.Solo(
                    characterId, CharacterRoster.TeammateId, opp1, opp2);
            }
            LoadArena(arenaIndex);
        }

        static string DrawFrom(System.Collections.Generic.List<CharacterDef> pool)
        {
            if (pool.Count == 0) pool.AddRange(CharacterRoster.All);
            CharacterDef draw = pool[Random.Range(0, pool.Count)];
            pool.Remove(draw);
            return draw.id;
        }

        /// <summary>
        /// Launch the next world-tour match from the save: the current region picks the court
        /// scene and environment, the current tournament match picks the opponent duo and AI
        /// difficulty, and the human always plays the protagonist duo. Regions whose courts
        /// aren't built yet fall back to the beach so the campaign stays playable mid-development.
        /// </summary>
        public static void LoadCampaignMatch()
        {
            CampaignSave save = SaveSystem.Load() ?? SaveSystem.NewGame();
            RegionDef region = RegionRoster.Get(save.regionIndex);
            int mi = Mathf.Clamp(save.matchIndex, 0, region.matches.Length - 1);
            MatchDef match = region.matches[mi];

            MatchConfig cfg = MatchConfig.Solo(
                CharacterRoster.ProtagonistId, CharacterRoster.TeammateId, match.opp1Id, match.opp2Id);
            cfg.isCampaign = true;
            cfg.aiErrorMult = match.aiErrorMult;
            cfg.aiReactionScale = match.aiReactionScale;
            cfg.matchLabel =
                $"{region.displayName} — Match {mi + 1}/{region.matches.Length} vs {match.teamName}";
            MatchSetup.Current = cfg;

            Time.timeScale = 1f;
            string scene = Application.CanStreamedLevelBeLoaded(region.sceneName)
                ? region.sceneName : BeachArena;
            SceneManager.LoadScene(scene);
        }
    }
}
