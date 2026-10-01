using System.Collections.Generic;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// HOT SPRING (Cabo): a steaming pool opens around Cabo on his side. Touches made standing
    /// in it are near-perfect, and a ball drifting down over it slows and floats. Counterplay:
    /// attack away from the pool.
    /// </summary>
    public class HotSpringAbility : Ability
    {
        public override string Hint => "Stand in your hot spring for perfect touches";

        const float Radius = 2.2f;
        readonly List<GameObject> _fx = new List<GameObject>();

        public override bool Plan(ref AbilityParams p)
        {
            Vector3 c = Owner.GroundPosition;
            float own = CourtGeometry.SideSign(Team);
            c.x = Mathf.Clamp(c.x, -CourtGeometry.HalfWidth, CourtGeometry.HalfWidth);
            c.z = own * Mathf.Clamp(Mathf.Abs(c.z), Radius * 0.6f, CourtGeometry.HalfDepth);
            p.a = c;
            return true;
        }

        public override void Begin()
        {
            FieldZones.Add(new FieldZone
            {
                kind = ZoneKind.HotSpring, center = new Vector2(P.a.x, P.a.z), radius = Radius, team = Team,
                startTick = P.startTick, endTick = P.startTick + FieldZones.Ticks(Def.duration), ownerKey = ZoneKey,
            });
            if (!AbilityFx.CanRender) return;
            _fx.Add(AbilityFx.Disc("Hot Spring", P.a, Radius, new Color(0.35f, 0.85f, 0.85f, 0.75f), 0.025f));
            _fx.Add(AbilityFx.Disc("Hot Spring Rim", P.a, Radius * 0.75f, new Color(0.6f, 0.95f, 0.95f, 0.6f), 0.03f));
        }

        public override void Update(float dt)
        {
            if (_fx.Count == 0 || Random.value > dt * 14f) return;
            Vector2 r = Random.insideUnitCircle * Radius * 0.85f;
            AbilityFx.Puff(P.a + new Vector3(r.x, 0.1f, r.y), new Color(1f, 1f, 1f, 0.55f), 1, 0.6f, 0.35f, 1.1f);
        }

        public override void End()
        {
            FieldZones.RemoveOwned(ZoneKey);
            foreach (var g in _fx) AbilityFx.Kill(g);
            _fx.Clear();
        }
    }

    /// <summary>
    /// BANANA BALL (Tiko): Tiko's next shot over the net swerves hard late in its flight,
    /// leaving a fruit trail. Counterplay: read the trail — the curve only starts past the apex.
    /// </summary>
    public class BananaBallAbility : Ability
    {
        public override string Hint => _shot ? "" : "Armed: your next shot over will swerve";

        const int MomentShot = 1;
        const float Swerve = 9f;
        bool _shot;
        Vector3 _side;
        float _trailT;
        float _flightAge;

        public override void OnBallLaunched(VolleyPlayer by, HitType type)
        {
            if (Done) return;
            if (_shot) { Finish(); return; } // someone played it: the swerve is spent
            if (by != Owner) return;
            Vector3 land = PredictLanding(out float t);
            if (t < 0.5f || CourtGeometry.SideOf(land) != Opp) return;

            Vector3 v = Ball.Body.linearVelocity;
            Vector3 flat = new Vector3(v.x, 0f, v.z).normalized;
            Vector3 perp = new Vector3(-flat.z, 0f, flat.x);
            // swerve toward the middle when the shot is heading wide, else whichever way the seed says
            float sign = Mathf.Abs(land.x) > 1.5f ? -Mathf.Sign(Vector3.Dot(perp, new Vector3(land.x, 0f, 0f)))
                                                   : (P.seed % 2 == 0 ? 1f : -1f);
            _side = perp * sign;
            _shot = true;
            Announce(MomentShot, Ball.transform.position, _side);
        }

        public override void FixedTick(float dt)
        {
            if (!_shot) return;
            _flightAge += dt;
            if (!Authority || Ball == null) return;
            if (Ball.Body.isKinematic || _flightAge > 3.5f) { Finish(); return; }
            // late swerve: only once the ball is on its way down
            Ball.ExtraAccel = Ball.Body.linearVelocity.y < 0f ? _side * Swerve : Vector3.zero;
        }

        public override void OnMoment(int code, Vector3 at, Vector3 dir)
        {
            if (code != MomentShot) return;
            _shot = true;
            _side = dir;
        }

        public override void Update(float dt)
        {
            if (!_shot || Ball == null) return;
            _trailT -= dt;
            if (_trailT > 0f) return;
            _trailT = 0.05f;
            Color[] fruit = { new Color(1f, 0.25f, 0.2f), new Color(1f, 0.6f, 0.1f), new Color(1f, 0.9f, 0.2f) };
            AbilityFx.Puff(Ball.transform.position, fruit[Random.Range(0, fruit.Length)], 1, 0.3f, 0.16f, 0.7f);
        }

        protected override float ExtraLifetime => 4f;
        public override float Remaining01 => _shot ? 0.5f : base.Remaining01;
    }

    /// <summary>
    /// SLOW-MO (Susu): the ball alone drops to 40% speed for a few seconds — same arc, just
    /// slower — while everyone runs at full pace. For rescuing hopeless rallies.
    /// </summary>
    public class SlowMoAbility : Ability
    {
        float _ringT;

        public override bool Plan(ref AbilityParams p) => Match != null && Match.State == MatchState.Rallying;

        public override void Begin()
        {
            if (Authority && Ball != null) Ball.SetTimeScale(0.4f);
        }

        public override void End()
        {
            if (Authority && Ball != null) Ball.SetTimeScale(1f);
        }

        public override void Update(float dt)
        {
            if (Ball == null) return;
            _ringT -= dt;
            if (_ringT > 0f) return;
            _ringT = 0.25f;
            AbilityFx.Ring(Ball.transform.position + Vector3.down * 0.06f, new Color(0.6f, 0.9f, 0.6f, 0.9f), 1.1f, 0.5f, 0.08f);
        }
    }

    /// <summary>
    /// POUNCE (Jax): a huge leap from anywhere on Jax's side straight to where the ball is
    /// going to be, playing it on arrival — a controlled pass, or straight over on the third
    /// touch. Counterplay: a pounce commits Jax — the ball after it can find the space he left.
    /// </summary>
    public class PounceAbility : Ability
    {
        const int MomentPlay = 1;
        bool _played;

        public override bool Plan(ref AbilityParams p)
        {
            if (Match == null || Match.State != MatchState.Rallying || !BallInFlight || !Owner.IsGrounded) return false;
            // where will the ball be at a playable height (≈1.7m, falling)?
            Vector3 bp = Ball.transform.position, v = Ball.Body.linearVelocity;
            float g = -Ball.EffectiveGravity.y;
            float disc = v.y * v.y + 2f * g * (bp.y - 1.7f);
            if (disc < 0f) return false;
            float t = (v.y + Mathf.Sqrt(disc)) / g;
            Vector3 at = bp + v * t;
            if (CourtGeometry.SideOf(at) != Team || t < 0.35f) return false;
            float air = Mathf.Clamp(t, 0.45f, 1.1f);
            Vector3 target = bp + v * air;
            float own = CourtGeometry.SideSign(Team);
            target.z = own * Mathf.Clamp(Mathf.Abs(target.z), CourtGeometry.NetStandoff + 0.3f, CourtGeometry.RoamHalfDepth - 0.5f);
            p.a = target;
            p.f = air;
            return true;
        }

        public override void Begin()
        {
            if (Authority) Owner.Leap(P.a, P.f);
            AbilityFx.Puff(Owner.transform.position + Vector3.up * 0.2f, new Color(0.95f, 0.8f, 0.45f, 1f), 12, 3.5f, 0.25f, 0.5f);
        }

        public override void FixedTick(float dt)
        {
            if (!Authority || _played || !BallInFlight) return;
            if (!Ball.CanBeHit || (Match != null && !Match.CanTeamTouch(Team))) return;
            Vector3 rel = Ball.transform.position - (Owner.SimPosition + Vector3.up * 1.0f);
            if (rel.magnitude > 1.9f) return;

            float own = CourtGeometry.SideSign(Team);
            bool third = Match != null && Match.Possession == Team && Match.Touches >= 2;
            bool ok = third
                ? AbilityShots.Strike(Owner, VolleyPlayer.CourtAimPoint(Team, Vector2.zero, 0.7f, 0.25f, 0.4f), HitType.Spike)
                : Owner.ExecuteHitAuthoritative(HitType.Bump, AimMode.Explicit,
                      new Vector3(Mathf.Clamp(Ball.transform.position.x * 0.4f, -3f, 3f), 0.6f, own * 2.2f), Vector2.zero);
            if (ok)
            {
                _played = true;
                Announce(MomentPlay, Ball.transform.position);
            }
        }

        public override void OnMoment(int code, Vector3 at, Vector3 dir)
        {
            if (code != MomentPlay) return;
            AbilityFx.Ring(new Vector3(at.x, Owner.transform.position.y, at.z), new Color(1f, 0.8f, 0.3f, 1f), 1.8f, 0.3f, 0.2f);
        }
    }
}
