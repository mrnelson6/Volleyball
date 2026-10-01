using System.Collections.Generic;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// STICKY PAWS (Rocky): the next ball that comes to Rocky on his side is CAUGHT — held in his
    /// paws for up to 1.5s while he lines up the throw. A human throws with a normal hit press
    /// (aimed with the stick, like any shot); the AI throws after a beat; either way, time up
    /// means an automatic throw deep. The throw is his touch. Counterplay: the catch gives the
    /// defence a moment to set.
    /// </summary>
    public class StickyPawsAbility : Ability
    {
        public override string Hint => _holding ? $"Throw it: {AbilityKeys.Hit}, aim with {AbilityKeys.Stick}" : "Armed: catch the next ball near you";

        const float MaxHold = 1.5f;
        const int MomentCatch = 1;
        bool _holding;
        float _held;

        Vector3 PawPoint => Owner.SimPosition + Vector3.up * (1.25f * Owner.Character.height)
                            + new Vector3(0f, 0f, Toward * 0.45f);

        public override void OnBallLaunched(VolleyPlayer by, HitType type)
        {
            if (_holding) Finish(); // released: thrown by a hit press (or someone else played it)
        }

        public override void FixedTick(float dt)
        {
            if (!Authority || Done || Ball == null) return;
            if (_holding)
            {
                _held += dt;
                Ball.Carry(PawPoint);
                bool throwNow = _held >= MaxHold || (!Owner.IsHuman && _held >= 0.55f);
                if (throwNow)
                {
                    Vector2 aim = Owner.IsHuman ? Owner.LastMoveDir : new Vector2((P.seed % 3 - 1) * 0.8f, Toward * 0.6f);
                    AbilityShots.Strike(Owner, VolleyPlayer.CourtAimPoint(Team, aim, 0.65f, 0.3f, 0.5f), HitType.Spike);
                    Finish();
                }
                return;
            }
            // armed: catch the first ball that comes into Rocky's reach on his side
            if (!BallInFlight || !Ball.CanBeHit || (Match != null && !Match.CanTeamTouch(Team))) return;
            Vector3 bp = Ball.transform.position;
            if (CourtGeometry.SideOf(bp) != Team || bp.y < 0.3f) return;
            Vector3 rel = bp - Owner.SimPosition;
            if (new Vector2(rel.x, rel.z).magnitude > Owner.reach * 0.8f || rel.y > Owner.hitReachHeight) return;
            _holding = true;
            _held = 0f;
            Ball.Carry(PawPoint);
            Announce(MomentCatch, bp);
        }

        public override void OnMoment(int code, Vector3 at, Vector3 dir)
        {
            if (code != MomentCatch) return;
            _holding = true;
            AbilityFx.Puff(at, new Color(0.7f, 0.7f, 0.75f, 0.9f), 8, 2f, 0.15f, 0.35f);
            GameAudio.PlayHit(HitType.Set, at);
        }

        public override void End()
        {
            // never leave the ball stuck in the air if the rally ended mid-hold
            if (Authority && _holding && Ball != null && Ball.IsSuspended && Match != null && Match.State == MatchState.Rallying)
                AbilityShots.Strike(Owner, VolleyPlayer.CourtAimPoint(Team, Vector2.zero, 0.65f, 0.3f, 0.5f), HitType.Spike);
        }

        protected override float ExtraLifetime => MaxHold + 0.5f;
    }

    /// <summary>
    /// WIDE LOAD (Moe): Moe's antlers swell to enormous size for a few seconds. They're solid —
    /// balls glance off them — and his block covers nearly half the net. He's top-heavy while it
    /// lasts (slower). Counterplay: it only covers his side of the net.
    /// </summary>
    public class WideLoadAbility : Ability
    {
        public override string Hint => "Block at the net with your giant antlers";

        static readonly PowerUpDef Wide = new PowerUpDef
        {
            displayName = "Wide Load", duration = 6f, blockReachMult = 2.4f, reachMult = 1.25f, moveMult = 0.85f,
            color = new Color(0.55f, 0.4f, 0.25f),
        };

        GameObject _rack;

        public override void Begin()
        {
            Owner.Power.AddEffect(Wide);

            // a solid antler rack above his head: collides with the BALL only — it sits on the
            // Ignore Raycast layer, which the players' world sweeps never query
            _rack = new GameObject("Wide Load Antlers");
            _rack.layer = 2;
            _rack.transform.SetParent(Owner.transform, false);
            _rack.transform.localPosition = new Vector3(0f, Owner.bodyHeight * 1.02f, 0f);
            var rb = _rack.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var col = _rack.AddComponent<BoxCollider>();
            col.size = new Vector3(2.9f, 0.6f, 0.4f);

            if (!AbilityFx.CanRender) return;
            Color antler = new Color(0.62f, 0.48f, 0.3f);
            foreach (float side in new[] { -1f, 1f })
            {
                GameObject beam = AbilityFx.Box("Antler Beam", antler);
                beam.transform.SetParent(_rack.transform, false);
                beam.transform.localPosition = new Vector3(side * 0.8f, 0f, 0f);
                beam.transform.localScale = new Vector3(1.4f, 0.22f, 0.22f);
                for (int t = 0; t < 3; t++)
                {
                    GameObject tine = AbilityFx.Box("Antler Tine", antler);
                    tine.transform.SetParent(_rack.transform, false);
                    tine.transform.localPosition = new Vector3(side * (0.35f + t * 0.45f), 0.3f, 0f);
                    tine.transform.localScale = new Vector3(0.16f, 0.55f, 0.16f);
                }
            }
            AbilityFx.Puff(_rack.transform.position, new Color(0.7f, 0.55f, 0.35f, 1f), 12, 3f, 0.2f, 0.5f);
        }

        public override void End()
        {
            Owner.Power.RemoveEffect(Wide);
            if (_rack != null) Object.Destroy(_rack);
        }
    }

    /// <summary>
    /// EARTHQUAKE (Butch): the opponents' side of the court shakes for a few seconds — nobody
    /// over there can jump, and running is harder. Time it before their attack and there's no
    /// spike and no block. Counterplay: a standing bump or set still works.
    /// </summary>
    public class EarthquakeAbility : Ability
    {
        readonly List<GameObject> _fx = new List<GameObject>();
        float _dustT;

        public override void Begin()
        {
            float half = (CourtGeometry.RoamHalfDepth - 0.1f) * 0.5f;
            FieldZones.Add(new FieldZone
            {
                kind = ZoneKind.Quake, team = Opp,
                center = new Vector2(0f, Toward * (0.1f + half)), halfSize = new Vector2(CourtGeometry.RoamHalfWidth, half),
                startTick = P.startTick, endTick = P.startTick + FieldZones.Ticks(Def.duration), ownerKey = ZoneKey,
            });
            if (!AbilityFx.CanRender) return;
            var rng = new System.Random(P.seed);
            for (int i = 0; i < 7; i++)
            {
                GameObject crack = AbilityFx.Box("Quake Crack", new Color(0.3f, 0.22f, 0.15f, 0.9f));
                float x = (float)(rng.NextDouble() * 2 - 1) * CourtGeometry.HalfWidth;
                float z = Toward * (1f + (float)rng.NextDouble() * (CourtGeometry.HalfDepth - 1.5f));
                crack.transform.position = new Vector3(x, 0.035f, z);
                crack.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 180f, 0f);
                crack.transform.localScale = new Vector3(1.6f + (float)rng.NextDouble() * 1.5f, 0.01f, 0.09f);
                _fx.Add(crack);
            }
        }

        public override void Update(float dt)
        {
            _dustT -= dt;
            if (_dustT > 0f) return;
            _dustT = 0.06f;
            Vector3 at = new Vector3(Random.Range(-CourtGeometry.HalfWidth, CourtGeometry.HalfWidth), 0.1f,
                                     Toward * Random.Range(0.5f, CourtGeometry.HalfDepth));
            AbilityFx.Puff(at, new Color(0.85f, 0.75f, 0.55f, 0.8f), 2, 2f, 0.25f, 0.5f);
            foreach (var g in _fx) // the cracks judder
                g.transform.position += new Vector3(Random.Range(-0.02f, 0.02f), 0f, Random.Range(-0.02f, 0.02f));
        }

        public override void End()
        {
            FieldZones.RemoveOwned(ZoneKey);
            foreach (var g in _fx) AbilityFx.Kill(g);
            _fx.Clear();
        }
    }

    /// <summary>
    /// NET WALKER (Cora): Cora springs onto the top of the net and prowls along the tape for a
    /// few seconds. Up there she can play any ball near the net on either side, spiking straight
    /// down from a height nobody else has, and blocking everything. Counterplay: hit deep —
    /// from the net she can't reach the back court, and her teammate is on their own.
    /// </summary>
    public class NetWalkerAbility : Ability
    {
        public override string Hint => "On the net: move sideways, hit anything near it";

        float _sparkT;

        // never while holding your own serve: the serve rule pins you behind the baseline
        public override bool Plan(ref AbilityParams p)
            => !Owner.IsKnockedDown && !Owner.IsDiving && !(Match != null && Match.IsServePhaseFor(Owner));

        public override void Begin()
        {
            if (Authority) Owner.StartPerch(Def.duration);
            AbilityFx.Puff(new Vector3(Owner.transform.position.x, CourtGeometry.NetTop, 0f),
                           new Color(0.95f, 0.8f, 0.55f, 1f), 12, 3f, 0.2f, 0.5f);
        }

        public override void Update(float dt)
        {
            if (!Owner.IsPerched) return;
            _sparkT -= dt;
            if (_sparkT > 0f) return;
            _sparkT = 0.1f;
            AbilityFx.Puff(new Vector3(Owner.transform.position.x, CourtGeometry.NetTop, 0f),
                           new Color(1f, 0.9f, 0.6f, 0.8f), 1, 0.8f, 0.12f, 0.4f);
        }
    }
}
