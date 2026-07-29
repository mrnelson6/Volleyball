using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Volleyball
{
    /// <summary>
    /// The Online entry screen, three doors: Server Match (ask the game box for a fresh
    /// dedicated server and hop straight into its lobby), Host (the session runs on THIS
    /// machine), or Join by code. Built by MainMenuSceneBuilder; all it does is drive
    /// <see cref="NetworkSessionController"/> and hand off to the lobby panel.
    /// </summary>
    public class OnlinePanel : MonoBehaviour
    {
        /// <summary>The on-demand server spawner (vb-spawn.py behind the site). POST → a
        /// dedicated match server boots on the box and answers with its join code. Same
        /// origin as the WebGL build, so browser players need no CORS story.</summary>
        const string SpawnUrl = "https://volleyball.ttnelson.com/spawn";

        public Button serverMatchButton;
        public Button hostButton;
        public Button joinButton;
        public Button backButton;
        public InputField codeInput;
        public Text statusText;
        public GameObject lobbyPanel;

        bool _awaitingLobby; // joined the session, waiting for the lobby object to replicate

        [System.Serializable]
        class SpawnResponse
        {
            public string code = null;  // = null: fields are filled by JsonUtility,
            public string error = null; // the explicit default just quiets CS0649
        }

        void Start()
        {
            if (serverMatchButton != null)
                serverMatchButton.onClick.AddListener(() => StartCoroutine(ServerMatch()));
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
            if (serverMatchButton != null) serverMatchButton.interactable = on;
            if (hostButton != null) hostButton.interactable = on;
            if (joinButton != null) joinButton.interactable = on;
        }

        /// <summary>Ask the box for a dedicated server, then join it like any other code.
        /// The request legitimately takes a few seconds — the server boots before answering.</summary>
        System.Collections.IEnumerator ServerMatch()
        {
            SetButtons(false);
            SetStatus("Requesting a dedicated server…");

            using var req = UnityWebRequest.PostWwwForm(SpawnUrl, "");
            req.timeout = 60;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                SetButtons(true);
                SetStatus($"Game server box unreachable ({req.error}) — try Host instead.");
                yield break;
            }

            SpawnResponse resp = null;
            try { resp = JsonUtility.FromJson<SpawnResponse>(req.downloadHandler.text); }
            catch { /* non-JSON answer falls through to the error below */ }
            if (resp == null || string.IsNullOrEmpty(resp.code))
            {
                SetButtons(true);
                SetStatus(string.IsNullOrEmpty(resp?.error) ? "No server available right now."
                                                            : resp.error);
                yield break;
            }

            SetStatus($"Server ready — joining {resp.code}…");
            JoinWithCode(resp.code);
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

        void Join() => JoinWithCode(codeInput != null ? codeInput.text : "");

        async void JoinWithCode(string code)
        {
            var nm = NetworkBootstrap.Ensure();
            if (nm == null || NetworkSessionController.Instance == null) return;

            SetButtons(false);
            SetStatus($"Joining {code}…");
            bool ok = await NetworkSessionController.Instance.JoinByCodeAsync(code);
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
