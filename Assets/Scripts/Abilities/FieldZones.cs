using System.Collections.Generic;
using UnityEngine;

namespace Volleyball
{
    /// <summary>What a patch of court does.</summary>
    public enum ZoneKind : byte
    {
        Mud,        // slow runs, weak jumps
        Ice,        // no grip: speed changes slowly
        HotSpring,  // touches made here are near-perfect; a ball over it drifts down gently
        Pad,        // trampoline: jumps go much higher
        Hole,       // step in and you're stuck for a moment
        Quake,      // no jumping, slower runs
        Whirl,      // a moving whirlwind: shoves players, swirls the ball
        Oasis,      // a ball landing in it splashes back up instead of scoring
    }

    /// <summary>One timed patch of changed ground: a circle (radius) or a box (halfSize).</summary>
    public struct FieldZone
    {
        public ZoneKind kind;
        public Vector2 center;   // XZ (a whirl's orbit centre)
        public float radius;     // circle radius (ignored when halfSize is set)
        public Vector2 halfSize; // box half extents on XZ; zero = circle
        /// <summary>Only players on this team feel it; None = everyone.</summary>
        public TeamSide team;
        public int startTick;
        public int endTick;      // exclusive
        /// <summary>Whirl only: the centre wanders ±orbit on X/Z with this angular speed per tick.</summary>
        public Vector2 orbit;
        public float orbitSpeed;
        /// <summary>Which ability made it (so it can take its zones away early).</summary>
        public int ownerKey;

        public Vector2 CenterAt(int tick)
        {
            if (orbitSpeed == 0f) return center;
            float a = (tick - startTick) * orbitSpeed;
            return center + new Vector2(orbit.x * Mathf.Sin(a), orbit.y * Mathf.Sin(a * 1.7f + 0.6f));
        }

        public bool Active(int tick) => tick >= startTick && tick < endTick;

        public bool Contains(Vector3 pos, int tick)
        {
            Vector2 c = CenterAt(tick);
            float dx = pos.x - c.x, dz = pos.z - c.y;
            if (halfSize.x > 0f) return Mathf.Abs(dx) <= halfSize.x && Mathf.Abs(dz) <= halfSize.y;
            return dx * dx + dz * dz <= radius * radius;
        }

        public bool Covers(Vector3 pos, int tick) => Active(tick) && Contains(pos, tick);
    }

    /// <summary>The combined effect of every zone under a player on one tick.</summary>
    public struct ZoneEffect
    {
        public float moveMult;
        public float jumpMult;
        /// <summary>0 = normal instant ground control; &gt;0 = slippery, this much acceleration.</summary>
        public float traction;
        public bool noJump;
        public bool trap;
        public Vector2 push;      // m/s shove
        public float errorMult;   // contact error

        public static ZoneEffect None => new ZoneEffect { moveMult = 1f, jumpMult = 1f, errorMult = 1f };
    }

    /// <summary>
    /// Ground-changing ability effects as an explicit input to the player simulation. Zones
    /// carry the simulation tick they start and end on, and the simulation asks by the tick it
    /// is stepping — so a predicting client that learns of a zone late replays the earlier ticks
    /// with it in place and lands exactly where the server did. Added identically on every
    /// machine by the ability that owns them (from replicated params).
    /// </summary>
    public static class FieldZones
    {
        static readonly List<FieldZone> _zones = new List<FieldZone>();

        public static IReadOnlyList<FieldZone> All => _zones;

        /// <summary>The latest simulation tick stepped on this machine — the clock for things
        /// outside the player simulation (the ball) that ask about zones.</summary>
        public static int CurrentTick { get; internal set; }

        public static void Add(FieldZone z) => _zones.Add(z);

        public static void Clear() => _zones.Clear();

        public static void RemoveOwned(int ownerKey) => _zones.RemoveAll(z => z.ownerKey == ownerKey);

        /// <summary>How the ground at <paramref name="pos"/> treats a player of <paramref name="team"/>
        /// on <paramref name="tick"/>.</summary>
        public static ZoneEffect Sample(Vector3 pos, int tick, TeamSide team)
        {
            ZoneEffect e = ZoneEffect.None;
            var cfg = GameConfig.Instance;
            for (int i = 0; i < _zones.Count; i++)
            {
                FieldZone z = _zones[i];
                if (z.team != TeamSide.None && z.team != team) continue;
                if (!z.Covers(pos, tick)) continue;
                switch (z.kind)
                {
                    case ZoneKind.Mud:
                        e.moveMult *= cfg.mudMoveMult;
                        e.jumpMult *= cfg.mudJumpMult;
                        break;
                    case ZoneKind.Ice:
                        e.traction = e.traction > 0f ? Mathf.Min(e.traction, cfg.iceAccel) : cfg.iceAccel;
                        break;
                    case ZoneKind.HotSpring:
                        e.errorMult *= cfg.hotSpringErrorMult;
                        break;
                    case ZoneKind.Pad:
                        e.jumpMult *= cfg.padJumpMult;
                        break;
                    case ZoneKind.Hole:
                        e.trap = true;
                        break;
                    case ZoneKind.Quake:
                        e.noJump = true;
                        e.moveMult *= cfg.quakeMoveMult;
                        break;
                    case ZoneKind.Whirl:
                    {
                        Vector2 c = z.CenterAt(tick);
                        Vector2 rel = new Vector2(pos.x - c.x, pos.z - c.y);
                        float d = Mathf.Max(rel.magnitude, 0.05f);
                        Vector2 n = rel / d;
                        Vector2 tangent = new Vector2(-n.y, n.x);
                        e.push += (tangent * 0.8f + n * 0.6f) * cfg.whirlPush; // spun round and flung out
                        break;
                    }
                }
            }
            return e;
        }

        /// <summary>Is there an active zone of <paramref name="kind"/> at this point right now?
        /// (For the ball and visuals — the player simulation uses <see cref="Sample"/>.)</summary>
        public static bool Any(ZoneKind kind, Vector3 pos, out FieldZone zone)
        {
            for (int i = 0; i < _zones.Count; i++)
                if (_zones[i].kind == kind && _zones[i].Covers(pos, CurrentTick))
                {
                    zone = _zones[i];
                    return true;
                }
            zone = default;
            return false;
        }

        /// <summary>Convert seconds to whole simulation ticks.</summary>
        public static int Ticks(float seconds) => Mathf.CeilToInt(seconds / Time.fixedDeltaTime);
    }
}
