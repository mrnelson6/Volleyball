using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Volleyball
{
    /// <summary>
    /// The pre-match lobby: the join code to share, four slot cards (claim one, pick your
    /// animal on it, ready up — unclaimed slots play as AI, and the host can re-cast those
    /// too), the host's arena choice, and Start. Pure view over
    /// <see cref="OnlineLobbyState"/>: every interaction is an RPC intent, every render is a
    /// full refresh from the replicated list.
    /// </summary>
    public class OnlineLobbyPanel : MonoBehaviour
    {
        [System.Serializable]
        public class SlotCard
        {
            public Button claimButton;     // the card background — click to claim
            public Text title;             // "TEAM A — LEFT" etc
            public Text occupantText;      // "AI (open)" / "YOU" / "P2"
            public Text characterText;
            public Image portrait;
            public Button prevCharButton;
            public Button nextCharButton;
        }

        public SlotCard[] cards = new SlotCard[4];
        public Text codeText;
        public Text arenaText;
        public Text statusText;
        public Button readyButton;
        public Text readyButtonLabel;
        public Button arenaPrevButton;
        public Button arenaNextButton;
        public Button startButton;
        public Button leaveButton;

        static readonly Color[] SlotJerseys =
            { PlayerColors.Human, PlayerColors.Mate, PlayerColors.Opp1, PlayerColors.Opp2 };

        void Start()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                var c = cards[i];
                if (c == null) continue;
                if (c.claimButton != null)
                    c.claimButton.onClick.AddListener(() => Lobby?.ClaimSlotRpc(index));
                if (c.prevCharButton != null)
                    c.prevCharButton.onClick.AddListener(() => CycleCharacter(index, -1));
                if (c.nextCharButton != null)
                    c.nextCharButton.onClick.AddListener(() => CycleCharacter(index, 1));
            }

            if (readyButton != null) readyButton.onClick.AddListener(ToggleReady);
            if (arenaPrevButton != null) arenaPrevButton.onClick.AddListener(() => Lobby?.CycleArenaHost(-1));
            if (arenaNextButton != null) arenaNextButton.onClick.AddListener(() => Lobby?.CycleArenaHost(1));
            if (startButton != null) startButton.onClick.AddListener(() => Lobby?.StartMatchHost());
            if (leaveButton != null) leaveButton.onClick.AddListener(Leave);
        }

        static OnlineLobbyState Lobby => OnlineLobbyState.Instance;
        static bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        void OnEnable()
        {
            OnlineLobbyState.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => OnlineLobbyState.Changed -= Refresh;

        async void Leave()
        {
            gameObject.SetActive(false);
            if (NetworkSessionController.Instance != null)
                await NetworkSessionController.Instance.LeaveAsync();
        }

        void ToggleReady()
        {
            if (Lobby == null) return;
            int mine = Lobby.MySlot;
            if (mine < 0) return;
            Lobby.SetReadyRpc(!Lobby.GetSlot(mine).ready);
        }

        void CycleCharacter(int slotIndex, int dir)
        {
            if (Lobby == null || slotIndex >= Lobby.SlotCount) return;
            var slot = Lobby.GetSlot(slotIndex);

            var all = CharacterRoster.All;
            int at = 0;
            for (int i = 0; i < all.Length; i++)
                if (all[i].id == slot.characterId.ToString()) { at = i; break; }
            int next = ((at + dir) % all.Length + all.Length) % all.Length;
            Lobby.SetCharacterRpc(slotIndex, all[next].id);
        }

        void Refresh()
        {
            var lobby = Lobby;
            if (lobby == null || !gameObject.activeInHierarchy) return;

            ulong myId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
            int mySlot = lobby.MySlot;

            if (codeText != null)
                codeText.text = IsHost
                    ? $"JOIN CODE:  {NetworkSessionController.Instance?.JoinCode ?? "…"}"
                    : "";

            for (int i = 0; i < cards.Length && i < lobby.SlotCount; i++)
            {
                var c = cards[i];
                if (c == null) continue;
                var slot = lobby.GetSlot(i);
                bool mine = !slot.IsOpen && slot.clientId == myId;
                CharacterDef ch = CharacterRoster.Get(slot.characterId.ToString());

                if (c.occupantText != null)
                    c.occupantText.text = slot.IsOpen ? "AI  (tap to claim)"
                                        : mine ? (slot.ready || i == 0 && IsHost ? "YOU  ✓" : "YOU")
                                               : $"P{slot.clientId}" + (slot.ready ? "  ✓" : "");
                if (c.characterText != null) c.characterText.text = ch.displayName;
                if (c.portrait != null)
                {
                    Sprite[] frames = CharacterSprites.LoadFrames(SlotJerseys[i], ch);
                    if (frames != null && frames.Length > 0 && frames[0] != null)
                    {
                        c.portrait.sprite = frames[0]; // the idle frame as the portrait
                        c.portrait.preserveAspect = true;
                        c.portrait.enabled = true;
                    }
                    else c.portrait.enabled = false;
                }

                if (c.claimButton != null) c.claimButton.interactable = slot.IsOpen;
                bool mayEditCharacter = mine || (IsHost && slot.IsOpen);
                if (c.prevCharButton != null) c.prevCharButton.gameObject.SetActive(mayEditCharacter);
                if (c.nextCharButton != null) c.nextCharButton.gameObject.SetActive(mayEditCharacter);
            }

            if (arenaText != null)
                arenaText.text = SceneFlow.ArenaNames[
                    Mathf.Clamp(lobby.ArenaIndex.Value, 0, SceneFlow.ArenaNames.Length - 1)];
            if (arenaPrevButton != null) arenaPrevButton.gameObject.SetActive(IsHost);
            if (arenaNextButton != null) arenaNextButton.gameObject.SetActive(IsHost);

            // the host "readies" by pressing Start; guests toggle Ready
            if (readyButton != null)
            {
                bool showReady = !IsHost && mySlot >= 0;
                readyButton.gameObject.SetActive(showReady);
                if (showReady && readyButtonLabel != null)
                    readyButtonLabel.text = lobby.GetSlot(mySlot).ready ? "UNREADY" : "READY";
            }

            if (startButton != null)
            {
                startButton.gameObject.SetActive(IsHost);
                startButton.interactable = lobby.AllHumansReady();
            }

            if (statusText != null)
                statusText.text = IsHost
                    ? (lobby.AllHumansReady() ? "All set — press Start!" : "Waiting for players to ready up…")
                    : (mySlot >= 0 ? "Ready up — the host starts the match." : "Tap a slot to claim it.");
        }
    }
}
