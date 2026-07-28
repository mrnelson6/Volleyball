using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Carries the pre-match choices from the menu into the next loaded arena (statics survive
    /// scene loads). The menu's character select fills it in; <see cref="MatchManager"/> applies
    /// it on Start and the players re-dress themselves. When inactive (scene played directly
    /// from the editor, or a campaign match) the scene's built-in characters are kept.
    /// </summary>
    public static class MatchSetup
    {
        /// <summary>The human's chosen character id, or null to keep the scene default.</summary>
        public static string humanCharacterId;

        /// <summary>Give each AI player a randomly drawn roster character.</summary>
        public static bool randomizeAI;

        public static void Clear()
        {
            humanCharacterId = null;
            randomizeAI = false;
        }
    }
}
