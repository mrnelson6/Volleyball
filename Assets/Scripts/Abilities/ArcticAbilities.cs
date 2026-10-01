using System.Collections.Generic;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// ICE RINK (Pingo): the opponents' court freezes over — no grip: they speed up slowly,
    /// can't stop, and overshoot everything. Counterplay: move early, move less.
    /// </summary>
    public class IceRinkAbility : Ability
    {
        readonly List<GameObject> _fx = new List<GameObject>();

        public override void Begin()
        {
            float half = (CourtGeometry.HalfDepth + 1.5f) * 0.5f;
            FieldZones.Add(new FieldZone
            {
                kind = ZoneKind.Ice, team = Opp,
                center = new Vector2(0f, Toward * (0.1f + half)), halfSize = new Vector2(CourtGeometry.HalfWidth + 1.5f, half),
                startTick = P.startTick, endTick = P.startTick + FieldZones.Ticks(Def.duration), ownerKey = ZoneKey,
            });
            if (!AbilityFx.CanRender) return;
            GameObject ice = AbilityFx.Box("Ice Rink", new Color(0.75f, 0.92f, 1f, 0.55f));
            ice.transform.position = new Vector3(0f, 0.03f, Toward * (0.1f + half));
            ice.transform.localScale = new Vector3((CourtGeometry.HalfWidth + 1.5f) * 2f, 0.01f, half * 2f);
            _fx.Add(ice);
            var rng = new System.Random(P.seed);
            for (int i = 0; i < 6; i++)
            {
                GameObject glint = AbilityFx.Box("Ice Glint", new Color(1f, 1f, 1f, 0.8f));
                glint.transform.position = new Vector3((float)(rng.NextDouble() * 2 - 1) * CourtGeometry.HalfWidth, 0.04f,
                                                       Toward * (1f + (float)rng.NextDouble() * (CourtGeometry.HalfDepth - 1f)));
                glint.transform.rotation = Quaternion.Euler(0f, 30f + i * 25f, 0f);
                glint.transform.localScale = new Vector3(0.9f, 0.01f, 0.06f);
                _fx.Add(glint);
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
    /// GLIDE (Ola): for a while, holding jump in the air floats Ola down slowly (with extra air
    /// control) — hang at the net for long blocks, or drift onto a ball. Lives in the player
    /// simulation. Counterplay: a gliding owl is a slow owl.
    /// </summary>
    public class GlideAbility : Ability
    {
        public override string Hint => $"Hold {AbilityKeys.Jump} in the air to glide";

        float _puffT;

        public override void Begin()
        {
            if (Authority) Owner.StartGlide(Def.duration);
        }

        public override void Update(float dt)
        {
            if (Owner.IsGrounded || Owner.VerticalVelocity > -1.0f || Owner.VerticalVelocity < -1.2f) return;
            _puffT -= dt;
            if (_puffT > 0f) return;
            _puffT = 0.1f;
            AbilityFx.Puff(Owner.transform.position + Vector3.up * 0.9f, new Color(1f, 1f, 1f, 0.9f), 1, 1f, 0.14f, 0.6f);
        }
    }

    /// <summary>
    /// ICEBERG (Wally): a solid wall of ice erupts at Wally's side of the net, 2m wide and well
    /// above the tape. Attacks into it bounce straight back. Counterplay: go round it.
    /// </summary>
    public class IcebergAbility : Ability
    {
        const float Width = 2.2f;
        GameObject _berg;

        public override bool Plan(ref AbilityParams p)
        {
            float own = CourtGeometry.SideSign(Team);
            float x = Mathf.Clamp(Owner.GroundPosition.x, -CourtGeometry.HalfWidth + Width * 0.5f, CourtGeometry.HalfWidth - Width * 0.5f);
            p.a = new Vector3(x, 0f, own * 0.5f);
            return true;
        }

        public override void Begin()
        {
            float h = CourtGeometry.NetHeight + 1.3f;
            _berg = AbilityFx.SolidBox("Iceberg", new Vector3(P.a.x, h * 0.5f, P.a.z), new Vector3(Width, h, 0.55f),
                                       new Color(0.72f, 0.9f, 1f));
            AbilityFx.Puff(P.a + Vector3.up * 0.5f, new Color(0.9f, 0.97f, 1f, 1f), 16, 3.5f, 0.28f, 0.6f);
        }

        public override void End()
        {
            if (_berg != null) AbilityFx.Puff(_berg.transform.position, new Color(0.85f, 0.95f, 1f, 1f), 14, 3f, 0.25f, 0.5f);
            AbilityFx.Kill(_berg);
            Physics.SyncTransforms();
        }
    }

    /// <summary>
    /// BLIZZARD (Boris): a gale blows toward the opponents' baseline for a few seconds — Boris's
    /// side's shots carry deep, theirs hang and fall short — and snow whips across their half.
    /// Counterplay: hit harder and flatter into the wind.
    /// </summary>
    public class BlizzardAbility : Ability
    {
        const float Wind = 4.5f;
        readonly List<GameObject> _flakes = new List<GameObject>();
        GameObject _haze;

        public override void Begin()
        {
            if (Authority) PowerUpDirector.SetExtraWind(new Vector3(0f, 0f, Toward * Wind));
            if (!AbilityFx.CanRender) return;
            _haze = AbilityFx.Box("Blizzard Haze", new Color(0.95f, 0.97f, 1f, 0.28f));
            _haze.transform.position = new Vector3(0f, 1.6f, Toward * CourtGeometry.HalfDepth * 0.55f);
            _haze.transform.localScale = new Vector3(CourtGeometry.HalfWidth * 2.6f, 3.2f, CourtGeometry.HalfDepth * 1.1f);
            for (int i = 0; i < 40; i++)
            {
                GameObject f = AbilityFx.Ball("Snowflake", Color.white);
                f.transform.localScale = Vector3.one * 0.09f;
                f.transform.position = RandomFlake();
                _flakes.Add(f);
            }
        }

        Vector3 RandomFlake() => new Vector3(Random.Range(-CourtGeometry.HalfWidth - 2f, CourtGeometry.HalfWidth + 2f),
                                             Random.Range(0.2f, 5f), Toward * Random.Range(-1f, CourtGeometry.HalfDepth + 2f));

        public override void Update(float dt)
        {
            foreach (var f in _flakes)
            {
                Vector3 p = f.transform.position + new Vector3(Random.Range(-0.5f, 0.5f), -1.2f, Toward * 6f) * dt;
                if (p.y < 0.05f || Mathf.Abs(p.z) > CourtGeometry.HalfDepth + 3f || p.z * Toward < -1.5f) p = RandomFlake();
                f.transform.position = p;
            }
        }

        public override void End()
        {
            if (Authority) PowerUpDirector.SetExtraWind(Vector3.zero);
            foreach (var f in _flakes) AbilityFx.Kill(f);
            _flakes.Clear();
            AbilityFx.Kill(_haze);
        }
    }
}
