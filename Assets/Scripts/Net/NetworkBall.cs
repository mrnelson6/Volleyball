using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Ball adapter. The ball is simulated ONLY on the server (real physics + wind there);
    /// clients hold a kinematic shell whose position interpolates ~100ms behind the server's
    /// snapshot stream. Launch/hold moments additionally arrive as reliable RPCs carrying
    /// what snapshots can't: spin for the sprite/trail, who hit it (swing pose), and the
    /// contact audio. Ground scoring stays server-side by construction — a kinematic client
    /// ball never generates collision events.
    /// </summary>
    public class NetworkBall : NetworkBehaviour
    {
        BallController _ball;

        struct Sample { public int tick; public Vector3 pos; }
        readonly List<Sample> _buffer = new List<Sample>(16);
        const float DelayTicks = 5f;
        bool _held;

        public override void OnNetworkSpawn()
        {
            _ball = GetComponent<BallController>();
            if (IsServer)
            {
                _ball.OnLaunched += ServerOnLaunched;
                _ball.OnHeldTransition += ServerOnHeld;
            }
            else
            {
                // proxies never simulate: park the rigidbody and let snapshots drive the transform
                _ball.Body.isKinematic = true;
                _ball.enabled = false; // no wind FixedUpdate, no collision logs on clients
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_ball == null) return;
            _ball.OnLaunched -= ServerOnLaunched;
            _ball.OnHeldTransition -= ServerOnHeld;
        }

        // ------------------------------------------------------------------ server → clients

        void ServerOnLaunched()
        {
            NetworkObjectReference hitter = default;
            var np = _ball.LastTouchPlayer != null
                ? _ball.LastTouchPlayer.GetComponent<NetworkObject>() : null;
            if (np != null) hitter = np;
            LaunchedRpc(_ball.Spin, _ball.SpinWobble, _ball.LastTouchTeam,
                        _ball.LastHitType, hitter, transform.position);
        }

        void ServerOnHeld() => HeldRpc(transform.position);

        [Rpc(SendTo.NotServer)]
        void LaunchedRpc(float spin, float wobble, TeamSide team, HitType type,
                         NetworkObjectReference hitter, Vector3 at)
        {
            _held = false;
            VolleyPlayer player = null;
            if (hitter.TryGet(out NetworkObject obj)) player = obj.GetComponent<VolleyPlayer>();
            _ball.MirrorLaunch(spin, wobble, team, player, type);

            // the remote contact, seen and heard locally: pose + hit sound at the ball
            player?.TriggerSwing(type == HitType.Serve ? HitType.Serve : type);
            GameAudio.PlayHit(type, at);
        }

        [Rpc(SendTo.NotServer)]
        void HeldRpc(Vector3 pos)
        {
            _held = true;
            _buffer.Clear();
            _ball.MirrorHold();
            transform.position = pos;
        }

        // ------------------------------------------------------------------ client rendering

        /// <summary>Snapshot position from SnapshotSync (server tick attached).</summary>
        public void OnBallState(int tick, Vector3 pos)
        {
            if (IsServer) return;
            for (int i = _buffer.Count - 1; i >= 0; i--)
                if (_buffer[i].tick == tick) return;
            _buffer.Add(new Sample { tick = tick, pos = pos });
            _buffer.Sort((a, b) => a.tick.CompareTo(b.tick));
            while (_buffer.Count > 12) _buffer.RemoveAt(0);
        }

        void Update()
        {
            if (!IsSpawned || IsServer || _buffer.Count == 0) return;

            // while held, the serve-position updates ride the snapshots too — but a held ball
            // sits still (or tracks the server player), so plain interpolation is fine as well
            var clock = SimClock.Instance;
            float renderTick = (clock != null ? clock.EstimatedServerTick : _buffer[_buffer.Count - 1].tick)
                               - DelayTicks;

            Sample a = _buffer[0], b = _buffer[_buffer.Count - 1];
            for (int i = 0; i < _buffer.Count - 1; i++)
                if (_buffer[i].tick <= renderTick && _buffer[i + 1].tick >= renderTick)
                {
                    a = _buffer[i];
                    b = _buffer[i + 1];
                    break;
                }
            float span = Mathf.Max(1, b.tick - a.tick);
            float u = Mathf.Clamp01((renderTick - a.tick) / span);
            transform.position = Vector3.Lerp(a.pos, b.pos, u);
        }
    }
}
