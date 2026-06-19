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
        [Header("Buttons")]
        public Button quickPlayButton;
        public Button campaignButton;
        public Button settingsButton;
        public Button quitButton;

        [Header("Panels (hidden until opened)")]
        public GameObject settingsPanel;
        public GameObject campaignPanel;

        void Start()
        {
            if (quickPlayButton != null) quickPlayButton.onClick.AddListener(SceneFlow.LoadQuickPlay);
            if (campaignButton != null) campaignButton.onClick.AddListener(() => Show(campaignPanel));
            if (settingsButton != null) settingsButton.onClick.AddListener(() => Show(settingsPanel));
            if (quitButton != null) quitButton.onClick.AddListener(Quit);

            // panels start closed
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (campaignPanel != null) campaignPanel.SetActive(false);
        }

        void Show(GameObject panel)
        {
            if (settingsPanel != null) settingsPanel.SetActive(panel == settingsPanel);
            if (campaignPanel != null) campaignPanel.SetActive(panel == campaignPanel);
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
