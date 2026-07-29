using Unity.Netcode;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// The server's state broadcast: every other tick (25Hz) it batches all four players'
    /// full <see cref="PlayerSimState"/> (+ power charge and command acks) and the ball
    /// position into one unreliable RPC. Clients route each entry to its
    /// <see cref="NetworkPlayer"/> — owners reconcile, proxies interpolate — and feed the
    /// tick to <see cref="SimClock"/> for clock sync. One batched message instead of four
    /// per-object streams: cheaper, and every receiver sees a coherent instant of the match.
    /// </summary>
    [DefaultExecutionOrder(300)] // captures AFTER every NetworkPlayer stepped this tick
    public class SnapshotSync : NetworkBehaviour
    {
        const int SendEveryNTicks = 2; // 25Hz

        public struct PlayerSnap : INetworkSerializable
        {
            public NetworkObjectReference player;
            public int lastCmdTick;   // owner's command tick the server last consumed
            public float powerCharge;
            public PlayerSimState state;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref player);
                serializer.SerializeValue(ref lastCmdTick);
                serializer.SerializeValue(ref powerCharge);
                serializer.SerializeNetworkSerializable(ref state);
            }
        }

        NetworkPlayer[] _players;
        NetworkBall _ball;
        int _lastSentTick;

        public override void OnNetworkSpawn()
        {
            _players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
            _ball = FindAnyObjectByType<NetworkBall>();
        }

        void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || !NetworkSession.IsOnline) return;
            var clock = SimClock.Instance;
            if (clock == null || clock.Tick - _lastSentTick < SendEveryNTicks) return;
            _lastSentTick = clock.Tick;

            if (_players == null || _players.Length == 0)
                _players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);

            var snaps = new PlayerSnap[_players.Length];
            for (int i = 0; i < _players.Length; i++)
            {
                var np = _players[i];
                snaps[i] = new PlayerSnap
                {
                    player = np.NetworkObject,
                    lastCmdTick = np.LastConsumedCmdTick,
                    powerCharge = np.Player != null ? np.Player.Power.Charge : 0f,
                    state = np.Player != null ? np.Player.CaptureSimState() : default,
                };
            }

            Vector3 ballPos = _ball != null ? _ball.transform.position : Vector3.zero;
            SnapshotRpc(clock.Tick, snaps, ballPos);
        }

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        void SnapshotRpc(int serverTick, PlayerSnap[] players, Vector3 ballPos)
        {
            SimClock.Instance?.OnServerTick(serverTick);

            foreach (var snap in players)
            {
                if (!snap.player.TryGet(out NetworkObject obj)) continue;
                var np = obj.GetComponent<NetworkPlayer>();
                if (np != null) np.OnServerState(serverTick, in snap.state, snap.powerCharge);
            }

            if (_ball == null) _ball = FindAnyObjectByType<NetworkBall>();
            _ball?.OnBallState(serverTick, ballPos);
        }
    }
}
