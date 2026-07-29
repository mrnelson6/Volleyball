using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Phase-1 developer entry point for online play, before any real lobby exists: an IMGUI
    /// overlay in the main menu with Host (versus / co-op / 4-human) and Join buttons, and a
    /// small stats line (RTT, tick lead, last prediction error) once a session runs. Editor
    /// and development builds only. Placed in the MainMenu scene by its builder; survives the
    /// scene switch so the stats stay visible in the arena.
    /// </summary>
    public class NetworkDebugHUD : MonoBehaviour
    {
        static NetworkDebugHUD _instance;
        string _joinIp = "127.0.0.1";

        void Awake()
        {
            if (_instance != null) { Destroy(gameObject); return; }
            _instance = this;
            if (!Application.isEditor && !Debug.isDebugBuild) { enabled = false; return; }
            DontDestroyOnLoad(gameObject);
        }

        void Host(MatchConfig cfg)
        {
            var nm = NetworkBootstrap.Ensure();
            if (nm == null) return;
            MatchSetup.Current = cfg;
            (nm.NetworkConfig.NetworkTransport as UnityTransport)?.SetConnectionData("0.0.0.0", 7777);
            nm.StartHost();
            nm.SceneManager.LoadScene(SceneFlow.BeachArena, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }

        void Join()
        {
            var nm = NetworkBootstrap.Ensure();
            if (nm == null) return;
            MatchSetup.Clear(); // the host's config arrives over the wire
            (nm.NetworkConfig.NetworkTransport as UnityTransport)?.SetConnectionData(_joinIp, 7777);
            nm.StartClient();
        }

        static MatchConfig.Slot Slot(TeamSide team, float half, SlotOccupant occ, string id)
            => new MatchConfig.Slot
            {
                team = team,
                halfSign = half,
                occupant = occ,
                characterId = id,
                clientId = occ == SlotOccupant.AI ? 0 : MatchConfig.UnassignedClient,
            };

        static MatchConfig Config(SlotOccupant aRight, SlotOccupant bLeft, SlotOccupant bRight)
            => new MatchConfig
            {
                slots = new[]
                {
                    Slot(TeamSide.A, -1f, SlotOccupant.LocalHuman, CharacterRoster.ProtagonistId),
                    Slot(TeamSide.A, 1f, aRight, CharacterRoster.TeammateId),
                    Slot(TeamSide.B, -1f, bLeft, "lion"),
                    Slot(TeamSide.B, 1f, bRight, "jaguar"),
                },
                matchLabel = "ONLINE (dev)",
            };

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 220));

            if (!NetworkSession.IsOnline)
            {
                // only offer hosting from the menu — mid-match hijacks are not a flow we support
                bool inMenu = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == SceneFlow.MainMenu;
                if (inMenu)
                {
                    GUILayout.Label("— ONLINE (dev) —");
                    if (GUILayout.Button("Host VERSUS (1v1 + AI mates)"))
                        Host(Config(SlotOccupant.AI, SlotOccupant.RemoteHuman, SlotOccupant.AI));
                    if (GUILayout.Button("Host CO-OP (2 humans vs AI)"))
                        Host(Config(SlotOccupant.RemoteHuman, SlotOccupant.AI, SlotOccupant.AI));
                    if (GUILayout.Button("Host 4-HUMAN"))
                        Host(Config(SlotOccupant.RemoteHuman, SlotOccupant.RemoteHuman, SlotOccupant.RemoteHuman));
                    GUILayout.BeginHorizontal();
                    _joinIp = GUILayout.TextField(_joinIp, GUILayout.Width(120));
                    if (GUILayout.Button("Join")) Join();
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                var nm = NetworkManager.Singleton;
                string role = nm.IsHost ? "HOST" : "CLIENT";
                float rtt = 0f;
                if (!nm.IsServer)
                    rtt = (nm.NetworkConfig.NetworkTransport as UnityTransport)
                          ?.GetCurrentRtt(NetworkManager.ServerClientId) ?? 0f;
                var clock = SimClock.Instance;
                GUILayout.Label($"{role}  rtt:{rtt:F0}ms  tick:{(clock != null ? clock.Tick : 0)}"
                                + (clock != null && !nm.IsServer ? $"  lead:{clock.LeadTicks:F1}" : ""));
                if (!nm.IsServer)
                    GUILayout.Label($"last correction: {NetworkPlayer.LastCorrectionError:F3}m");
                if (GUILayout.Button("Disconnect"))
                {
                    nm.Shutdown();
                    SceneFlow.LoadMenu();
                }
            }

            GUILayout.EndArea();
        }
    }
}
