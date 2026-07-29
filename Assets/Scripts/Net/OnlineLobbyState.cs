using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// The lobby's replicated truth: four court slots (0 = A-left, 1 = A-right, 2 = B-left,
    /// 3 = B-right), each either open/AI or claimed by a client, with a character pick and a
    /// ready flag, plus the host's arena choice. Spawned by the host right after the session
    /// is created (survives into the arena for a future back-to-lobby flow). All mutations go
    /// through server RPCs; the UI just renders the list and pushes intents. Sessions (UGS)
    /// handles connect/relay — lobby STATE deliberately replicates over NGO so the whole
    /// project has exactly one replication idiom.
    /// </summary>
    public class OnlineLobbyState : NetworkBehaviour
    {
        public static OnlineLobbyState Instance { get; private set; }

        /// <summary>Raised on every machine when the lobby replicates in or changes.</summary>
        public static event Action Changed;

        /// <summary>Set true (before the session spawns this) by a headless <c>-vbhost</c>
        /// server: the machine fields no player, so slot 0 stays open like the rest, no
        /// client sees Start/arena controls, and the match auto-starts on a short countdown
        /// once at least one slot is claimed and every claimed player is ready.</summary>
        public static bool DedicatedMode;

        /// <summary>Dedicated auto-start countdown in whole seconds; -1 = not counting.
        /// Replicated so every lobby screen can show "Starting in N…".</summary>
        public readonly NetworkVariable<int> AutoStartSeconds = new NetworkVariable<int>(-1);

        float _autoStartAt = -1f; // server wall-clock deadline
        bool _matchLaunched;

        public struct LobbySlot : INetworkSerializable, IEquatable<LobbySlot>
        {
            public ulong clientId; // MatchConfig.UnassignedClient = open (AI plays it)
            public FixedString32Bytes characterId;
            public bool ready;

            public bool IsOpen => clientId == MatchConfig.UnassignedClient;

            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref clientId);
                s.SerializeValue(ref characterId);
                s.SerializeValue(ref ready);
            }

            public bool Equals(LobbySlot o)
                => clientId == o.clientId && characterId.Equals(o.characterId) && ready == o.ready;
        }

        NetworkList<LobbySlot> _slots;

        public readonly NetworkVariable<int> ArenaIndex = new NetworkVariable<int>();

        public static TeamSide TeamOf(int slotIndex) => slotIndex < 2 ? TeamSide.A : TeamSide.B;
        public static float HalfSignOf(int slotIndex) => slotIndex % 2 == 0 ? -1f : 1f;

        static readonly string[] DefaultCharacters =
            { CharacterRoster.ProtagonistId, CharacterRoster.TeammateId, "lion", "jaguar" };

        void Awake()
        {
            _slots = new NetworkList<LobbySlot>();
            Instance = this;
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            DontDestroyOnLoad(gameObject);
            if (IsServer && _slots.Count == 0)
            {
                for (int i = 0; i < 4; i++)
                    _slots.Add(new LobbySlot
                    {
                        // the hosting player takes A-left; on a dedicated box there is no
                        // hosting player, so every slot starts open (AI plays the leftovers)
                        clientId = i == 0 && !DedicatedMode ? NetworkManager.ServerClientId
                                                            : MatchConfig.UnassignedClient,
                        characterId = DefaultCharacters[i],
                        ready = false,
                    });
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            }
            _slots.OnListChanged += _ => Changed?.Invoke();
            ArenaIndex.OnValueChanged += (_, _2) => Changed?.Invoke();
            AutoStartSeconds.OnValueChanged += (_, _2) => Changed?.Invoke();
            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null)
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        void OnClientDisconnected(ulong clientId)
        {
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].clientId == clientId)
                {
                    LobbySlot s = _slots[i];
                    s.clientId = MatchConfig.UnassignedClient;
                    s.ready = false;
                    _slots[i] = s;
                }
        }

        /// <summary>
        /// Dedicated auto-start: once at least one slot is claimed and every claimed player
        /// is ready, a short replicated countdown runs — the grace window lets a second
        /// friend claim a slot, which (like any lobby change) resets the clock. Zero → the
        /// match launches exactly as if a hosting player pressed Start.
        /// </summary>
        void Update()
        {
            if (!IsSpawned || !IsServer || !DedicatedMode || _matchLaunched) return;

            int claimed = 0;
            for (int i = 0; i < _slots.Count; i++)
                if (!_slots[i].IsOpen) claimed++;
            bool conditions = claimed > 0 && AllHumansReady();

            if (!conditions)
            {
                _autoStartAt = -1f;
                if (AutoStartSeconds.Value != -1) AutoStartSeconds.Value = -1;
                return;
            }

            if (_autoStartAt < 0f) _autoStartAt = Time.time + 10f;
            int remain = Mathf.Max(0, Mathf.CeilToInt(_autoStartAt - Time.time));
            if (AutoStartSeconds.Value != remain) AutoStartSeconds.Value = remain;
            if (remain == 0) StartMatchHost();
        }

        // ------------------------------------------------------------------ queries (any machine)

        public int SlotCount => _slots.Count;
        public LobbySlot GetSlot(int i) => _slots[i];

        public int SlotOf(ulong clientId)
        {
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].clientId == clientId) return i;
            return -1;
        }

        public int MySlot => SlotOf(NetworkManager.LocalClientId);

        public bool AllHumansReady()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                // the host readies by pressing Start itself
                if (!s.IsOpen && s.clientId != NetworkManager.ServerClientId && !s.ready)
                    return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ intents (client → server)

        [Rpc(SendTo.Server)]
        public void ClaimSlotRpc(int index, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (index < 0 || index >= _slots.Count) return;
            if (!_slots[index].IsOpen) return; // taken — first come, first served

            int current = SlotOf(sender);
            if (current == index) return;
            if (current >= 0)
            {
                LobbySlot old = _slots[current];
                old.clientId = MatchConfig.UnassignedClient;
                old.ready = false;
                _slots[current] = old;
            }

            LobbySlot s = _slots[index];
            s.clientId = sender;
            s.ready = false;
            _slots[index] = s;
            VBLog.Event($"LOBBY client {sender} claims slot {index}");
        }

        [Rpc(SendTo.Server)]
        public void SetCharacterRpc(int index, FixedString32Bytes characterId, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (index < 0 || index >= _slots.Count) return;
            // your own slot — or, for the host, any open (AI) slot
            bool allowed = _slots[index].clientId == sender
                           || (sender == NetworkManager.ServerClientId && _slots[index].IsOpen);
            if (!allowed) return;

            LobbySlot s = _slots[index];
            s.characterId = characterId;
            _slots[index] = s;
        }

        [Rpc(SendTo.Server)]
        public void SetReadyRpc(bool ready, RpcParams rpcParams = default)
        {
            int i = SlotOf(rpcParams.Receive.SenderClientId);
            if (i < 0) return;
            LobbySlot s = _slots[i];
            s.ready = ready;
            _slots[i] = s;
        }

        /// <summary>Host only (runs where the server is): set the arena directly.</summary>
        public void CycleArenaHost(int dir)
        {
            if (!IsServer) return;
            int n = SceneFlow.Arenas.Length;
            ArenaIndex.Value = ((ArenaIndex.Value + dir) % n + n) % n;
        }

        /// <summary>
        /// Host pressed Start: freeze the lobby into a <see cref="MatchConfig"/>, lock the
        /// session, and take everyone into the arena. From the scene load onward the Phase 1
        /// pipeline runs unchanged — config replication, slot binding, ownership, kickoff.
        /// </summary>
        public void StartMatchHost()
        {
            if (!IsServer || _matchLaunched || !AllHumansReady()) return;
            _matchLaunched = true;
            AutoStartSeconds.Value = -1;

            var cfg = new MatchConfig { matchLabel = $"ONLINE — {SceneFlow.ArenaNames[ArenaIndex.Value]}" };
            for (int i = 0; i < 4; i++)
            {
                LobbySlot s = _slots[i];
                cfg.slots[i] = new MatchConfig.Slot
                {
                    team = TeamOf(i),
                    halfSign = HalfSignOf(i),
                    occupant = s.IsOpen ? SlotOccupant.AI
                             : s.clientId == NetworkManager.ServerClientId ? SlotOccupant.LocalHuman
                                                                          : SlotOccupant.RemoteHuman,
                    characterId = s.characterId.ToString(),
                    clientId = s.IsOpen ? 0 : s.clientId,
                };
            }
            MatchSetup.Current = cfg;

            NetworkSessionController.Instance?.LockSession();
            VBLog.Event($"LOBBY start -> {SceneFlow.Arenas[ArenaIndex.Value]}");
            NetworkManager.SceneManager.LoadScene(SceneFlow.Arenas[ArenaIndex.Value],
                                                  UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }
}
