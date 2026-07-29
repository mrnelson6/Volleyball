using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Per-player network adapter, placed by the scene builder next to each
    /// <see cref="VolleyPlayer"/>. The player class itself stays a plain MonoBehaviour;
    /// this component decides who steps its simulation and how:
    ///
    ///  - SERVER: steps every player as Authority. AI and the host's own human sample their
    ///    command source directly; a remote human's commands come from the tick-indexed
    ///    buffer its owner streams up (holding the last known command over gaps).
    ///  - OWNER CLIENT: samples input, predicts its own player immediately (Predict role),
    ///    records command+state history, and streams the last few commands to the server
    ///    (unreliable, redundant). When a snapshot disagrees with what was predicted for
    ///    that tick, state rolls back and replays — Replay role, so no effect re-fires.
    ///  - PROXY (everyone else on a client): the VolleyPlayer is disabled entirely and this
    ///    component drives it from an interpolation buffer ~100ms behind the server.
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour
    {
        const int BufferSize = 256; // ~5s of ticks; plenty beyond any RTT
        const int Mask = BufferSize - 1;

        VolleyPlayer _player;
        public VolleyPlayer Player => _player != null ? _player : (_player = GetComponent<VolleyPlayer>());

        // ---- owner-client prediction history ----
        readonly InputCommand[] _cmdHistory = new InputCommand[BufferSize];
        readonly PlayerSimState[] _stateHistory = new PlayerSimState[BufferSize];
        readonly int[] _historyTick = new int[BufferSize];
        int _lastPredictedTick = -1;

        /// <summary>Magnitude of the last reconciliation correction (debug HUD; ~0 = healthy).</summary>
        public static float LastCorrectionError { get; private set; }

        // ---- server-side command buffer for a remote human ----
        readonly InputCommand[] _serverCmds = new InputCommand[BufferSize];
        readonly int[] _serverCmdTick = new int[BufferSize];
        InputCommand _heldCmd; // last known command, reused over stream gaps
        int _lastConsumedCmdTick = -1;

        /// <summary>The tick of the owner command the server last actually consumed —
        /// acked back in snapshots so the owner can trust (or distrust) its prediction.</summary>
        public int LastConsumedCmdTick => _lastConsumedCmdTick;

        // ---- proxy interpolation ----
        struct ProxySample { public int tick; public PlayerSimState state; }
        readonly List<ProxySample> _proxyBuffer = new List<ProxySample>(16);
        const float ProxyDelayTicks = 5f; // ~100ms behind the server

        bool IsOwnedHuman => Player != null && Player.IsHuman && IsOwner;
        bool IsProxy => !IsServer && !IsOwnedHuman;

        void Awake()
        {
            // tick 0 is a real tick — unfilled ring slots must never match it
            for (int i = 0; i < BufferSize; i++)
            {
                _serverCmdTick[i] = int.MinValue;
                _historyTick[i] = int.MinValue;
            }
        }

        public override void OnNetworkSpawn() => Reconfigure();
        protected override void OnOwnershipChanged(ulong previous, ulong current) => Reconfigure();

        /// <summary>
        /// (Re)derive this player's mode from ownership + occupant. Idempotent, and called
        /// liberally: on spawn, on ownership changes, and after the slot binder swaps the
        /// controller component underneath us.
        /// </summary>
        public void Reconfigure()
        {
            _player = GetComponent<VolleyPlayer>();
            if (_player == null || !IsSpawned) return;

            // networked: this adapter drives every simulation step
            _player.SimulationEnabled = false;

            if (IsServer)
            {
                // server renders its own sim; the host also samples its own input locally.
                // Local-control is decided by the CONFIG's occupant, not by ownership: a
                // disconnected client's player reverts to server ownership, and ownership
                // alone would hand it to the host's keyboard.
                _player.enabled = true;
                _player.IsLocallyControlled = IsConfiguredLocalHuman();
            }
            else if (IsOwnedHuman)
            {
                _player.enabled = true; // predicts + interpolates its own view
                _player.IsLocallyControlled = true;
                _lastPredictedTick = -1;
            }
            else
            {
                // proxy: fully driven from snapshots — the player's own Update/FixedUpdate stay off
                _player.enabled = false;
                _player.IsLocallyControlled = false;
                _proxyBuffer.Clear();
            }
        }

        void FixedUpdate()
        {
            if (!IsSpawned || !NetworkSession.IsOnline || Player == null) return;
            var clock = SimClock.Instance;
            if (clock == null) return;

            if (IsServer) ServerSteps(clock);
            else if (IsOwnedHuman) OwnerSteps(clock);
        }

        // ------------------------------------------------------------------ server

        bool IsConfiguredLocalHuman()
        {
            if (_player == null || !_player.IsHuman) return false;
            MatchConfig cfg = MatchSetup.Current;
            if (cfg != null && cfg.TryGetSlot(_player.team, _player.halfSign, out MatchConfig.Slot slot))
                return slot.occupant == SlotOccupant.LocalHuman;
            return OwnerClientId == NetworkManager.ServerClientId; // no config (dev flows)
        }

        /// <summary>The owning client is gone: silence its input so the player stands still
        /// (never runs off on the last held command) until the AI takes the slot over.</summary>
        public void OnOwnerDropped()
        {
            _heldCmd = default;
            for (int i = 0; i < BufferSize; i++) _serverCmdTick[i] = int.MinValue;
            if (Player != null) Player.IsLocallyControlled = false;
        }

        void ServerSteps(SimClock clock)
        {
            float dt = Time.fixedDeltaTime;
            for (int i = clock.StepsThisTick - 1; i >= 0; i--)
            {
                int tick = clock.Tick - i;
                InputCommand cmd;
                bool remoteHuman = Player.IsHuman && OwnerClientId != NetworkManager.ServerClientId;
                if (remoteHuman)
                {
                    int slot = tick & Mask;
                    if (_serverCmdTick[slot] == tick) cmd = _serverCmds[slot];
                    else
                    {
                        // gap in the stream: repeat the last known intent, minus the one-shot
                        // callout — a lost packet must not make the player shout every tick
                        cmd = _heldCmd;
                        cmd.chat = ChatCall.None;
                    }
                    cmd.tick = tick;
                    _heldCmd = cmd;
                }
                else
                {
                    cmd = Player.GetCommand(tick); // AI, or the host sampling its own devices
                }
                Player.Simulate(in cmd, dt, SimRole.Authority);
                _lastConsumedCmdTick = cmd.tick;
            }
        }

        /// <summary>Owner → server: the last few ticks of commands, redundantly, unreliable.</summary>
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        void SubmitCommandsRpc(InputCommand[] cmds)
        {
            foreach (var c in cmds)
            {
                int slot = c.tick & Mask;
                if (_serverCmdTick[slot] == c.tick) continue; // duplicate from redundancy
                _serverCmds[slot] = c;
                _serverCmdTick[slot] = c.tick;
            }
        }

        // ------------------------------------------------------------------ owner client

        void OwnerSteps(SimClock clock)
        {
            if (clock.StepsThisTick == 0)
            {
                // convergence stall: no sim step this FixedUpdate — pin the view so it
                // doesn't re-lerp (backwards) across the previous step's interval
                Player.FlattenViewInterpolation();
                return;
            }

            float dt = Time.fixedDeltaTime;
            for (int i = clock.StepsThisTick - 1; i >= 0; i--)
            {
                int tick = clock.Tick - i;
                InputCommand cmd = Player.GetCommand(tick);
                int slot = tick & Mask;
                _cmdHistory[slot] = cmd;
                Player.Simulate(in cmd, dt, SimRole.Predict);
                _stateHistory[slot] = Player.CaptureSimState();
                _historyTick[slot] = tick;
                _lastPredictedTick = tick;
            }

            if (clock.StepsThisTick > 0)
            {
                // ship the last 3 commands so one lost packet costs nothing
                int n = Mathf.Min(3, _lastPredictedTick + 1);
                var send = new InputCommand[n];
                for (int i = 0; i < n; i++)
                    send[i] = _cmdHistory[(_lastPredictedTick - i) & Mask];
                SubmitCommandsRpc(send);
            }
        }

        /// <summary>
        /// A server snapshot for this player has arrived (routed by SnapshotSync).
        /// Owner: verify the prediction for that tick and roll back + replay on mismatch.
        /// Proxy: append to the interpolation buffer.
        /// </summary>
        public void OnServerState(int serverTick, in PlayerSimState serverState, float powerCharge)
        {
            if (IsServer || Player == null) return;

            Player.Power.MirrorCharge(powerCharge);

            if (IsOwnedHuman) ReconcileOwner(serverTick, in serverState);
            else PushProxySample(serverTick, in serverState);
        }

        void ReconcileOwner(int serverTick, in PlayerSimState serverState)
        {
            int slot = serverTick & Mask;
            bool haveHistory = _historyTick[slot] == serverTick && serverTick <= _lastPredictedTick;

            if (haveHistory)
            {
                PlayerSimState predicted = _stateHistory[slot];
                float posErr = Vector3.Distance(predicted.position, serverState.position);
                bool mismatch = posErr > 0.02f
                                || Mathf.Abs(predicted.vertVel - serverState.vertVel) > 0.1f
                                || (predicted.diveTimer > 0f) != (serverState.diveTimer > 0f);
                if (!mismatch) return; // prediction confirmed — the common case
                LastCorrectionError = posErr;
            }
            else
            {
                LastCorrectionError = -1f; // no history to check against (fresh spawn / huge lag)
            }

            // roll back to the server's truth and replay everything it hasn't seen yet
            Player.ApplySimState(in serverState);
            float dt = Time.fixedDeltaTime;
            for (int t = serverTick + 1; t <= _lastPredictedTick; t++)
            {
                int s = t & Mask;
                InputCommand cmd = _historyTick[s] == t ? _cmdHistory[s] : InputCommand.Empty(t);
                Player.Simulate(in cmd, dt, SimRole.Replay);
                _stateHistory[s] = Player.CaptureSimState();
                _historyTick[s] = t;
            }
        }

        // ------------------------------------------------------------------ proxy

        void PushProxySample(int tick, in PlayerSimState state)
        {
            // keep the buffer sorted & bounded; unreliable delivery can reorder
            for (int i = _proxyBuffer.Count - 1; i >= 0; i--)
                if (_proxyBuffer[i].tick == tick) return;
            _proxyBuffer.Add(new ProxySample { tick = tick, state = state });
            _proxyBuffer.Sort((a, b) => a.tick.CompareTo(b.tick));
            while (_proxyBuffer.Count > 12) _proxyBuffer.RemoveAt(0);
        }

        /// <summary>A rally reset teleported everyone — drop stale motion history.</summary>
        public void OnTeleported(Vector3 groundPos)
        {
            _proxyBuffer.Clear();
            if (Player == null) return;
            Player.TeleportTo(groundPos);
            Player.ResetState();
            if (IsOwnedHuman) _lastPredictedTick = -1; // prediction restarts from the new spot
        }

        void Update()
        {
            if (!IsSpawned || !NetworkSession.IsOnline || !IsProxy || Player == null) return;
            if (_proxyBuffer.Count == 0) return;

            var clock = SimClock.Instance;
            float renderTick = (clock != null ? clock.EstimatedServerTick : _proxyBuffer[_proxyBuffer.Count - 1].tick)
                               - ProxyDelayTicks;

            // find the two samples bracketing renderTick
            ProxySample a = _proxyBuffer[0], b = _proxyBuffer[_proxyBuffer.Count - 1];
            for (int i = 0; i < _proxyBuffer.Count - 1; i++)
                if (_proxyBuffer[i].tick <= renderTick && _proxyBuffer[i + 1].tick >= renderTick)
                {
                    a = _proxyBuffer[i];
                    b = _proxyBuffer[i + 1];
                    break;
                }

            float span = Mathf.Max(1, b.tick - a.tick);
            float u = Mathf.Clamp01((renderTick - a.tick) / span);

            // nearest sample gives the animator its pose state (dive timers, air state);
            // position lerps smoothly between the two
            PlayerSimState pose = u < 0.5f ? a.state : b.state;
            Player.ApplySimState(in pose);
            Player.transform.position = Vector3.Lerp(a.state.position, b.state.position, u);
        }
    }
}
