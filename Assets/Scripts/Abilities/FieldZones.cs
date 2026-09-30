using System.Collections.Generic;
using UnityEngine;

namespace Volleyball
{
    /// <summary>What a patch of court does to a player standing in it.</summary>
    public enum ZoneKind : byte { Mud }

    /// <summary>One timed patch of changed ground (a circle on the court).</summary>
    public struct FieldZone
    {
        public ZoneKind kind;
        public Vector2 center; // XZ
        public float radius;
        public int startTick;
        public int endTick;    // exclusive

        public bool Covers(Vector3 pos, int tick)
        {
            if (tick < startTick || tick >= endTick) return false;
            float dx = pos.x - center.x, dz = pos.z - center.y;
            return dx * dx + dz * dz <= radius * radius;
        }
    }

    /// <summary>The combined effect of every zone under a player on one tick (1 = unchanged).</summary>
    public struct ZoneEffect
    {
        public float moveMult;
        public float jumpMult;

        public static ZoneEffect None => new ZoneEffect { moveMult = 1f, jumpMult = 1f };
    }

    /// <summary>
    /// Ground-changing ability effects (mud now; ice, holes, quakes later) as an explicit input
    /// to the player simulation. Zones carry the simulation tick they start and end on, and the
    /// simulation asks by the tick it is stepping — so a predicting client that learns of a zone
    /// late replays the earlier ticks with it in place and lands exactly where the server did.
    /// Added identically on every machine by the ability that owns them (from replicated params).
    /// </summary>
    public static class FieldZones
    {
        static readonly List<FieldZone> _zones = new List<FieldZone>();

        public static IReadOnlyList<FieldZone> All => _zones;

        public static void Add(FieldZone z) => _zones.Add(z);

        public static void Clear() => _zones.Clear();

        /// <summary>Remove zones that ended before <paramref name="tick"/>.</summary>
        public static void Prune(int tick) => _zones.RemoveAll(z => z.endTick <= tick);

        /// <summary>How the ground at <paramref name="pos"/> treats a player on <paramref name="tick"/>.</summary>
        public static ZoneEffect Sample(Vector3 pos, int tick)
        {
            ZoneEffect e = ZoneEffect.None;
            for (int i = 0; i < _zones.Count; i++)
            {
                FieldZone z = _zones[i];
                if (!z.Covers(pos, tick)) continue;
                switch (z.kind)
                {
                    case ZoneKind.Mud:
                        e.moveMult *= GameConfig.Instance.mudMoveMult;
                        e.jumpMult *= GameConfig.Instance.mudJumpMult;
                        break;
                }
            }
            return e;
        }

        /// <summary>Convert seconds to whole simulation ticks.</summary>
        public static int Ticks(float seconds) => Mathf.CeilToInt(seconds / Time.fixedDeltaTime);
    }
}
