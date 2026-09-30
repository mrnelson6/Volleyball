using System.Collections.Generic;
using UnityEngine;

namespace Volleyball
{
    /// <summary>Shared placement for abilities that drop things around the opponents.</summary>
    static class Spots
    {
        /// <summary>Up to three spots on <paramref name="side"/>'s court, each in front of an
        /// opponent (toward the ball / net) and never on top of anyone.</summary>
        public static Vector3[] AroundOpponents(Ability a, IEnumerable<VolleyPlayer> opps, TeamSide side,
                                                Vector3 ballish, float clearance, System.Random rng)
        {
            var list = new List<Vector3>();
            float s = CourtGeometry.SideSign(side);
            foreach (var o in opps)
            {
                Vector3 g = o.GroundPosition;
                Vector3 toward = new Vector3(ballish.x - g.x, 0f, ballish.z - g.z);
                if (toward.sqrMagnitude < 0.5f) toward = new Vector3(0f, 0f, -s); // toward the net
                list.Add(g + toward.normalized * 1.7f);
            }
            while (list.Count < 3)
                list.Add(new Vector3((float)(rng.NextDouble() * 2 - 1) * (CourtGeometry.HalfWidth - 1f), 0f,
                                     s * (2f + (float)rng.NextDouble() * (CourtGeometry.HalfDepth - 3f))));
            for (int i = 0; i < list.Count; i++)
            {
                Vector3 p = list[i];
                p.x = Mathf.Clamp(p.x, -CourtGeometry.HalfWidth + 0.5f, CourtGeometry.HalfWidth - 0.5f);
                p.z = s * Mathf.Clamp(Mathf.Abs(p.z), 1.2f, CourtGeometry.HalfDepth - 0.5f);
                // never drop it on someone: nudge sideways until clear
                for (int k = 0; k < 6 && TooClose(opps, p, clearance); k++)
                    p.x = Mathf.Clamp(p.x + (k % 2 == 0 ? 1.1f : -2.2f), -CourtGeometry.HalfWidth + 0.5f, CourtGeometry.HalfWidth - 0.5f);
                list[i] = p;
            }
            return list.GetRange(0, 3).ToArray();
        }

        static bool TooClose(IEnumerable<VolleyPlayer> ps, Vector3 p, float c)
        {
            foreach (var o in ps)
                if ((o.GroundPosition - p).magnitude < c) return true;
            return false;
        }

        /// <summary>Pack three XZ spots into two Vector3 params (a.y = x3, b.y = z3).</summary>
        public static void Pack(Vector3[] s, ref AbilityParams p)
        {
            p.a = new Vector3(s[0].x, s[2].x, s[0].z);
            p.b = new Vector3(s[1].x, s[2].z, s[1].z);
        }

        public static Vector3[] Unpack(in AbilityParams p) => new[]
        {
            new Vector3(p.a.x, 0f, p.a.z), new Vector3(p.b.x, 0f, p.b.z), new Vector3(p.a.y, 0f, p.b.y),
        };
    }

    /// <summary>
    /// CUBE DROP (Wanda): three sturdy cubes thump down onto the opponents' court — solid:
    /// they block movement (and can be stood on) and the ball glances off them. Shadows warn
    /// where they'll land. Counterplay: they don't move — play around them.
    /// </summary>
    public class CubeDropAbility : Ability
    {
        const float Size = 0.9f;
        const float Warn = 0.6f;
        readonly List<GameObject> _cubes = new List<GameObject>();
        readonly List<GameObject> _shadows = new List<GameObject>();
        Vector3[] _spots;

        public override bool Plan(ref AbilityParams p)
        {
            if (Match == null) return false;
            Vector3 ballish = Ball != null ? Ball.transform.position : CourtGeometry.CourtCenter(Opp);
            var spots = Spots.AroundOpponents(this, Opponents(), Opp, ballish, 1.1f, new System.Random(p.seed));
            Spots.Pack(spots, ref p);
            return true;
        }

        public override void Begin()
        {
            _spots = Spots.Unpack(P);
            if (!AbilityFx.CanRender) return;
            foreach (var s in _spots)
                _shadows.Add(AbilityFx.Disc("Cube Shadow", s, Size * 0.3f, new Color(0f, 0f, 0f, 0.35f), 0.03f));
        }

        public override void Update(float dt)
        {
            if (_cubes.Count > 0 || _spots == null) return;
            float u = Mathf.Clamp01(Elapsed / Warn);
            foreach (var sh in _shadows)
                sh.transform.localScale = new Vector3(Size * (0.6f + 0.7f * u), 0.01f, Size * (0.6f + 0.7f * u));
        }

        public override void FixedTick(float dt)
        {
            if (_cubes.Count > 0 || Elapsed < Warn || _spots == null) return;
            foreach (var s in _spots)
            {
                GameObject c = AbilityFx.SolidBox("Wombat Cube", new Vector3(s.x, Size * 0.5f, s.z),
                                                  Vector3.one * Size, new Color(0.52f, 0.36f, 0.22f));
                _cubes.Add(c);
                AbilityFx.Puff(s + Vector3.up * 0.2f, new Color(0.9f, 0.8f, 0.6f, 1f), 10, 3f, 0.24f, 0.5f);
            }
            foreach (var sh in _shadows) AbilityFx.Kill(sh);
            _shadows.Clear();
            GameAudio.PlayScenery(_spots[0]);
        }

        public override void End()
        {
            foreach (var g in _cubes) AbilityFx.Kill(g);
            foreach (var g in _shadows) AbilityFx.Kill(g);
            _cubes.Clear();
            _shadows.Clear();
            Physics.SyncTransforms();
        }
    }

    /// <summary>
    /// PACK HUNT (Digger): a ghost dingo joins Digger's side for one touch — it runs down the
    /// ball and plays it (a pass to the net, or over on the third touch). Counterplay: it only
    /// ever makes one touch, and it's no blocker.
    /// </summary>
    public class PackHuntAbility : Ability
    {
        const float Speed = 7.5f;
        const int MomentTouch = 1;
        const int MomentGoal = 2;
        Vector3 _pos;
        Vector3 _goal;
        Vector3 _announcedGoal = new Vector3(999f, 0f, 999f);
        GameObject _ghost;
        float _sparkT;

        public override bool Plan(ref AbilityParams p)
        {
            float own = CourtGeometry.SideSign(Team);
            p.a = new Vector3(-Owner.GroundPosition.x * 0.6f, 0f, own * CourtGeometry.HalfDepth * 0.55f);
            return Match != null;
        }

        public override void Begin()
        {
            _pos = P.a;
            _goal = P.a;
            _ghost = AbilityFx.Animal("dingo", new Color(0.55f, 0.85f, 1f), true);
            if (_ghost != null)
            {
                _ghost.transform.position = _pos;
                _ghost.transform.localScale *= 0.9f;
                // spectral: a strong cyan glow over the whole body
                _ghost.GetComponent<ModelCharacterView>()?.SetGlow(new Color(0.45f, 0.9f, 1f), 0.75f);
            }
            AbilityFx.Puff(_pos + Vector3.up * 0.5f, new Color(0.6f, 0.9f, 1f, 0.9f), 14, 3f, 0.24f, 0.6f);
        }

        public override void FixedTick(float dt)
        {
            // The ghost runs to where the ball will come down on our side (else holds the middle).
            // Only the authority can predict that (a client's ball is a replicated shell with no
            // velocity), so it announces the goal whenever it moves and clients follow.
            float own = CourtGeometry.SideSign(Team);
            if (Authority)
            {
                _goal = new Vector3(0f, 0f, own * CourtGeometry.HalfDepth * 0.5f);
                if (BallInFlight)
                {
                    Vector3 land = PredictLanding(out _);
                    if (CourtGeometry.SideOf(land) == Team) _goal = land;
                }
                if ((_goal - _announcedGoal).sqrMagnitude > 0.6f * 0.6f)
                {
                    _announcedGoal = _goal;
                    Announce(MomentGoal, _goal);
                }
            }
            Vector3 d = _goal - _pos;
            d.y = 0f;
            _pos += Vector3.ClampMagnitude(d, Speed * dt);

            if (!Authority || !BallInFlight || !Ball.CanBeHit) return;
            if (Match != null && !Match.CanTeamTouch(Team)) return;
            Vector3 bp = Ball.transform.position;
            if (CourtGeometry.SideOf(bp) != Team || bp.y > 2.6f || bp.y < 0.2f) return;
            if (new Vector2(bp.x - _pos.x, bp.z - _pos.z).magnitude > 1.4f) return;

            bool third = Match != null && Match.Possession == Team && Match.Touches >= 2;
            if (third)
                AbilityShots.Strike(Owner, VolleyPlayer.CourtAimPoint(Team, Vector2.zero, 0.65f, 0.25f, 0.4f), HitType.Bump);
            else
            {
                Ball.LaunchTo(new Vector3(Mathf.Clamp(bp.x * 0.5f, -2.5f, 2.5f), 0.6f, own * 2.0f), 3.0f, Team, Owner, HitType.Bump);
                Match?.RegisterTouch(Team, Owner);
                GameAudio.PlayHit(HitType.Bump, bp);
            }
            Announce(MomentTouch, bp);
            Finish();
        }

        public override void Update(float dt)
        {
            if (_ghost == null) return;
            Vector3 prev = _ghost.transform.position;
            Vector3 next = Vector3.Lerp(prev, _pos, 1f - Mathf.Exp(-12f * dt));
            Vector3 mv = next - prev;
            if (mv.sqrMagnitude > 1e-5f) _ghost.transform.rotation = Quaternion.LookRotation(new Vector3(mv.x, 0f, mv.z));
            _ghost.transform.position = next;
            _sparkT -= dt;
            if (_sparkT <= 0f)
            {
                _sparkT = 0.12f;
                AbilityFx.Puff(next + Vector3.up * 0.6f, new Color(0.6f, 0.9f, 1f, 0.7f), 1, 0.8f, 0.14f, 0.5f);
            }
        }

        public override void OnMoment(int code, Vector3 at, Vector3 dir)
        {
            if (code == MomentTouch) AbilityFx.Ring(new Vector3(at.x, 0f, at.z), new Color(0.6f, 0.9f, 1f, 1f), 1.5f, 0.35f);
            else if (code == MomentGoal && !Authority) _goal = at;
        }

        public override void End()
        {
            if (_ghost != null) AbilityFx.Puff(_ghost.transform.position + Vector3.up * 0.5f, new Color(0.6f, 0.9f, 1f, 0.9f), 12, 2.5f, 0.22f, 0.5f);
            AbilityFx.Kill(_ghost);
        }
    }

    /// <summary>
    /// BIG STRIDE (Ezra): for a while Ezra runs faster and every jump is a huge bound — the
    /// momentum it carries is more than double running speed. Great for covering court, hard
    /// to stop. Counterplay: a bound commits; put the ball where he's bounding from.
    /// </summary>
    public class BigStrideAbility : Ability
    {
        float _puffT;

        public override void Begin()
        {
            if (Authority) Owner.StartStride(Def.duration);
        }

        public override void Update(float dt)
        {
            _puffT -= dt;
            if (_puffT > 0f || Owner.IsGrounded) return;
            _puffT = 0.08f;
            AbilityFx.Puff(Owner.transform.position + Vector3.up * 0.8f, new Color(0.45f, 0.4f, 0.35f, 0.9f), 1, 0.6f, 0.14f, 0.5f);
        }
    }

    /// <summary>
    /// TRAMPOLINE (Kip): a bounce pad appears under Kip. Anyone on Kip's team who jumps from it
    /// goes much higher — a launch pad for spikes and blocks. Counterplay: it stays put.
    /// </summary>
    public class TrampolineAbility : Ability
    {
        const float Radius = 1.1f;
        readonly List<GameObject> _fx = new List<GameObject>();

        public override bool Plan(ref AbilityParams p)
        {
            if (!Owner.IsGrounded) return false;
            p.a = Owner.GroundPosition;
            return true;
        }

        public override void Begin()
        {
            FieldZones.Add(new FieldZone
            {
                kind = ZoneKind.Pad, center = new Vector2(P.a.x, P.a.z), radius = Radius, team = Team,
                startTick = P.startTick, endTick = P.startTick + FieldZones.Ticks(Def.duration), ownerKey = ZoneKey,
            });
            if (!AbilityFx.CanRender) return;
            _fx.Add(AbilityFx.Disc("Trampoline", P.a, Radius, new Color(0.2f, 0.35f, 0.9f, 0.95f), 0.06f));
            _fx.Add(AbilityFx.Disc("Trampoline Mat", P.a, Radius * 0.78f, new Color(0.1f, 0.1f, 0.15f, 1f), 0.07f));
            AbilityFx.Ring(P.a, new Color(0.4f, 0.6f, 1f, 1f), Radius * 1.6f, 0.4f);
        }

        public override void Update(float dt)
        {
            // boing: a ring whenever a teammate launches off it
            if (Match == null) return;
            foreach (var p in Match.players)
            {
                if (p == null || p.team != Team) continue;
                Vector3 g = p.transform.position;
                if (new Vector2(g.x - P.a.x, g.z - P.a.z).magnitude < Radius && p.VerticalVelocity > p.jumpSpeed * 1.1f && g.y < 0.5f)
                    AbilityFx.Ring(P.a, new Color(0.5f, 0.7f, 1f, 1f), Radius * 1.4f, 0.25f, 0.12f);
            }
        }

        public override void End()
        {
            FieldZones.RemoveOwned(ZoneKey);
            foreach (var g in _fx) AbilityFx.Kill(g);
            _fx.Clear();
        }
    }
}
