using System.Collections.Generic;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// FOX TRICK (Finn): armed until Finn's next shot over the net, which splits in two — the real
    /// ball and a decoy flying to a different spot. The decoy is pure view (it can't score or be
    /// played) and pops the moment someone reaches it or it lands. Counterplay: the decoy casts
    /// no shadow. The AI can be fooled too — each opposing AI believes one of the two balls
    /// until the last moment.
    /// </summary>
    public class FoxTrickAbility : Ability
    {
        public override string Hint => "Armed: your next shot over splits in two";

        const int MomentSplit = 1;

        public override void OnBallLaunched(VolleyPlayer by, HitType type)
        {
            if (by != Owner || Done || Ball == null) return;
            Vector3 land = PredictLanding(out float t);
            if (t < 0.35f || CourtGeometry.SideOf(land) != Opp) return; // not a shot over — stay armed

            // Send the decoy somewhere clearly different: across the court, a little deeper or shorter.
            var rng = new System.Random(P.seed);
            float dx = Mathf.Abs(land.x) > 1.2f ? -2f * land.x : (land.x >= 0f ? -3.2f : 3.2f);
            float dz = (float)(rng.NextDouble() * 3.0 - 1.5);
            Vector3 decoyLand = new Vector3(
                Mathf.Clamp(land.x + dx, -CourtGeometry.HalfWidth + 0.4f, CourtGeometry.HalfWidth - 0.4f), 0f,
                Toward * Mathf.Clamp(Mathf.Abs(land.z + dz * Toward), 1.2f, CourtGeometry.HalfDepth - 0.4f));
            Vector3 v = Ball.Body.linearVelocity;
            Vector3 dv = new Vector3((decoyLand.x - land.x) / t, 0f, (decoyLand.z - land.z) / t);
            Announce(MomentSplit, Ball.transform.position, v + dv);
            Done = true;
        }

        public override void OnMoment(int code, Vector3 at, Vector3 dir)
        {
            if (code != MomentSplit) return;
            DecoyBall.Spawn(at, dir, P.seed);
            AbilityFx.Puff(at, new Color(1f, 0.6f, 0.2f, 0.9f), 8, 2f, 0.16f, 0.4f);
        }
    }

    /// <summary>
    /// The Fox Trick decoy: a ballistic look-alike of the ball with no shadow. Simulated
    /// identically on every machine from the split moment (position + velocity), so it needs no
    /// replication of its own. It never interacts with gameplay — it only fools eyes (and AI).
    /// </summary>
    public class DecoyBall : MonoBehaviour
    {
        static readonly List<DecoyBall> _live = new List<DecoyBall>();

        Vector3 _v;
        int _seed;
        float _age;

        /// <summary>The decoy currently in the air, if any.</summary>
        public static DecoyBall Current => _live.Count > 0 ? _live[_live.Count - 1] : null;

        public Vector3 Position => transform.position;
        public Vector3 Velocity => _v;

        /// <summary>Seconds until it reaches the sand.</summary>
        public float TimeToLand
        {
            get
            {
                float g = Mathf.Max(0.1f, -Physics.gravity.y);
                float y = transform.position.y - 0.3f;
                return (_v.y + Mathf.Sqrt(Mathf.Max(0f, _v.y * _v.y + 2f * g * Mathf.Max(y, 0f)))) / g;
            }
        }

        /// <summary>Does this AI believe the decoy is the real ball? A fixed coin per player, so
        /// the two defenders usually split up — one chases each ball.</summary>
        public bool Fools(VolleyPlayer ai) => ((_seed >> 3) + ai.BodyOrder) % 2 == 0;

        public static void Spawn(Vector3 at, Vector3 velocity, int seed)
        {
            ClearAll();
            var go = new GameObject("Decoy Ball");
            go.transform.position = at;
            var d = go.AddComponent<DecoyBall>();
            d._v = velocity;
            d._seed = seed;
            _live.Add(d);

            if (!AbilityFx.CanRender) return;
            // look exactly like the ball: copy its visual model (minus the held-ball helper)
            BallController ball = AbilityDirector.Ball;
            Transform model = ball != null ? ball.transform.Find("Model") : null;
            if (model != null)
            {
                GameObject look = Instantiate(model.gameObject, go.transform, false);
                look.transform.localPosition = Vector3.zero;
                var held = look.GetComponent<HeldBallView>();
                if (held != null) Destroy(held);
            }
            else
            {
                GameObject b = AbilityFx.Ball("Decoy Look", new Color(1f, 0.95f, 0.85f));
                b.transform.SetParent(go.transform, false);
                b.transform.localScale = Vector3.one * 0.6f;
            }
        }

        public static void ClearAll()
        {
            foreach (var d in _live)
                if (d != null) Destroy(d.gameObject);
            _live.Clear();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            _v += Physics.gravity * dt;
            transform.position += _v * dt;

            bool landed = transform.position.y <= 0.3f;
            bool reached = false;
            TeamSide side = CourtGeometry.SideOf(transform.position);
            foreach (var p in FindObjectsByType<VolleyPlayer>(FindObjectsSortMode.None))
            {
                // only a defender on the side it's dropping into can "reach" it — and not in
                // the first moments, while it's still leaving the shooter's hand
                if (_age < 0.3f || p.team != side) continue;
                Vector3 d = p.transform.position - transform.position;
                if (new Vector2(d.x, d.z).magnitude < 0.9f && transform.position.y - p.transform.position.y < 2.3f)
                {
                    reached = true;
                    break;
                }
            }
            if (landed || reached || _age > 5f) Pop();
        }

        void Pop()
        {
            AbilityFx.Puff(transform.position, new Color(1f, 0.65f, 0.25f, 0.9f), 12, 3f, 0.2f, 0.5f);
            AbilityFx.Ring(new Vector3(transform.position.x, 0f, transform.position.z),
                           new Color(1f, 0.6f, 0.2f, 0.9f), 1.2f, 0.4f);
            _live.Remove(this);
            Destroy(gameObject);
        }

        void OnDestroy() => _live.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _live.Clear();
    }

    /// <summary>
    /// BEAR SLAM (Bruno): armed for a while; the next time Bruno lands from a jump the sand
    /// erupts — every opponent standing within range (typically their blockers at the net) is
    /// knocked flat. Counterplay: be in the air when he lands, or stay off the net.
    /// </summary>
    public class BearSlamAbility : Ability
    {
        public override string Hint => "Armed: jump, and land to slam the sand";

        const int MomentSlam = 1;
        const float Radius = 3.6f;
        float _airTime;
        bool _wasAir;

        public override void FixedTick(float dt)
        {
            if (!Authority || Done) return;
            bool air = !Owner.IsGrounded;
            if (air) _airTime += dt;
            if (_wasAir && !air && _airTime > 0.25f)
            {
                Vector3 at = Owner.GroundPosition;
                foreach (var o in Opponents())
                {
                    Vector3 d = o.GroundPosition - at;
                    if (d.magnitude > Radius || !o.IsGrounded || !o.CanBeKnockedDown) continue;
                    o.KnockDown(d.sqrMagnitude > 1e-4f ? d : new Vector3(0f, 0f, Toward));
                }
                Announce(MomentSlam, at);
                Done = true;
            }
            if (!air) _airTime = 0f;
            _wasAir = air;
        }

        public override void OnMoment(int code, Vector3 at, Vector3 dir)
        {
            if (code != MomentSlam) return;
            AbilityFx.Ring(at, new Color(0.85f, 0.55f, 0.25f, 1f), Radius, 0.45f, 0.35f);
            AbilityFx.Ring(at, new Color(1f, 0.85f, 0.55f, 0.8f), Radius * 0.6f, 0.3f, 0.2f);
            AbilityFx.Puff(at + Vector3.up * 0.2f, new Color(0.93f, 0.82f, 0.6f, 1f), 18, 5f, 0.3f, 0.7f);
            GameAudio.PlayScenery(at);
        }
    }
}
