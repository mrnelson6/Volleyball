using UnityEngine;
using UnityEngine.UI;

namespace Volleyball
{
    /// <summary>
    /// The world-tour board: one card per region showing its state (locked / current stop with
    /// the next opponents / conquered), plus Play, New Game (with a confirm step when it would
    /// wipe progress) and Back. Cards are built by MainMenuSceneBuilder from
    /// <see cref="RegionRoster"/>; this refreshes them from the save whenever it opens.
    /// </summary>
    public class CampaignPanel : MonoBehaviour
    {
        [System.Serializable]
        public class RegionRow
        {
            public string regionId;
            public Image card;      // background, tinted by state
            public Text nameLabel;
            public Text stateLabel;
        }

        public RegionRow[] rows;
        public Text statusLabel;    // tour-wide summary line
        public Button playButton;
        public Text playButtonLabel;
        public Button newGameButton;
        public Text newGameButtonLabel;
        public Button backButton;

        static readonly Color CardLocked = new Color(1f, 1f, 1f, 0.05f);
        static readonly Color CardDone = new Color(0.22f, 0.65f, 0.35f, 0.35f);
        static readonly Color CardCurrent = new Color(0.30f, 0.65f, 1f, 0.45f);
        static readonly Color TextDim = new Color(1f, 1f, 1f, 0.45f);

        bool _confirmingNewGame;

        void Awake()
        {
            if (playButton != null) playButton.onClick.AddListener(Play);
            if (newGameButton != null) newGameButton.onClick.AddListener(NewGame);
            if (backButton != null) backButton.onClick.AddListener(Close);
        }

        void OnEnable()
        {
            _confirmingNewGame = false;
            Refresh();
        }

        void Refresh()
        {
            CampaignSave save = SaveSystem.Load();
            bool hasSave = save != null;
            int currentRegion = hasSave ? save.regionIndex : 0;

            if (statusLabel != null)
            {
                if (!hasSave)
                    statusLabel.text = "Your world tour awaits — two travellers, nine wild courts.";
                else if (save.tourComplete)
                    statusLabel.text = $"WORLD TOUR CHAMPIONS!   W{save.matchesWon} / L{save.matchesLost}";
                else
                    statusLabel.text = $"On tour — W{save.matchesWon} / L{save.matchesLost}";
            }

            if (playButtonLabel != null)
                playButtonLabel.text = !hasSave ? "Start the Tour"
                    : save.tourComplete ? "Replay the Grand Final"
                    : "Play Next Match";

            if (newGameButtonLabel != null && !_confirmingNewGame)
                newGameButtonLabel.text = "New Game";

            if (rows == null) return;
            for (int i = 0; i < rows.Length && i < RegionRoster.All.Length; i++)
            {
                RegionRow row = rows[i];
                RegionDef region = RegionRoster.All[i];
                bool done = hasSave && (i < currentRegion || save.tourComplete);
                bool current = hasSave ? (i == currentRegion && !save.tourComplete) : i == 0;

                if (row.card != null)
                    row.card.color = done ? CardDone : current ? CardCurrent : CardLocked;

                if (row.nameLabel != null)
                {
                    row.nameLabel.text = $"{i + 1}.  {region.displayName}";
                    row.nameLabel.color = done || current ? Color.white : TextDim;
                }

                if (row.stateLabel == null) continue;
                if (done)
                {
                    row.stateLabel.text = "CONQUERED";
                    row.stateLabel.color = new Color(0.55f, 1f, 0.65f, 0.9f);
                }
                else if (current)
                {
                    int mi = hasSave ? Mathf.Clamp(save.matchIndex, 0, region.matches.Length - 1) : 0;
                    MatchDef next = region.matches[mi];
                    string line = $"Match {mi + 1}/{region.matches.Length} — vs {next.teamName}";
                    if (hasSave && save.attemptsThisMatch > 0)
                        line += $"  (attempt {save.attemptsThisMatch + 1})";
                    if (!string.IsNullOrEmpty(region.env.bannerNote))
                        line += "\n" + region.env.bannerNote;
                    row.stateLabel.text = line;
                    row.stateLabel.color = Color.white;
                }
                else
                {
                    row.stateLabel.text = "Locked";
                    row.stateLabel.color = TextDim;
                }
            }
        }

        void Play()
        {
            if (!SaveSystem.Exists()) SaveSystem.NewGame();
            SceneFlow.LoadCampaignMatch();
        }

        void NewGame()
        {
            // restarting over an existing tour wipes it — ask once before doing it
            if (SaveSystem.Exists() && !_confirmingNewGame)
            {
                _confirmingNewGame = true;
                if (newGameButtonLabel != null) newGameButtonLabel.text = "Really restart?";
                return;
            }
            SaveSystem.NewGame();
            SceneFlow.LoadCampaignMatch();
        }

        void Close() => gameObject.SetActive(false);
    }
}
