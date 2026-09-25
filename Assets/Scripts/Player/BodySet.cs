using System.Collections.Generic;
using UnityEngine;

namespace Volleyball
{
    /// <summary>One other player's body as <see cref="VolleyPlayer.Simulate"/> sees it this tick.</summary>
    public struct BodyEntry
    {
        public VolleyPlayer player;   // identity only (skip self); the sim never reads its state
        public Vector3 position;
        public float radius;
        public float height;
        public int order;             // stable tie-break when two bodies sit exactly on top of each other
    }

    /// <summary>
    /// Every player's body at the START of a tick — an explicit input to
    /// <see cref="VolleyPlayer.Simulate"/>, so player-vs-player pushing stays a pure function of
    /// (state, command, bodies, dt) and doesn't depend on which player happened to step first.
    /// Wraps a shared buffer (no allocation per tick); <see cref="Empty"/> means "alone on court".
    /// </summary>
    public readonly struct BodyFrame
    {
        readonly BodyEntry[] _entries;
        public readonly int Count;

        public BodyFrame(BodyEntry[] entries, int count)
        {
            _entries = entries;
            Count = count;
        }

        public BodyEntry this[int i] => _entries[i];
        public static BodyFrame Empty => default;
    }

    /// <summary>
    /// Captures the tick-start body frame for the live match. Positions come from
    /// <see cref="VolleyPlayer.CollisionPosition"/>: the simulated position for players this
    /// machine steps, and the latest server snapshot for online proxies (fresher than their
    /// interpolated, ~100ms-behind view). Driven by <see cref="BodyTick"/>.
    /// </summary>
    public static class BodySet
    {
        static readonly BodyEntry[] Buffer = new BodyEntry[8];
        static int _count;

        public static BodyFrame Frame => new BodyFrame(Buffer, _count);

        public static void Capture(IReadOnlyList<VolleyPlayer> players)
        {
            _count = 0;
            if (players == null) return;
            for (int i = 0; i < players.Count && _count < Buffer.Length; i++)
            {
                VolleyPlayer p = players[i];
                if (p == null || !p.gameObject.activeInHierarchy) continue;
                Buffer[_count++] = new BodyEntry
                {
                    player = p,
                    position = p.CollisionPosition,
                    radius = p.bodyRadius,
                    height = p.bodyHeight,
                    order = p.BodyOrder,
                };
            }
        }

        public static void Clear() => _count = 0;
    }
}
