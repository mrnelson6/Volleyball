using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// The ONLY class that talks to Unity Gaming Services — everything session-shaped
    /// (anonymous sign-in, Relay-backed session create/join by code, locking, leaving) is
    /// isolated here so UGS API churn touches one file. The Sessions package's Netcode
    /// integration starts/stops the NGO host or client for us when a session is created or
    /// joined; this class just owns the session handle and surfaces state to the UI.
    /// Lives on the NetworkBootstrap prefab.
    /// </summary>
    public class NetworkSessionController : MonoBehaviour
    {
        public static NetworkSessionController Instance { get; private set; }

        ISession _session;
        static bool _servicesReady;

        /// <summary>The shareable join code, once hosting (null otherwise).</summary>
        public string JoinCode => _session?.Code;

        /// <summary>True while an async session operation runs (UI disables its buttons).</summary>
        public bool Busy { get; private set; }

        /// <summary>Human-readable outcome of the last failed operation, for the status line.</summary>
        public string LastError { get; private set; }

        /// <summary>One-shot notice for the menu after an involuntary disconnect ("host left").
        /// Set here, displayed and cleared by the Online panel.</summary>
        public static string DisconnectNotice;

        NetworkManager _nm;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _nm = GetComponent<NetworkManager>();
        }

        void Start()
        {
            if (_nm != null) _nm.OnClientDisconnectCallback += OnClientDisconnect;
        }

        void OnDestroy()
        {
            if (_nm != null) _nm.OnClientDisconnectCallback -= OnClientDisconnect;
            if (Instance == this) Instance = null;
        }

        /// <summary>The connection died under us (host quit, network drop): tear down and go
        /// home. Fires only for OUR own disconnect on a client — on the server this callback
        /// reports other clients leaving, which NetworkMatchState handles.</summary>
        void OnClientDisconnect(ulong clientId)
        {
            if (_nm == null || _nm.IsServer || clientId != _nm.LocalClientId) return;
            // the server may have sent a reason (e.g. the version-mismatch rejection)
            DisconnectNotice = !string.IsNullOrEmpty(_nm.DisconnectReason)
                ? _nm.DisconnectReason
                : "Disconnected — the host left or the connection dropped.";
            _session = null; // the session died with the host; nothing to politely leave
            _ = LeaveAsync();
            SceneFlow.LoadMenu();
        }

        static async Task EnsureServicesAsync()
        {
            if (_servicesReady) return;
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            _servicesReady = true;
        }

        /// <summary>Create a private Relay-backed session for 4 players. On success the NGO
        /// host is already running and the lobby state object is spawned.</summary>
        public async Task<bool> HostAsync()
        {
            if (Busy) return false;
            Busy = true;
            LastError = null;
            try
            {
                await EnsureServicesAsync();
                var options = new SessionOptions
                {
                    MaxPlayers = 4,
                    IsPrivate = true, // join-code only — never listed publicly
                }.WithRelayNetwork();
                _session = await MultiplayerService.Instance.CreateSessionAsync(options);
                NetworkBootstrap.SpawnLobbyState();
                VBLog.Event($"SESSION hosted, code={_session.Code}");
                return true;
            }
            catch (Exception e)
            {
                LastError = Friendly(e);
                Debug.LogError($"[Volleyball] Host failed: {e}");
                return false;
            }
            finally { Busy = false; }
        }

        /// <summary>Join a session by its code. On success the NGO client is connecting and
        /// the lobby state will replicate in shortly after.</summary>
        public async Task<bool> JoinByCodeAsync(string code)
        {
            if (Busy) return false;
            if (string.IsNullOrWhiteSpace(code)) { LastError = "Enter a join code first."; return false; }
            Busy = true;
            LastError = null;
            try
            {
                await EnsureServicesAsync();
                _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
                VBLog.Event($"SESSION joined, code={_session.Code}");
                return true;
            }
            catch (Exception e)
            {
                LastError = Friendly(e);
                Debug.LogError($"[Volleyball] Join failed: {e}");
                return false;
            }
            finally { Busy = false; }
        }

        /// <summary>Lock the session when the match starts — no more joins this session.</summary>
        public async void LockSession()
        {
            try
            {
                if (_session == null) return;
                var host = _session.AsHost();
                host.IsLocked = true;
                await host.SavePropertiesAsync();
            }
            catch (Exception e) { Debug.LogWarning($"[Volleyball] Session lock failed: {e.Message}"); }
        }

        /// <summary>Leave the session and shut the network down. Safe to call from anywhere,
        /// in any state — this is the single teardown path.</summary>
        public async Task LeaveAsync()
        {
            var s = _session;
            _session = null;
            try { if (s != null) await s.LeaveAsync(); }
            catch (Exception e) { Debug.LogWarning($"[Volleyball] Session leave: {e.Message}"); }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
        }

        /// <summary>Fire-and-forget teardown for synchronous callers (scene transitions).</summary>
        public static void LeaveEverything()
        {
            if (Instance != null) _ = Instance.LeaveAsync();
            else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
        }

        static string Friendly(Exception e)
        {
            string m = e.Message;
            if (m.Contains("not found") || m.Contains("404")) return "No session with that code.";
            if (m.Contains("locked")) return "That match has already started.";
            if (m.Contains("full") || m.Contains("max")) return "That session is full.";
            return m.Length > 120 ? m.Substring(0, 120) : m;
        }
    }
}
