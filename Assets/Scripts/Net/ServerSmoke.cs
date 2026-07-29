using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// The dedicated-server smoke test: launch the player with <c>-vbserver</c> (typically
    /// also <c>-batchmode -nographics</c>) and it starts a pure server — StartServer, no host
    /// player, no session/relay — loads the beach arena with an all-AI cast, and lets the
    /// match run. Points scored = the whole authoritative loop (sim, AI, ball physics, match
    /// rules) works with no display, no audio device, and no local input; the process exits 0
    /// on pass, 1 on timeout. This is the cheap, repeatable proof that the code stays
    /// dedicated-server-ready — the cloud deployment story depends on exactly this working.
    /// </summary>
    public static class ServerSmoke
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-vbserver") < 0)
                return;
            if (Object.FindAnyObjectByType<ServerSmokeRunner>() != null) return;
            var go = new GameObject("ServerSmoke");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<ServerSmokeRunner>();
        }
    }

    public class ServerSmokeRunner : MonoBehaviour
    {
        const float TimeoutSeconds = 120f;
        const int TargetPoints = 3;

        float _elapsed;
        bool _started;

        void Start()
        {
            Debug.Log("[Volleyball] SERVER SMOKE starting — dedicated server, all-AI match.");

            var cfg = new MatchConfig { matchLabel = "DEDICATED SERVER SMOKE" };
            string[] cast = { CharacterRoster.ProtagonistId, CharacterRoster.TeammateId, "lion", "jaguar" };
            for (int i = 0; i < 4; i++)
                cfg.slots[i] = new MatchConfig.Slot
                {
                    team = i < 2 ? TeamSide.A : TeamSide.B,
                    halfSign = i % 2 == 0 ? -1f : 1f,
                    occupant = SlotOccupant.AI,
                    characterId = cast[i],
                };
            MatchSetup.Current = cfg;

            var nm = NetworkBootstrap.Ensure();
            if (nm == null) { Fail("no bootstrap prefab"); return; }
            (nm.NetworkConfig.NetworkTransport as UnityTransport)?.SetConnectionData("0.0.0.0", 7777);
            nm.StartServer(); // a real dedicated server: no host player at all
            nm.SceneManager.LoadScene(SceneFlow.BeachArena, UnityEngine.SceneManagement.LoadSceneMode.Single);
            _started = true;
        }

        void Update()
        {
            if (!_started) return;
            _elapsed += Time.deltaTime;

            var match = FindAnyObjectByType<MatchManager>();
            int points = match != null ? match.ScoreA + match.ScoreB : 0;
            if (points >= TargetPoints)
            {
                Debug.Log($"[Volleyball] SERVER SMOKE PASS — {points} points in {_elapsed:F0}s " +
                          $"({match.ScoreA}-{match.ScoreB}). Simulation is display/audio/input-free.");
                Application.Quit(0);
                return;
            }
            if (_elapsed > TimeoutSeconds)
                Fail($"no {TargetPoints} points after {TimeoutSeconds:F0}s (score {points})");
        }

        void Fail(string why)
        {
            Debug.LogError($"[Volleyball] SERVER SMOKE FAIL — {why}");
            Application.Quit(1);
        }
    }
}
