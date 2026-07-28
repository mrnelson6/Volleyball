using UnityEngine;
using UnityEngine.UI;

namespace Volleyball
{
    /// <summary>
    /// Drives the main menu: the four top-level buttons and the two slide-in panels (Settings,
    /// Campaign). References are wired by the editor scene builder
    /// (<c>MainMenuSceneBuilder</c>); nothing here assumes a particular layout.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        /// <summary>Set before loading the menu scene to land straight on the tour board —
        /// used when a campaign match sends the player home to see the ladder advance.</summary>
        public static bool openCampaignOnLoad;

        [Header("Buttons")]
        public Button quickPlayButton;
        public Button campaignButton;
        public Button settingsButton;
        public Button quitButton;

        [Header("Panels (hidden until opened)")]
        public GameObject settingsPanel;
        public GameObject campaignPanel;
        public GameObject characterSelectPanel;

        [Header("Home screen (title + top-level buttons), hidden while any panel is open")]
        public GameObject homeRoot;

        void Start()
        {
            // Quick Play goes through character select; a menu scene built before the panel
            // existed falls back to launching the match directly.
            if (quickPlayButton != null)
                quickPlayButton.onClick.AddListener(() =>
                {
                    if (characterSelectPanel != null) Show(characterSelectPanel);
                    else SceneFlow.LoadQuickPlay();
                });
            if (campaignButton != null) campaignButton.onClick.AddListener(() => Show(campaignPanel));
            if (settingsButton != null) settingsButton.onClick.AddListener(() => Show(settingsPanel));
            if (quitButton != null) quitButton.onClick.AddListener(Quit);

            // panels start closed — unless a campaign match sent us home to the tour board
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (campaignPanel != null) campaignPanel.SetActive(openCampaignOnLoad);
            if (characterSelectPanel != null) characterSelectPanel.SetActive(false);
            openCampaignOnLoad = false;
        }

        void Show(GameObject panel)
        {
            if (settingsPanel != null) settingsPanel.SetActive(panel == settingsPanel);
            if (campaignPanel != null) campaignPanel.SetActive(panel == campaignPanel);
            if (characterSelectPanel != null) characterSelectPanel.SetActive(panel == characterSelectPanel);
        }

        // Panels close themselves via their own Back buttons (plain SetActive(false)), so the
        // home screen's visibility is polled rather than event-wired: hidden the moment any
        // panel is open, back the moment none are.
        void Update()
        {
            if (homeRoot == null) return;
            bool anyPanelOpen = (settingsPanel != null && settingsPanel.activeSelf)
                                || (campaignPanel != null && campaignPanel.activeSelf)
                                || (characterSelectPanel != null && characterSelectPanel.activeSelf);
            if (homeRoot.activeSelf == anyPanelOpen) homeRoot.SetActive(!anyPanelOpen);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
