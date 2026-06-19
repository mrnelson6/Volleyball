using UnityEngine;
using UnityEngine.UI;

namespace Volleyball
{
    /// <summary>
    /// Stub campaign screen. The save plumbing is real (<see cref="SaveSystem"/>) but the mode
    /// itself is not built yet, so both New Game and Continue currently launch a beach match.
    /// "Continue" is only interactable when a save exists.
    /// </summary>
    public class CampaignPanel : MonoBehaviour
    {
        public Button newGameButton;
        public Button continueButton;
        public Button backButton;
        public Text statusLabel; // shows save state

        void Awake()
        {
            if (newGameButton != null) newGameButton.onClick.AddListener(NewGame);
            if (continueButton != null) continueButton.onClick.AddListener(Continue);
            if (backButton != null) backButton.onClick.AddListener(Close);
        }

        void OnEnable() => Refresh();

        void Refresh()
        {
            bool hasSave = SaveSystem.Exists();
            if (continueButton != null) continueButton.interactable = hasSave;

            if (statusLabel != null)
            {
                if (!hasSave)
                {
                    statusLabel.text = "No campaign yet — start a New Game.";
                }
                else
                {
                    var s = SaveSystem.Load();
                    statusLabel.text = s != null
                        ? $"Saved campaign — stage {s.stage + 1},  W{s.matchesWon} / L{s.matchesLost}"
                        : "Saved campaign found.";
                }
            }
        }

        void NewGame()
        {
            SaveSystem.NewGame();
            SceneFlow.LoadCampaignMatch();
        }

        void Continue()
        {
            if (!SaveSystem.Exists()) return;
            SceneFlow.LoadCampaignMatch();
        }

        void Close() => gameObject.SetActive(false);
    }
}
