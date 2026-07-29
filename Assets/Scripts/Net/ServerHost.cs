using Unity.Netcode;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// The real dedicated session host: launch any player build with <c>-vbhost</c> (the
    /// Linux server build implies -batchmode) and it signs into UGS, creates a private
    /// Relay-backed session, and prints the JOIN CODE to the log — friends join through the
    /// normal Online → Join flow, exactly as if a player were hosting. No port forwarding,
    /// no TLS certificates, WebGL clients included. The box itself fields no player: the
    /// lobby runs in dedicated mode (all four slots open, match auto-starts once every
    /// claimed player readies up). Rally logic, AI, and scoring all run here — the machine
    /// is a neutral referee with a good connection.
    /// </summary>
    public static class ServerHost
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-vbhost") < 0)
                return;
            if (Object.FindAnyObjectByType<ServerHostRunner>() != null) return;
            var go = new GameObject("ServerHost");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<ServerHostRunner>();
        }
    }

    public class ServerHostRunner : MonoBehaviour
    {
        /// <summary>A spawned-on-demand server must die on its own: once no guest has been
        /// connected for this long (nobody ever came, or everyone left), exit cleanly so
        /// processes never accumulate on the box.</summary>
        const float IdleExitSeconds = 10f * 60f;

        float _emptySince;
        bool _running;

        async void Start()
        {
            // headless servers happily spin at thousands of fps — cap it, save the CPU
            Application.targetFrameRate = 60;

            Debug.Log("[Volleyball] SERVER HOST starting — creating Relay session…");
            OnlineLobbyState.DedicatedMode = true;

            var nm = NetworkBootstrap.Ensure();
            if (nm == null) { Quit("no bootstrap prefab in build"); return; }
            if (NetworkSessionController.Instance == null) { Quit("no session controller"); return; }

            bool ok = await NetworkSessionController.Instance.HostAsync();
            if (!ok)
            {
                Quit($"session create failed: {NetworkSessionController.Instance.LastError}");
                return;
            }

            string code = NetworkSessionController.Instance.JoinCode;
            Debug.Log("=======================================");
            Debug.Log($"[Volleyball]   JOIN CODE:  {code}");
            Debug.Log("=======================================");
            Debug.Log("[Volleyball] Waiting in dedicated lobby — match auto-starts when all " +
                      "claimed players are ready.");

            nm.OnClientConnectedCallback += id =>
                Debug.Log($"[Volleyball] client {id} connected " +
                          $"({nm.ConnectedClients.Count - 1} guest(s) in session)");
            nm.OnClientDisconnectCallback += id =>
                Debug.Log($"[Volleyball] client {id} disconnected");

            _emptySince = Time.realtimeSinceStartup;
            _running = true;
        }

        void Update()
        {
            if (!_running) return;
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return;

            // guests = everyone except our own (headless, playerless) host client
            int guests = nm.ConnectedClients.Count - (nm.IsHost ? 1 : 0);
            if (guests > 0)
            {
                _emptySince = Time.realtimeSinceStartup;
                return;
            }

            if (Time.realtimeSinceStartup - _emptySince > IdleExitSeconds)
            {
                Debug.Log($"[Volleyball] SERVER HOST idle-exit — empty for " +
                          $"{IdleExitSeconds / 60f:F0} minutes.");
                _running = false;
                NetworkSessionController.LeaveEverything();
                Application.Quit(0);
            }
        }

        static void Quit(string why)
        {
            Debug.LogError($"[Volleyball] SERVER HOST FAIL — {why}");
            Application.Quit(1);
        }
    }
}
