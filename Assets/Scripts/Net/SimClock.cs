using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// The shared simulation tick, stepped in FixedUpdate (50Hz). On the server it simply
    /// counts. On a client it runs AHEAD of the server by ~half the round-trip plus a small
    /// jitter margin, so a command stamped with the client's tick arrives just before the
    /// server simulates that tick. The client converges on its target by occasionally
    /// running two sim steps in one FixedUpdate (behind) or none (ahead) — never by touching
    /// timeScale. Offline the clock is absent and players self-drive as in Phase 0.
    /// </summary>
    [DefaultExecutionOrder(-300)] // ticks before every NetworkPlayer steps its sim
    public class SimClock : MonoBehaviour
    {
        public static SimClock Instance { get; private set; }

        /// <summary>The tick the local simulation is currently AT (last stepped).</summary>
        public int Tick { get; private set; }

        /// <summary>How many sim steps to run this FixedUpdate: usually 1; 2 to catch up,
        /// 0 to let the server gain ground. The steps cover ticks
        /// (Tick - StepsThisTick + 1) .. Tick.</summary>
        public int StepsThisTick { get; private set; } = 1;

        /// <summary>Extra safety ticks on top of the RTT-derived lead.</summary>
        const int JitterMarginTicks = 2;
        const int HardSnapThreshold = 10;

        int _lastServerTick = -1;
        float _lastServerTickAt; // Time.time when that snapshot tick arrived

        /// <summary>Best guess of the server's current tick. Measured against Time.time — NOT
        /// fixedTime, which freezes between physics steps: proxies and the ball render at this
        /// value every frame, and a stepping clock strobes their motion (twitchy run cycles).</summary>
        public float EstimatedServerTick
            => _lastServerTick < 0 ? 0f
             : _lastServerTick + (Time.time - _lastServerTickAt) / Time.fixedDeltaTime;

        /// <summary>Current tick lead over the estimated server tick (debug HUD).</summary>
        public float LeadTicks => Tick - EstimatedServerTick;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Fed by SnapshotSync every time a server snapshot arrives.</summary>
        public void OnServerTick(int serverTick)
        {
            if (serverTick <= _lastServerTick) return; // stale/out-of-order (unreliable channel)
            _lastServerTick = serverTick;
            _lastServerTickAt = Time.time;
        }

        void FixedUpdate()
        {
            if (!NetworkSession.IsOnline || NetworkSession.IsAuthority)
            {
                StepsThisTick = 1;
                Tick++;
                return;
            }

            // client: aim for estimated-server-tick + lead
            if (_lastServerTick < 0) { StepsThisTick = 0; return; } // nothing heard yet

            float rttMs = 0f;
            var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
            if (transport != null) rttMs = transport.GetCurrentRtt(NetworkManager.ServerClientId);

            int lead = Mathf.CeilToInt(rttMs * 0.001f * 0.5f / Time.fixedDeltaTime) + JitterMarginTicks;
            int target = Mathf.RoundToInt(EstimatedServerTick) + lead + 1; // +1: we're about to step

            int diff = target - (Tick + 1);
            if (Mathf.Abs(diff) > HardSnapThreshold)
            {
                Tick = target;
                StepsThisTick = 1;
                VBLog.Event($"SIMCLOCK hard snap to tick {target} (lead {lead})");
                return;
            }
            StepsThisTick = diff >= 1 ? 2 : diff <= -1 ? 0 : 1;
            Tick += StepsThisTick;
        }
    }
}
