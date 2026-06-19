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
        /// Launch a campaign match. For now this reuses the beach arena; once the campaign ladder
        /// exists this will pick the stage's arena/opponents from the loaded <see cref="CampaignSave"/>.
        /// </summary>
        public static void LoadCampaignMatch()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(BeachArena);
        }
    }
}
