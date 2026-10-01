using System.Collections.Generic;
using UnityEngine;

namespace Volleyball
{
    /// <summary>Shots struck by an ability rather than a hit press (a charge's power bump).</summary>
    public static class AbilityShots
    {
        /// <summary>
        /// The shortest flight time (flattest, fastest arc) from <paramref name="start"/> to
        /// <paramref name="target"/> that still clears the net tape by <paramref name="margin"/>.
        /// </summary>
        public static float FlightTimeClearingNet(Vector3 start, Vector3 target, float margin = 0.4f)
        {
            float g = -Physics.gravity.y;
            float dz = target.z - start.z;
            bool crosses = Mathf.Abs(dz) > 1e-3f && (start.z - CourtGeometry.NetZ) * (target.z - CourtGeometry.NetZ) < 0f;
            for (float t = 0.55f; t <= 2.6f; t += 0.05f)
            {
                if (!crosses) return t;
                float vy = (target.y - start.y + 0.5f * g * t * t) / t;
                float tn = (CourtGeometry.NetZ - start.z) / dz * t;
                float y = start.y + vy * tn - 0.5f * g * tn * tn;
                if (y >= CourtGeometry.NetTop + margin) return t;
            }
            return 1.8f;
        }

        /// <summary>AUTHORITY: strike the ball for <paramref name="by"/> toward <paramref name="target"/>
        /// on a flat, net-clearing arc. Counts as their touch. Returns false if the rules forbid it.</summary>
        public static bool Strike(VolleyPlayer by, Vector3 target, HitType type)
        {
            BallController ball = AbilityDirector.Ball;
            MatchManager match = by.Match;
            if (ball == null || !ball.CanBeHit) return false;
            if (match != null && !match.CanTeamTouch(by.team)) return false;
            float t = FlightTimeClearingNet(ball.transform.position, target);
            ball.LaunchTo(target, 0f, by.team, by, type, t);
            match?.RegisterTouch(by.team, by);
            GameAudio.PlayHit(HitType.Spike, ball.transform.position);
            by.TriggerSwing(type);
            VBLog.Event($"ABILITY STRIKE by '{by.name}' team={by.team} target={VBLog.V(target)} flight={t:F2}");
            return true;
        }
    }

    /// <summary>
    /// BURROW (Pip): dig in and pop up right under where the ball is coming down on your side.
    /// Refuses (meter kept) unless the ball is actually dropping onto Pip's court. Counterplay:
    /// Pip still has to make a clean touch — and the surfacing takes a moment.
    /// </summary>
    public class BurrowAbility : Ability
    {
        const float HideSeconds = 0.45f;
        const int MomentUp = 1;
        bool _surfaced;
        GameObject _mound;

        public override bool Plan(ref AbilityParams p)
        {
            if (Match == null || Match.State != MatchState.Rallying || Ball == null || Ball.Body.isKinematic)
                return false;
            Vector3 land = PredictLanding(out float t);
            if (CourtGeometry.SideOf(land) != Team || t < HideSeconds + 0.1f) return false;
            // surface a touch behind the landing spot so the ball drops onto the hands
            float own = CourtGeometry.SideSign(Team);
            Vector3 target = land + new Vector3(0f, 0f, own * 0.35f);
            target.x = Mathf.Clamp(target.x, -CourtGeometry.RoamHalfWidth + 0.5f, CourtGeometry.RoamHalfWidth - 0.5f);
            target.z = own * Mathf.Clamp(Mathf.Abs(target.z), 0.6f, CourtGeometry.RoamHalfDepth - 0.5f);
            p.a = target;
            p.b = Owner.GroundPosition;
            return true;
        }

        public override void Begin()
        {
            if (Authority) Owner.BurrowTo(P.a, HideSeconds);
            AbilityFx.Puff(P.b + Vector3.up * 0.2f, new Color(0.85f, 0.72f, 0.5f, 1f), 14, 3.5f, 0.24f, 0.6f);
            if (AbilityFx.CanRender)
                _mound = AbilityFx.Disc("Burrow Mound", P.a, 0.55f, new Color(0.6f, 0.45f, 0.28f, 0.9f), 0.04f);
        }

        public override void Update(float dt)
        {
            if (_surfaced || Elapsed < HideSeconds) return;
            _surfaced = true;
            AbilityFx.Puff(P.a + Vector3.up * 0.2f, new Color(0.85f, 0.72f, 0.5f, 1f), 16, 4f, 0.26f, 0.6f);
            AbilityFx.Ring(P.a, new Color(0.9f, 0.75f, 0.45f, 0.9f), 1.4f, 0.35f);
            AbilityFx.Kill(_mound);
        }

        public override void End() => AbilityFx.Kill(_mound);
    }

    /// <summary>
    /// STAMPEDE (Zuri): a lane across the opponents' court fills with dust for a second — the
    /// warning — then a zebra herd thunders through it, bowling over anyone still standing in
    /// it. The lane is picked on whichever opponent is nearest the ball. Counterplay: move.
    /// </summary>
    public class StampedeAbility : Ability
    {
        const float Warning = 1.0f;
        const float RunTime = 1.2f;
        const float HalfLane = 1.2f;
        const float StartX = 9.5f;
        static readonly float[] ZebraDz = { -0.7f, 0.15f, 0.75f, -0.25f };
        static readonly float[] ZebraDx = { 0f, -1.6f, -0.8f, -2.6f };

        readonly List<GameObject> _fx = new List<GameObject>();
        readonly List<Transform> _zebras = new List<Transform>();
        float _dir;

        public override bool Plan(ref AbilityParams p)
        {
            if (Match == null || Match.State != MatchState.Rallying) return false;
            VolleyPlayer target = null;
            float best = float.MaxValue;
            Vector3 bp = Ball != null ? Ball.transform.position : Vector3.zero;
            foreach (var o in Opponents())
            {
                float d = (o.GroundPosition - new Vector3(bp.x, 0f, bp.z)).sqrMagnitude;
                if (d < best) { best = d; target = o; }
            }
            float z = target != null ? Mathf.Abs(target.GroundPosition.z) : CourtGeometry.HalfDepth * 0.5f;
            z = Mathf.Clamp(z, HalfLane + 0.2f, CourtGeometry.HalfDepth - 0.3f);
            p.a = new Vector3(Random.value < 0.5f ? -1f : 1f, 0f, Toward * z);
            return true;
        }

        public override void Begin()
        {
            _dir = P.a.x >= 0f ? 1f : -1f;
            if (!AbilityFx.CanRender) return;

            // the warning: a pulsing dust band across the whole width
            float w = CourtGeometry.RoamHalfWidth * 2f;
            GameObject band = AbilityFx.Box("Stampede Lane", new Color(0.95f, 0.55f, 0.2f, 0.5f));
            band.transform.position = new Vector3(0f, 0.03f, P.a.z);
            band.transform.localScale = new Vector3(w, 0.01f, HalfLane * 2f);
            _fx.Add(band);
            foreach (float edge in new[] { -HalfLane, HalfLane })
            {
                GameObject line = AbilityFx.Box("Stampede Edge", new Color(1f, 0.35f, 0.15f, 1f));
                line.transform.position = new Vector3(0f, 0.04f, P.a.z + edge);
                line.transform.localScale = new Vector3(w, 0.01f, 0.14f);
                _fx.Add(line);
            }

            GameObject prefab = CharacterModels.LoadPrefab("zebra");
            for (int i = 0; i < ZebraDz.Length; i++)
            {
                if (prefab == null) break;
                GameObject z = Object.Instantiate(prefab);
                z.name = "Stampede Zebra";
                z.GetComponent<AnimalLook>()?.Set("zebra", Owner.jerseyColor);
                var view = z.GetComponent<ModelCharacterView>();
                if (view != null) view.PlayShowcase(view.run);
                z.transform.rotation = Quaternion.LookRotation(new Vector3(_dir, 0f, 0f));
                z.SetActive(false);
                _zebras.Add(z.transform);
                _fx.Add(z);
            }
        }

        Vector3 ZebraPos(int i, float runT)
            => new Vector3(_dir * (-StartX + runT * (2f * StartX) / RunTime) + _dir * ZebraDx[i], 0f,
                           P.a.z + ZebraDz[i]);

        public override void FixedTick(float dt)
        {
            float runT = Elapsed - Warning;
            if (!Authority || runT < 0f || runT > RunTime) return;
            for (int i = 0; i < ZebraDz.Length; i++)
            {
                Vector3 zp = ZebraPos(i, runT);
                foreach (var o in Opponents())
                {
                    Vector3 d = o.GroundPosition - zp;
                    if (new Vector2(d.x, d.z).magnitude > 0.95f) continue;
                    if (!o.IsGrounded || !o.CanBeKnockedDown) continue; // jump the herd!
                    o.KnockDown(new Vector3(_dir, 0f, Mathf.Sign(d.z) * 0.4f));
                }
            }
        }

        public override void Update(float dt)
        {
            if (_fx.Count == 0) return;
            float runT = Elapsed - Warning;
            float pulse = 0.4f + 0.2f * Mathf.Sin(Elapsed * 18f); // the warning must be unmissable
            for (int i = 0; i < 3 && i < _fx.Count; i++)
            {
                var r = _fx[i].GetComponent<Renderer>();
                if (r == null) continue;
                Color c = r.sharedMaterial.color;
                c.a = runT < 0f ? pulse + (i > 0 ? 0.4f : 0f) : Mathf.Max(0f, c.a - dt);
                r.sharedMaterial.color = c;
            }
            for (int i = 0; i < _zebras.Count; i++)
            {
                bool on = runT >= 0f && runT <= RunTime + 0.4f;
                if (_zebras[i].gameObject.activeSelf != on) _zebras[i].gameObject.SetActive(on);
                if (!on) continue;
                _zebras[i].position = ZebraPos(i, runT);
                if (Random.value < 0.25f)
                    AbilityFx.Puff(_zebras[i].position + Vector3.up * 0.1f, new Color(0.9f, 0.8f, 0.6f, 0.9f), 1, 1.5f, 0.3f, 0.5f);
            }
        }

        public override void End()
        {
            foreach (var g in _fx) AbilityFx.Kill(g);
            _fx.Clear();
            _zebras.Clear();
        }
    }

    /// <summary>
    /// MUD WALLOW (Waldo): a patch of the opponents' court (between their two players) turns
    /// to mud for several seconds — running, diving and jumping in it are sluggish.
    /// Counterplay: play around it; it doesn't follow you.
    /// </summary>
    public class MudWallowAbility : Ability
    {
        const float Radius = 2.6f;
        readonly List<GameObject> _fx = new List<GameObject>();

        public override bool Plan(ref AbilityParams p)
        {
            if (Match == null) return false;
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (var o in Opponents()) { sum += o.GroundPosition; n++; }
            Vector3 c = n > 0 ? sum / n : CourtGeometry.CourtCenter(Opp);
            c.x = Mathf.Clamp(c.x, -CourtGeometry.HalfWidth + 1f, CourtGeometry.HalfWidth - 1f);
            c.z = Toward * Mathf.Clamp(Mathf.Abs(c.z), 1.8f, CourtGeometry.HalfDepth - 1.2f);
            p.a = c;
            p.f = Radius;
            return true;
        }

        public override void Begin()
        {
            FieldZones.Add(new FieldZone
            {
                kind = ZoneKind.Mud, center = new Vector2(P.a.x, P.a.z), radius = P.f,
                startTick = P.startTick, endTick = P.startTick + FieldZones.Ticks(Def.duration),
            });
            if (!AbilityFx.CanRender) return;
            _fx.Add(AbilityFx.Disc("Mud", P.a, P.f, new Color(0.42f, 0.28f, 0.16f, 0.92f), 0.025f));
            var rng = new System.Random(P.seed);
            for (int i = 0; i < 7; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = (float)rng.NextDouble() * P.f * 0.75f;
                Vector3 at = P.a + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                _fx.Add(AbilityFx.Disc("Mud Blob", at, 0.25f + (float)rng.NextDouble() * 0.35f,
                                       new Color(0.3f, 0.19f, 0.1f, 0.95f), 0.035f));
            }
            AbilityFx.Puff(P.a + Vector3.up * 0.3f, new Color(0.45f, 0.3f, 0.17f, 1f), 14, 3f, 0.28f, 0.6f);
        }

        public override void Update(float dt)
        {
            // the odd bubble popping
            if (_fx.Count > 0 && Random.value < dt * 5f)
            {
                Vector2 r = Random.insideUnitCircle * P.f * 0.8f;
                AbilityFx.Puff(P.a + new Vector3(r.x, 0.05f, r.y), new Color(0.38f, 0.25f, 0.14f, 1f), 3, 0.8f, 0.14f, 0.4f);
            }
        }

        public override void End()
        {
            foreach (var g in _fx) AbilityFx.Kill(g);
            _fx.Clear();
        }
    }

    /// <summary>
    /// TALL ORDER (Gigi): the net shoots up a metre for a few seconds. Attacks planned for the
    /// old tape clip the new one; Gigi's reach still gets over it. Counterplay: tip it, go
    /// high and deep, or wait it out.
    /// </summary>
    public class TallOrderAbility : Ability
    {
        const float Rise = 1.0f;
        const float Ramp = 0.35f;

        public override void FixedTick(float dt) => Apply();
        public override void Update(float dt) => Apply();

        void Apply()
        {
            float up = Mathf.Clamp01(Elapsed / Ramp) * Mathf.Clamp01((Def.duration - Elapsed) / Ramp);
            NetDynamics.SetExtra(Rise * up);
        }

        public override void Begin()
        {
            AbilityFx.Puff(new Vector3(0f, CourtGeometry.NetHeight, 0f), new Color(1f, 0.85f, 0.35f, 0.9f), 14, 3f, 0.2f, 0.5f);
        }

        public override void End() => NetDynamics.Reset();
    }

    /// <summary>
    /// ROAR (Leo): a mighty roar across the net. Opponents inside the cone in front of Leo are
    /// stunned for a moment and their next touches are shaky. Counterplay: spacing — the cone
    /// has a range and an angle — and being in the air.
    /// </summary>
    public class RoarAbility : Ability
    {
        const float Range = 9.5f;
        const float HalfAngle = 55f;
        const float StunSeconds = 0.8f;

        static readonly PowerUpDef Rattled = new PowerUpDef
        {
            displayName = "Rattled", duration = 3f, oppErrorMult = 2.2f, color = new Color(0.95f, 0.65f, 0.2f),
        };

        readonly List<GameObject> _stars = new List<GameObject>();

        public override bool Plan(ref AbilityParams p)
        {
            Vector3 me = Owner.GroundPosition;
            Vector3 aim = CourtGeometry.CourtCenter(Opp) - me;
            aim.y = 0f;
            p.a = me;
            p.b = aim.sqrMagnitude > 1e-4f ? aim.normalized : new Vector3(0f, 0f, Toward);
            return true;
        }

        public override void Begin()
        {
            if (Authority)
            {
                foreach (var o in Opponents())
                {
                    Vector3 d = o.GroundPosition - P.a;
                    if (d.magnitude > Range || Vector3.Angle(d, P.b) > HalfAngle) continue;
                    if (!o.IsGrounded) continue; // caught mid-air: the roar washes past
                    o.Stun(StunSeconds);
                    o.Power.Inflict(Rattled);
                }
            }
            for (int i = 0; i < 3; i++)
                AbilityFx.Ring(P.a + P.b * (1.5f + i * 2.5f), new Color(1f, 0.7f, 0.25f, 0.8f - i * 0.2f),
                               2f + i * 1.6f, 0.5f + i * 0.12f, 0.25f);
        }

        public override void Update(float dt)
        {
            if (!AbilityFx.CanRender) return;
            // stun stars circling the heads of whoever is stunned
            int k = 0;
            foreach (var o in Opponents())
            {
                if (!o.IsStunned) continue;
                for (int s = 0; s < 3; s++, k++)
                {
                    while (_stars.Count <= k) _stars.Add(AbilityFx.Ball("Stun Star", new Color(1f, 0.95f, 0.3f)));
                    float a = Elapsed * 9f + s * 2.1f;
                    float top = o.transform.position.y + o.bodyHeight + 0.25f;
                    _stars[k].SetActive(true);
                    _stars[k].transform.position = o.transform.position + new Vector3(Mathf.Cos(a) * 0.4f, top, Mathf.Sin(a) * 0.4f);
                    _stars[k].transform.localScale = Vector3.one * 0.14f;
                }
            }
            for (int i = k; i < _stars.Count; i++) _stars[i].SetActive(false);
        }

        public override void End()
        {
            foreach (var g in _stars) AbilityFx.Kill(g);
            _stars.Clear();
        }
    }

    /// <summary>
    /// CHARGE (Rocco): a straight bulldozing dash across Rocco's court. If the ball comes into
    /// reach on the way he smashes it over on a flat, fast arc — a free power shot that counts as
    /// his touch. Anyone in the path goes flying. Counterplay: the line is predictable, and a
    /// charge in the wrong direction is a wasted rhino.
    /// </summary>
    public class ChargeAbility : Ability
    {
        const float Speed = 13f;
        const int MomentSmash = 1;
        bool _struck;
        float _dustT;

        public override bool Plan(ref AbilityParams p)
        {
            if (Match != null && Match.IsServePhaseFor(Owner)) return false; // not while holding your own serve
            Vector2 dir = Owner.LastMoveDir;
            // the AI (and a human standing still) charges at the ball if it's coming down on our side
            Vector3 land = PredictLanding(out float t);
            if (!Owner.IsHuman || dir.sqrMagnitude < 0.01f)
            {
                if (Ball != null && CourtGeometry.SideOf(land) == Team)
                {
                    Vector3 d = land - Owner.GroundPosition;
                    dir = new Vector2(d.x, d.z);
                }
                if (dir.sqrMagnitude < 0.01f) dir = new Vector2(0f, Toward);
            }
            p.b = new Vector3(dir.x, 0f, dir.y).normalized;
            p.f = Speed;
            return true;
        }

        public override void Begin()
        {
            if (Authority) Owner.StartDash(new Vector2(P.b.x, P.b.z), P.f, Def.duration);
        }

        public override void FixedTick(float dt)
        {
            if (!Authority) return;

            // anyone in the way (opponents who wandered round the net) goes flying
            foreach (var o in Opponents())
            {
                Vector3 d = o.GroundPosition - Owner.GroundPosition;
                if (new Vector2(d.x, d.z).magnitude < 1.0f && o.CanBeKnockedDown && o.IsGrounded)
                    o.KnockDown(P.b);
            }

            if (_struck || Ball == null || Ball.Body.isKinematic) return;
            Vector3 bp = Ball.transform.position;
            if (CourtGeometry.SideOf(bp) != Team) return;
            Vector3 rel = bp - Owner.SimPosition;
            if (new Vector2(rel.x, rel.z).magnitude > 1.8f || rel.y > Owner.hitReachHeight + 0.4f || bp.y < 0f) return;

            Vector3 target = VolleyPlayer.CourtAimPoint(Team, new Vector2(P.b.x, P.b.z) * 0.5f, 0.7f, 0.25f, 0.4f);
            if (AbilityShots.Strike(Owner, target, HitType.Bump))
            {
                _struck = true;
                Announce(MomentSmash, bp);
            }
        }

        public override void Update(float dt)
        {
            if (Elapsed > Def.duration) return;
            _dustT -= dt;
            if (_dustT > 0f) return;
            _dustT = 0.05f;
            AbilityFx.Puff(Owner.transform.position + Vector3.up * 0.15f, new Color(0.88f, 0.78f, 0.58f, 0.9f), 2, 1.5f, 0.3f, 0.45f);
        }

        public override void OnMoment(int code, Vector3 at, Vector3 dir)
        {
            if (code != MomentSmash) return;
            AbilityFx.Ring(new Vector3(at.x, 0f, at.z), new Color(0.8f, 0.82f, 0.9f, 1f), 2.2f, 0.35f, 0.3f);
            AbilityFx.Puff(at, new Color(1f, 1f, 1f, 0.9f), 10, 4f, 0.18f, 0.35f);
        }
    }
}
