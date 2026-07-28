using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Carries the pre-match choices from the menu into the next loaded arena (statics survive
    /// scene loads). Quick Play fills the character/randomize fields; the campaign fills the
    /// explicit team casts and AI tuning. <see cref="MatchManager"/> applies it on Start and the
    /// players re-dress themselves. When inactive (scene played directly from the editor) the
    /// scene's built-in characters are kept.
    /// </summary>
    public static class MatchSetup
    {
        /// <summary>The human's chosen character id, or null to keep the scene default.</summary>
        public static string humanCharacterId;

        /// <summary>Give the human's AI teammate a randomly drawn roster character. Off in
        /// Quick Play — your side stays the protagonist duo unless asked otherwise.</summary>
        public static bool randomizeTeammate;

        /// <summary>Give each opposing AI a randomly drawn roster character.</summary>
        public static bool randomizeOpponents;

        // ---- campaign ----

        /// <summary>True while a world-tour match is running: results write to the save and
        /// the Hit key routes to the next match instead of an in-place restart.</summary>
        public static bool isCampaign;

        /// <summary>Explicit cast for team A: [human, teammate]. Null = not set.</summary>
        public static string[] teamAIds;

        /// <summary>Explicit cast for team B: [opponent on x&lt;0, opponent on x&gt;0]. Null = not set.</summary>
        public static string[] teamBIds;

        /// <summary>Shown on the HUD: "Sunny Savanna — Match 2/3 vs Stripe Sprinters".</summary>
        public static string matchLabel;

        /// <summary>Per-match AI contact-error multiplier; &lt;= 0 = use the GameConfig value.</summary>
        public static float aiErrorMult = -1f;

        /// <summary>Per-match scale on the AI reaction window; &lt;= 0 = unscaled.</summary>
        public static float aiReactionScale = -1f;

        public static void Clear()
        {
            humanCharacterId = null;
            randomizeTeammate = false;
            randomizeOpponents = false;
            isCampaign = false;
            teamAIds = null;
            teamBIds = null;
            matchLabel = null;
            aiErrorMult = -1f;
            aiReactionScale = -1f;
        }
    }
}
