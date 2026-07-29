using UnityEngine;
using UnityEngine.UI;

namespace Volleyball
{
    /// <summary>
    /// The Online entry screen: Host a match (creates a private Relay session and opens the
    /// lobby with a shareable code) or Join by code. Built by MainMenuSceneBuilder; all it
    /// does is drive <see cref="NetworkSessionController"/> and hand off to the lobby panel.
    /// </summary>
    public class OnlinePanel : MonoBehaviour
    {
        public Button hostButton;
        public Button joinButton;
        public Button backButton;
        public InputField codeInput;
        public Text statusText;
        public GameObject lobbyPanel;

        bool _awaitingLobby; // joined the session, waiting for the lobby object to replicate

        void Start()
        {
            if (hostButton != null) hostButton.onClick.AddListener(Host);
            if (joinButton != null) joinButton.onClick.AddListener(Join);
            if (backButton != null) backButton.onClick.AddListener(() => gameObject.SetActive(false));
        }

        void OnEnable()
        {
            // surface an involuntary disconnect ("host left") from the previous session once
            SetStatus(NetworkSessionController.DisconnectNotice ?? "");
            NetworkSessionController.DisconnectNotice = null;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (statusText != null && statusText.text == "")
                SetStatus("Tip: browser tabs pause in the background — a desktop player makes the steadiest host.");
#endif
            OnlineLobbyState.Changed += OnLobbyChanged;
        }

        void OnDisable() => OnlineLobbyState.Changed -= OnLobbyChanged;

        void SetStatus(string s) { if (statusText != null) statusText.text = s; }

        void SetButtons(bool on)
        {
            if (hostButton != null) hostButton.interactable = on;
            if (joinButton != null) joinButton.interactable = on;
        }

        async void Host()
        {
            var nm = NetworkBootstrap.Ensure();
            if (nm == null || NetworkSessionController.Instance == null) return;

            SetButtons(false);
            SetStatus("Creating session…");
            bool ok = await NetworkSessionController.Instance.HostAsync();
            SetButtons(true);
            if (!ok)
            {
                SetStatus(NetworkSessionController.Instance.LastError ?? "Could not host.");
                return;
            }
            OpenLobby();
        }

        async void Join()
        {
            var nm = NetworkBootstrap.Ensure();
            if (nm == null || NetworkSessionController.Instance == null) return;

            SetButtons(false);
            SetStatus("Joining…");
            bool ok = await NetworkSessionController.Instance.JoinByCodeAsync(
                codeInput != null ? codeInput.text : "");
            if (!ok)
            {
                SetButtons(true);
                SetStatus(NetworkSessionController.Instance.LastError ?? "Could not join.");
                return;
            }

            // connected — the lobby object replicates in a moment; OnLobbyChanged flips us over
            SetStatus("Connected — entering lobby…");
            _awaitingLobby = true;
            if (OnlineLobbyState.Instance != null) OpenLobby();
        }

        void OnLobbyChanged()
        {
            if (_awaitingLobby && OnlineLobbyState.Instance != null) OpenLobby();
        }

        void OpenLobby()
        {
            _awaitingLobby = false;
            SetButtons(true);
            SetStatus("");
            if (lobbyPanel != null)
            {
                lobbyPanel.SetActive(true);
                gameObject.SetActive(false);
            }
        }
    }
}
