using System.Collections.Generic;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// BALANCE (Rumi): Rumi's next pass (a set or bump that stays on her side) freezes at the top
    /// of its arc for a second — hanging there, perfectly still, for the spiker. Counterplay:
    /// everyone can see exactly where the attack is coming from.
    /// </summary>
    public class BalanceAbility : Ability
    {
        const int MomentFreeze = 1;
        const float Hang = 1.0f;
        bool _watching, _frozen;
        float _frozenAt;
        Vector3 _at;
        float _spinT;
        readonly List<GameObject> _fx = new List<GameObject>();

        public override void OnBallLaunched(VolleyPlayer by, HitType type)
        {
            if (Done) return;
            if (_watching || _frozen) { Finish(); return; } // someone played it: over
            if (by != Owner) return;
            Vector3 land = PredictLanding(out float t);
            if (CourtGeometry.SideOf(land) != Team || t < 0.5f) return; // not a pass to our side
            _watching = true;
        }

        public override void FixedTick(float dt)
        {
            if (!Authority || Done) return;
            if (_frozen)
            {
                if (Elapsed - _frozenAt > Hang + 0.1f) Finish();
                return;
            }
            if (!_watching || Ball == null || Ball.Body.isKinematic) return;
            if (Ball.Body.linearVelocity.y <= 0.2f)
            {
                Ball.Freeze(Hang);
                _frozen = true;
                _frozenAt = Elapsed;
                Announce(MomentFreeze, Ball.transform.position);
            }
        }

        public override void OnMoment(int code, Vector3 at, Vector3 dir)
        {
            if (code != MomentFreeze) return;
            _frozen = true;
            _at = at;
            if (!AbilityFx.CanRender) return;
            for (int i = 0; i < 6; i++)
            {
                GameObject leaf = AbilityFx.Ball("Balance Leaf", i % 2 == 0 ? new Color(0.45f, 0.8f, 0.3f) : new Color(1f, 0.85f, 0.3f));
                leaf.transform.localScale = new Vector3(0.18f, 0.05f, 0.1f);
                _fx.Add(leaf);
            }
        }

        public override void Update(float dt)
        {
            if (_fx.Count == 0) return;
            _spinT += dt;
            for (int i = 0; i < _fx.Count; i++)
            {
                float a = _spinT * 4f + i * Mathf.PI / 3f;
                _fx[i].transform.position = _at + new Vector3(Mathf.Cos(a) * 0.6f, Mathf.Sin(_spinT * 3f + i) * 0.15f, Mathf.Sin(a) * 0.6f);
            }
            if (_spinT > Hang + 0.2f) Clear();
        }

        void Clear()
        {
            foreach (var g in _fx) AbilityFx.Kill(g);
            _fx.Clear();
        }

        public override void End() => Clear();
        protected override float ExtraLifetime => 3f;
    }

    /// <summary>
    /// AVALANCHE (Yara): the opponents' back court turns white for a second — the warning — then
    /// giant snowballs roll right across it, flattening anyone caught deep. Counterplay: step up
    /// and give away the back court for a moment.
    /// </summary>
    public class AvalancheAbility : Ability
    {
        const float Warning = 1.0f;
        const float RunTime = 1.3f;
        const float StartX = 9.5f;
        const float BallRadius = 0.75f;
        static readonly float[] LaneFrac = { 0.62f, 0.8f, 0.98f };  // of the court depth
        static readonly float[] Lag = { 0f, 0.35f, 0.15f };           // seconds behind the first

        readonly List<GameObject> _fx = new List<GameObject>();
        readonly List<Transform> _balls = new List<Transform>();
        float _dir;

        public override bool Plan(ref AbilityParams p)
        {
            p.a = new Vector3(Random.value < 0.5f ? -1f : 1f, 0f, 0f);
            return Match != null && Match.State == MatchState.Rallying;
        }

        Vector3 SnowPos(int i, float runT)
        {
            float t = runT - Lag[i];
            return new Vector3(_dir * (-StartX + t * (2f * StartX) / RunTime), BallRadius,
                               Toward * CourtGeometry.HalfDepth * LaneFrac[i]);
        }

        public override void Begin()
        {
            _dir = P.a.x >= 0f ? 1f : -1f;
            if (!AbilityFx.CanRender) return;
            float zMid = Toward * CourtGeometry.HalfDepth * 0.8f;
            GameObject band = AbilityFx.Box("Avalanche Warning", new Color(0.85f, 0.93f, 1f, 0.5f));
            band.transform.position = new Vector3(0f, 0.03f, zMid);
            band.transform.localScale = new Vector3(CourtGeometry.RoamHalfWidth * 2f, 0.01f, CourtGeometry.HalfDepth * 0.5f);
            _fx.Add(band);
            for (int i = 0; i < LaneFrac.Length; i++)
            {
                GameObject b = AbilityFx.Ball("Snowball", new Color(0.97f, 0.98f, 1f));
                b.transform.localScale = Vector3.one * BallRadius * 2f;
                b.SetActive(false);
                _balls.Add(b.transform);
                _fx.Add(b);
            }
        }

        public override void FixedTick(float dt)
        {
            float runT = Elapsed - Warning;
            if (!Authority || runT < 0f) return;
            for (int i = 0; i < LaneFrac.Length; i++)
            {
                float t = runT - Lag[i];
                if (t < 0f || t > RunTime) continue;
                Vector3 sp = SnowPos(i, runT);
                foreach (var o in Opponents())
                {
                    Vector3 d = o.GroundPosition - new Vector3(sp.x, 0f, sp.z);
                    if (d.magnitude > BallRadius + 0.35f || !o.IsGrounded || !o.CanBeKnockedDown) continue;
                    o.KnockDown(new Vector3(_dir, 0f, 0f));
                }
            }
        }

        public override void Update(float dt)
        {
            if (_fx.Count == 0) return;
            float runT = Elapsed - Warning;
            var band = _fx[0].GetComponent<Renderer>();
            Color c = band.sharedMaterial.color;
            c.a = runT < 0f ? 0.35f + 0.25f * Mathf.Sin(Elapsed * 16f) : Mathf.Max(0f, c.a - dt);
            band.sharedMaterial.color = c;
            for (int i = 0; i < _balls.Count; i++)
            {
                float t = runT - Lag[i];
                bool on = t >= 0f && t <= RunTime + 0.3f;
                if (_balls[i].gameObject.activeSelf != on) _balls[i].gameObject.SetActive(on);
                if (!on) continue;
                _balls[i].position = SnowPos(i, runT);
                _balls[i].Rotate(Vector3.forward, -_dir * 900f * dt, Space.World);
                if (Random.value < 0.35f)
                    AbilityFx.Puff(_balls[i].position - Vector3.up * 0.5f, new Color(1f, 1f, 1f, 0.9f), 1, 1.5f, 0.25f, 0.4f);
            }
        }

        public override void End()
        {
            foreach (var g in _fx) AbilityFx.Kill(g);
            _fx.Clear();
            _balls.Clear();
        }
    }

    /// <summary>
    /// CLIFF HOP (Mako): a rock ledge bursts up under Mako on his side, lifting him 1.25m — jump
    /// from up there and his spikes and blocks come from a height nobody else has. It's solid:
    /// the ball bounces off it too. Counterplay: it's a big rock in his own court.
    /// </summary>
    public class CliffHopAbility : Ability
    {
        const float Height = 1.25f;
        const float Half = 0.8f;
        GameObject _rock;

        public override bool Plan(ref AbilityParams p)
        {
            if (!Owner.IsGrounded || Owner.GroundHeight > 0.05f) return false;
            Vector3 c = Owner.GroundPosition;
            float own = CourtGeometry.SideSign(Team);
            c.x = Mathf.Clamp(c.x, -CourtGeometry.HalfWidth, CourtGeometry.HalfWidth);
            c.z = own * Mathf.Clamp(Mathf.Abs(c.z), Half + 0.45f, CourtGeometry.HalfDepth);
            p.a = c;
            return true;
        }

        public override void Begin()
        {
            _rock = AbilityFx.SolidBox("Cliff Ledge", new Vector3(P.a.x, Height * 0.5f, P.a.z),
                                       new Vector3(Half * 2f, Height, Half * 2f), new Color(0.55f, 0.52f, 0.48f));
            if (Authority) Owner.LiftTo(P.a, Height);
            AbilityFx.Puff(P.a + Vector3.up * 0.3f, new Color(0.7f, 0.66f, 0.6f, 1f), 16, 3.5f, 0.3f, 0.6f);
        }

        public override void End()
        {
            AbilityFx.Kill(_rock);
            Physics.SyncTransforms();
        }
    }

    /// <summary>
    /// PHANTOM STRIKE (Sasha): Sasha's next spike goes nearly invisible — no shadow, no trail,
    /// only the odd flicker — until someone touches it. The AI reads it late too. Counterplay:
    /// watch Sasha's arm and the flickers.
    /// </summary>
    public class PhantomStrikeAbility : Ability
    {
        const int MomentShot = 1;
        bool _shot;
        float _shotAge;
        Renderer[] _ballRenderers;
        BallTrail _trail;
        DropShadow _shadow;

        /// <summary>A phantom spike is in the air right now (the AI reads it late).</summary>
        public static bool InFlight => AbilityDirector.Find<PhantomStrikeAbility>()?._shot == true;

        public override void OnBallLaunched(VolleyPlayer by, HitType type)
        {
            if (Done) return;
            if (_shot) { Finish(); return; }
            if (by != Owner || type != HitType.Spike) return;
            Announce(MomentShot, Ball.transform.position);
        }

        public override void OnMoment(int code, Vector3 at, Vector3 dir)
        {
            if (code != MomentShot || Ball == null) return;
            _shot = true;
            _shotAge = 0f;
            Transform model = Ball.transform.Find("Model");
            _ballRenderers = model != null ? model.GetComponentsInChildren<Renderer>() : Ball.GetComponentsInChildren<Renderer>();
            _trail = Ball.GetComponent<BallTrail>();
            foreach (var ds in Object.FindObjectsByType<DropShadow>(FindObjectsSortMode.None))
                if (ds.target == Ball.transform) _shadow = ds;
            AbilityFx.Puff(at, new Color(0.8f, 0.85f, 0.95f, 0.8f), 8, 2f, 0.18f, 0.4f);
        }

        public override void Update(float dt)
        {
            if (!_shot) return;
            _shotAge += dt;
            // a clients-side end: the ball got played (or it's been long enough)
            if (Ball == null || Ball.LastTouchPlayer != Owner || Ball.Body.isKinematic && !Authority && _shotAge > 2.5f || _shotAge > 3f)
            {
                Reveal();
                return;
            }
            bool flicker = Mathf.Repeat(_shotAge, 0.3f) < 0.04f;
            SetVisible(flicker);
        }

        void SetVisible(bool on)
        {
            if (_ballRenderers != null) foreach (var r in _ballRenderers) if (r != null) r.enabled = on;
            if (_trail != null) _trail.enabled = on;
            if (_shadow != null) _shadow.enabled = on;
        }

        void Reveal()
        {
            if (!_shot) return;
            _shot = false;
            SetVisible(true);
            if (Authority) Finish();
        }

        public override void End() => Reveal();
        protected override float ExtraLifetime => 3.5f;
    }
}
