using System.Collections.Generic;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// DOUBLE JUMP (Juju): for a while Juju can jump again in mid-air (once per leap) — the
    /// highest point anyone reaches. Lives in the player simulation, so it's predicted.
    /// Counterplay: she still has to time the second hop.
    /// </summary>
    public class DoubleJumpAbility : Ability
    {
        float _lastVy;

        public override void Begin()
        {
            if (Authority) Owner.StartDoubleJump(Def.duration);
        }

        public override void Update(float dt)
        {
            float vy = Owner.VerticalVelocity;
            // a mid-air kick upward = the second jump
            if (!Owner.IsGrounded && vy > _lastVy + 3f && Owner.transform.position.y > 0.3f)
            {
                AbilityFx.Ring(Owner.transform.position, new Color(1f, 0.85f, 0.5f, 1f), 1.1f, 0.3f, 0.12f);
                AbilityFx.Puff(Owner.transform.position, new Color(1f, 0.92f, 0.7f, 0.9f), 6, 2f, 0.16f, 0.35f);
            }
            _lastVy = vy;
        }
    }

    /// <summary>
    /// SOUND BLAST (Fifi): a shockwave from those ears shoves the ball in flight sideways along
    /// Fifi's stick (the AI pushes it away from the nearest defender). Not a touch — a nudge on
    /// top of whatever shot is flying. Counterplay: it's a nudge, not a teleport.
    /// </summary>
    public class SoundBlastAbility : Ability
    {
        const float Push = 5.5f;

        public override bool Plan(ref AbilityParams p)
        {
            if (Match == null || Match.State != MatchState.Rallying || !BallInFlight) return false;
            Vector2 dir = Owner.LastMoveDir;
            if (!Owner.IsHuman || dir.sqrMagnitude < 0.01f)
            {
                // push it away from whichever defender is closest to where it's coming down
                Vector3 land = PredictLanding(out _);
                VolleyPlayer near = null;
                float best = float.MaxValue;
                foreach (var o in Opponents())
                {
                    float d = (o.GroundPosition - land).sqrMagnitude;
                    if (d < best) { best = d; near = o; }
                }
                Vector3 away = near != null ? land - near.GroundPosition : new Vector3(1f, 0f, 0f);
                dir = new Vector2(away.x, away.z);
                if (dir.sqrMagnitude < 0.01f) dir = new Vector2(1f, 0f);
            }
            dir.Normalize();
            p.b = new Vector3(dir.x, 0f, dir.y);
            p.a = Ball.transform.position;
            return true;
        }

        public override void Begin()
        {
            if (Authority && BallInFlight) Ball.Body.linearVelocity += P.b * Push;
            Vector3 from = Owner.transform.position + Vector3.up * 1.4f;
            for (int i = 0; i < 3; i++)
                AbilityFx.Ring(Vector3.Lerp(from, P.a, (i + 1) / 3f) + Vector3.down * 0.1f,
                               new Color(1f, 0.85f, 0.55f, 0.9f), 0.8f + i * 0.4f, 0.35f + i * 0.08f, 0.12f);
            AbilityFx.Puff(P.a, new Color(1f, 0.9f, 0.6f, 0.9f), 10, 3f, 0.16f, 0.35f);
        }
    }

    /// <summary>
    /// SANDSTORM DEVIL (Orin): a whirlwind wanders the opponents' court for several seconds. It
    /// shoves anyone it catches and flings a low ball round and up. Its path is the same on every
    /// screen (a replicated seed). Counterplay: keep away from it — and it may help you too.
    /// </summary>
    public class SandstormDevilAbility : Ability
    {
        const float Radius = 1.6f;
        readonly List<GameObject> _fx = new List<GameObject>();
        FieldZone _zone;

        public override bool Plan(ref AbilityParams p)
        {
            p.a = CourtGeometry.CourtCenter(Opp);
            return Match != null;
        }

        public override void Begin()
        {
            _zone = new FieldZone
            {
                kind = ZoneKind.Whirl, center = new Vector2(P.a.x, P.a.z), radius = Radius,
                orbit = new Vector2(CourtGeometry.HalfWidth - 1.2f, CourtGeometry.HalfDepth * 0.35f),
                orbitSpeed = 0.035f, startTick = P.startTick,
                endTick = P.startTick + FieldZones.Ticks(Def.duration), ownerKey = ZoneKey,
            };
            FieldZones.Add(_zone);
            if (!AbilityFx.CanRender) return;
            for (int i = 0; i < 7; i++)
            {
                GameObject puff = AbilityFx.Ball("Sandstorm", new Color(0.9f, 0.75f, 0.5f, 0.55f));
                _fx.Add(puff);
            }
        }

        public override void Update(float dt)
        {
            if (_fx.Count == 0) return;
            int tick = P.startTick + Mathf.RoundToInt(Elapsed / Time.fixedDeltaTime);
            Vector2 c = _zone.CenterAt(tick);
            for (int i = 0; i < _fx.Count; i++)
            {
                float h = i * 0.45f;
                float r = 0.35f + h * 0.28f;                // a funnel, wider at the top
                float a = Elapsed * (9f - i * 0.6f) + i * 1.3f;
                _fx[i].transform.position = new Vector3(c.x + Mathf.Cos(a) * r, 0.2f + h, c.y + Mathf.Sin(a) * r);
                _fx[i].transform.localScale = Vector3.one * (0.5f + h * 0.35f);
            }
            if (Random.value < dt * 20f)
                AbilityFx.Puff(new Vector3(c.x, 0.15f, c.y), new Color(0.9f, 0.78f, 0.55f, 0.8f), 1, 2.5f, 0.22f, 0.5f);
        }

        public override void End()
        {
            FieldZones.RemoveOwned(ZoneKey);
            foreach (var g in _fx) AbilityFx.Kill(g);
            _fx.Clear();
        }
    }

    /// <summary>
    /// OASIS (Cleo): a pool of water opens on Cleo's side — under the ball if it's dropping onto
    /// her court. The first ball to land in it splashes back up instead of scoring, handing her
    /// team another go. Counterplay: land it anywhere else.
    /// </summary>
    public class OasisAbility : Ability
    {
        const float Radius = 1.5f;
        const int MomentSplash = 1;
        readonly List<GameObject> _fx = new List<GameObject>();
        System.Action<Vector3> _onSplash;

        public override bool Plan(ref AbilityParams p)
        {
            float own = CourtGeometry.SideSign(Team);
            Vector3 c = new Vector3(0f, 0f, own * CourtGeometry.HalfDepth * 0.6f);
            if (BallInFlight)
            {
                Vector3 land = PredictLanding(out _);
                if (CourtGeometry.SideOf(land) == Team) c = land;
            }
            c.x = Mathf.Clamp(c.x, -CourtGeometry.HalfWidth - 0.5f, CourtGeometry.HalfWidth + 0.5f);
            c.z = own * Mathf.Clamp(Mathf.Abs(c.z), Radius + 0.3f, CourtGeometry.HalfDepth + 0.5f); // all on our side
            p.a = c;
            return Match != null;
        }

        public override void Begin()
        {
            FieldZones.Add(new FieldZone
            {
                kind = ZoneKind.Oasis, center = new Vector2(P.a.x, P.a.z), radius = Radius,
                startTick = P.startTick, endTick = P.startTick + FieldZones.Ticks(Def.duration), ownerKey = ZoneKey,
            });
            if (Authority && Ball != null)
            {
                _onSplash = at => { Announce(MomentSplash, at); Finish(); };
                Ball.Splashed += _onSplash;
            }
            if (!AbilityFx.CanRender) return;
            _fx.Add(AbilityFx.Disc("Oasis Sand", P.a, Radius * 1.2f, new Color(0.95f, 0.85f, 0.55f, 1f), 0.02f));
            _fx.Add(AbilityFx.Disc("Oasis", P.a, Radius, new Color(0.2f, 0.6f, 0.95f, 0.9f), 0.03f));
            AbilityFx.Ring(P.a, new Color(0.5f, 0.8f, 1f, 1f), Radius * 1.5f, 0.4f);
        }

        public override void OnMoment(int code, Vector3 at, Vector3 dir)
        {
            if (code != MomentSplash) return;
            AbilityFx.Puff(at, new Color(0.4f, 0.75f, 1f, 0.95f), 18, 4.5f, 0.22f, 0.6f);
            AbilityFx.Ring(new Vector3(at.x, 0f, at.z), new Color(0.5f, 0.8f, 1f, 1f), 2f, 0.4f, 0.2f);
        }

        public override void End()
        {
            if (_onSplash != null && Ball != null) Ball.Splashed -= _onSplash;
            FieldZones.RemoveOwned(ZoneKey);
            foreach (var g in _fx) AbilityFx.Kill(g);
            _fx.Clear();
        }
    }
}
