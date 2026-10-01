using System.Collections.Generic;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// BLINK HOP (Hazel): three charges of an instant short hop — press the power button again
    /// to blink ~3m along the stick. Runs inside the player simulation (sim state), so it is
    /// predicted like any other move. Counterplay: three hops, then she's just a hare.
    /// </summary>
    public class BlinkHopAbility : Ability
    {
        public override string Hint => $"Press {AbilityKeys.Power} to blink ({Owner.BlinkCharges} left)";

        public const int Charges = 3;
        int _lastCharges = -1;

        public override void Begin()
        {
            if (Authority) Owner.GiveBlinks(Charges, Def.duration);
        }

        public override void Update(float dt)
        {
            int c = Owner.BlinkCharges;
            if (_lastCharges > 0 && c < _lastCharges)
            {
                AbilityFx.Puff(Owner.transform.position + Vector3.up * 0.6f, new Color(0.85f, 0.75f, 1f, 0.9f), 10, 2.5f, 0.2f, 0.4f);
                AbilityFx.Ring(Owner.transform.position, new Color(0.8f, 0.7f, 1f, 1f), 1.2f, 0.25f, 0.1f);
            }
            if (c > 0 || _lastCharges < 0) _lastCharges = c;
            if (Authority && _lastCharges == 0 && c == 0 && Elapsed > 0.2f) Finish();
            _lastCharges = c;
        }

        public override float Remaining01 => Mathf.Clamp01(Owner.BlinkCharges / (float)Charges);
    }

    /// <summary>
    /// TUNNEL TRAP (Bram): three holes open up on the opponents' court, just in front of them.
    /// Step into one and you're stuck for a second. The holes are plain to see. Counterplay:
    /// look where you run.
    /// </summary>
    public class TunnelTrapAbility : Ability
    {
        const float Radius = 0.5f;
        readonly List<GameObject> _fx = new List<GameObject>();

        public override bool Plan(ref AbilityParams p)
        {
            if (Match == null) return false;
            Vector3 ballish = Ball != null ? Ball.transform.position : CourtGeometry.CourtCenter(Opp);
            Spots.Pack(Spots.AroundOpponents(this, Opponents(), Opp, ballish, 0.9f, new System.Random(p.seed)), ref p);
            return true;
        }

        public override void Begin()
        {
            int end = P.startTick + FieldZones.Ticks(Def.duration);
            foreach (var s in Spots.Unpack(P))
            {
                FieldZones.Add(new FieldZone
                {
                    kind = ZoneKind.Hole, center = new Vector2(s.x, s.z), radius = Radius, team = Opp,
                    startTick = P.startTick, endTick = end, ownerKey = ZoneKey,
                });
                if (!AbilityFx.CanRender) continue;
                _fx.Add(AbilityFx.Disc("Hole Rim", s, Radius * 1.35f, new Color(0.55f, 0.4f, 0.25f, 1f), 0.03f));
                _fx.Add(AbilityFx.Disc("Hole", s, Radius, new Color(0.06f, 0.04f, 0.03f, 1f), 0.04f));
                AbilityFx.Puff(s + Vector3.up * 0.2f, new Color(0.6f, 0.45f, 0.3f, 1f), 8, 2.5f, 0.2f, 0.5f);
            }
        }

        public override void End()
        {
            FieldZones.RemoveOwned(ZoneKey);
            foreach (var g in _fx) AbilityFx.Kill(g);
            _fx.Clear();
        }
    }

    /// <summary>
    /// RAMPAGE (Iggy): Iggy's next spike carries a battering ram behind it — whoever on the other
    /// side plays it gets the touch away and is knocked flat straight after, leaving their team a
    /// player short for the rest of the rally. Counterplay: let the right player take it.
    /// </summary>
    public class RampageAbility : Ability
    {
        public override string Hint => _shot ? "" : "Armed: flatten whoever digs your next spike";

        const int MomentRam = 1;
        bool _shot;
        float _shotAge;

        public override void OnBallLaunched(VolleyPlayer by, HitType type)
        {
            if (Done) return;
            if (!_shot)
            {
                if (by == Owner && (type == HitType.Spike || type == HitType.Serve))
                {
                    _shot = true;
                    _shotAge = 0f;
                }
                return;
            }
            if (by != null && by.team == Opp)
            {
                Vector3 push = new Vector3(0f, 0f, Toward); // bowled back toward their baseline
                by.KnockDown(push);
                Announce(MomentRam, by.GroundPosition);
            }
            Finish();
        }

        public override void FixedTick(float dt)
        {
            if (!_shot) return;
            _shotAge += dt;
            if (Authority && (_shotAge > 3f || (Ball != null && Ball.Body.isKinematic))) Finish();
        }

        public override void OnMoment(int code, Vector3 at, Vector3 dir)
        {
            if (code != MomentRam) return;
            AbilityFx.Ring(at, new Color(0.7f, 0.4f, 0.25f, 1f), 2f, 0.35f, 0.25f);
            AbilityFx.Puff(at + Vector3.up * 0.4f, new Color(0.9f, 0.8f, 0.6f, 1f), 12, 3.5f, 0.22f, 0.5f);
        }

        protected override float ExtraLifetime => 4f;
    }

    /// <summary>
    /// ANTLER PARRY (Stellan): for a few seconds any ball the opponents send over near Stellan is
    /// swatted straight back by his antlers — but wildly: it can land anywhere, even out.
    /// Counterplay: attack away from him, or make him swat it long.
    /// </summary>
    public class AntlerParryAbility : Ability
    {
        public override string Hint => "Stay near the net to swat balls back";

        const int MomentSwat = 1;
        float _cooldown;

        public override void FixedTick(float dt)
        {
            _cooldown -= dt;
            if (!Authority || _cooldown > 0f || !BallInFlight || !Ball.CanBeHit) return;
            if (Ball.LastTouchTeam != Opp || (Match != null && !Match.CanTeamTouch(Team))) return;
            Vector3 bp = Ball.transform.position;
            if (Mathf.Abs(Owner.GroundPosition.z) > 3.2f) return;           // only from near the net
            if (Mathf.Abs(bp.z) > 1.3f) return;                              // the ball is crossing
            if (Mathf.Abs(bp.x - Owner.GroundPosition.x) > 2.4f) return;
            if (bp.y > Owner.SimPosition.y + Owner.hitReachHeight + 0.9f) return;

            var rng = new System.Random(P.seed + (int)(Elapsed * 1000f));
            Vector3 target = new Vector3((float)(rng.NextDouble() * 2 - 1) * (CourtGeometry.HalfWidth + 1.2f), 0.2f,
                                         Toward * CourtGeometry.HalfDepth * (0.3f + (float)rng.NextDouble() * 0.85f));
            Ball.transform.position = new Vector3(bp.x, bp.y, Toward * Mathf.Max(0.35f, bp.z * Toward)); // on their side of the tape
            Ball.LaunchTo(target, 1.6f, Team, Owner, HitType.Block);
            Match?.RegisterTouch(Team, Owner);
            GameAudio.PlayHit(HitType.Block, bp);
            Owner.TriggerSwing(HitType.Block);
            _cooldown = 0.8f;
            Announce(MomentSwat, bp);
        }

        public override void OnMoment(int code, Vector3 at, Vector3 dir)
        {
            if (code != MomentSwat) return;
            AbilityFx.Puff(at, new Color(0.65f, 0.45f, 0.25f, 1f), 10, 3f, 0.18f, 0.4f);
            AbilityFx.Ring(new Vector3(at.x, 0f, at.z), new Color(0.7f, 0.5f, 0.3f, 1f), 1.6f, 0.3f);
        }
    }
}
