using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// The volleyball. Handles being held (during a serve), being launched along a
    /// ballistic arc toward a target, and reporting the first ground contact of a rally.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BallController : MonoBehaviour
    {
        Rigidbody _rb;
        float _hitLockUntil;

        public TeamSide LastTouchTeam { get; private set; } = TeamSide.None;
        public VolleyPlayer LastTouchPlayer { get; private set; }

        /// <summary>The kind of the most recent contact — lets a spiker tell whether the ball
        /// was actually set to them (own-team Set) versus dug/passed or sent by the opponent.</summary>
        public HitType LastHitType { get; private set; }

        /// <summary>Visual spin in degrees/second (sign = direction); read by the sprite.</summary>
        public float Spin { get; private set; }
        /// <summary>0 = clean spin, &gt;0 = wobbly/chaotic spin (a shanked bump).</summary>
        public float SpinWobble { get; private set; }

        /// <summary>Raised when an Oasis splashes the ball back up instead of it landing.</summary>
        public System.Action<Vector3> Splashed;

        /// <summary>Raised when the ball touches the ground: (point, impactVelocity).</summary>
        public System.Action<Vector3, Vector3> OnGroundHit;

        /// <summary>Raised after every launch (spin/touch state freshly set) — the network
        /// layer replicates the launch so clients mirror spin, trail and contact audio.</summary>
        public System.Action OnLaunched;

        /// <summary>Raised when the ball BECOMES held (not on every reposition while held).</summary>
        public System.Action OnHeldTransition;

        public Rigidbody Body => _rb;

        // ---- ability modifiers (AUTHORITY: the ball only simulates there; clients mirror it) ----

        /// <summary>Ball-only slow motion (Slow-Mo): 1 = normal. The arc keeps its shape — speed
        /// scales by this and gravity by its square — so aim and landing spots are unchanged.</summary>
        public float TimeScale { get; private set; } = 1f;
        /// <summary>The gravity this ball actually falls under right now (predictors use this).</summary>
        public Vector3 EffectiveGravity => Physics.gravity * (TimeScale * TimeScale);
        /// <summary>Extra acceleration for the current flight (a late swerve); cleared on every launch.</summary>
        public Vector3 ExtraAccel { get; set; }
        /// <summary>Frozen in mid-air (Balance) or carried (Sticky Paws) — kinematic, but not held for a serve.</summary>
        public bool IsSuspended => _freezeLeft > 0f || _carried;

        float _freezeLeft;
        Vector3 _frozenVel;
        bool _carried;

        public void SetTimeScale(float scale)
        {
            scale = Mathf.Clamp(scale, 0.05f, 1f);
            if (_rb != null && !_rb.isKinematic) _rb.linearVelocity *= scale / TimeScale;
            if (_freezeLeft > 0f) _frozenVel *= scale / TimeScale;
            TimeScale = scale;
        }

        /// <summary>Stop the ball dead in the air for <paramref name="seconds"/>, then let it carry on
        /// exactly as it was going. Any contact cancels the freeze (it relaunches).</summary>
        public void Freeze(float seconds)
        {
            if (_rb.isKinematic) return;
            _frozenVel = _rb.linearVelocity;
            _rb.linearVelocity = Vector3.zero;
            _rb.isKinematic = true;
            _freezeLeft = seconds;
        }

        /// <summary>Hold the ball in someone's paws (Sticky Paws) — kinematic, placed each tick.
        /// Ended by the next launch (the throw).</summary>
        public void Carry(Vector3 pos)
        {
            if (!_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.isKinematic = true;
            }
            _carried = true;
            _freezeLeft = 0f;
            transform.position = pos;
        }
        public bool CanBeHit => Time.time >= _hitLockUntil;

        /// <summary>Prevent any contact with the ball for a while (e.g. just after a serve).</summary>
        public void LockHits(float duration) => _hitLockUntil = Mathf.Max(_hitLockUntil, Time.time + duration);

        /// <summary>Toss the ball up (a self-toss for a jump serve — not a contact), with an
        /// optional horizontal <paramref name="carryVelocity"/> — the jump-serve toss uses it
        /// to throw the ball forward toward the baseline for the run-up.</summary>
        public void Toss(float upSpeed, Vector3 carryVelocity = default)
        {
            _rb.isKinematic = false;
            _rb.linearVelocity = new Vector3(carryVelocity.x, upSpeed, carryVelocity.z);
            _rb.angularVelocity = Vector3.zero;
            Spin = 0f;
            SpinWobble = 0f;
        }

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            if (GetComponent<BallTrail>() == null) gameObject.AddComponent<BallTrail>();
        }

        void FixedUpdate()
        {
            // regional wind (constant + gusts) pushes the ball while it's in free flight,
            // plus any power-up wind (Cyclone) — which the AI deliberately doesn't predict
            if (_freezeLeft > 0f)
            {
                _freezeLeft -= Time.fixedDeltaTime;
                if (_freezeLeft <= 0f)
                {
                    _rb.isKinematic = false;
                    _rb.linearVelocity = _frozenVel;
                }
                return;
            }
            if (_rb.isKinematic) return;

            float ts2 = TimeScale * TimeScale;
            Vector3 wind = (CourtEnvironment.WindNow(Time.time) + PowerUpDirector.ExtraWind) * ts2;
            if (wind != Vector3.zero) _rb.AddForce(wind, ForceMode.Acceleration);
            if (ts2 < 1f) _rb.AddForce(Physics.gravity * (ts2 - 1f), ForceMode.Acceleration); // slow-mo fall
            if (ExtraAccel != Vector3.zero) _rb.AddForce(ExtraAccel * ts2, ForceMode.Acceleration);

            Vector3 p = transform.position;
            // Hot Spring: a ball drifting down over the steam slows and floats a little
            if (_rb.linearVelocity.y < 0f && FieldZones.Any(ZoneKind.HotSpring, p, out _))
            {
                Vector3 v = _rb.linearVelocity;
                v.y = Mathf.Max(v.y, -5f);
                v.x *= Mathf.Exp(-1.2f * Time.fixedDeltaTime);
                v.z *= Mathf.Exp(-1.2f * Time.fixedDeltaTime);
                _rb.linearVelocity = v;
            }
            // Sandstorm Devil: a low ball caught in the whirlwind is spun round it and lifted
            if (p.y < 4.5f && FieldZones.Any(ZoneKind.Whirl, p, out FieldZone whirl))
            {
                Vector2 c = whirl.CenterAt(FieldZones.CurrentTick);
                Vector2 rel = new Vector2(p.x - c.x, p.z - c.y);
                Vector2 n = rel.sqrMagnitude > 1e-4f ? rel.normalized : Vector2.right;
                Vector3 swirl = new Vector3(-n.y, 0f, n.x) * 16f + Vector3.up * 5f;
                _rb.AddForce(swirl, ForceMode.Acceleration);
            }
        }

        /// <summary>Freeze the ball at a position (used while waiting to serve).</summary>
        public void Hold(Vector3 pos)
        {
            _freezeLeft = 0f;
            _carried = false;
            ExtraAccel = Vector3.zero;
            if (!_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.isKinematic = true;
                OnHeldTransition?.Invoke();
            }
            transform.position = pos;
            LastTouchTeam = TeamSide.None;
            LastTouchPlayer = null;
            LastHitType = HitType.Serve;
            Spin = 0f;
            SpinWobble = 0f;
        }

        /// <summary>
        /// Launch from the current position to <paramref name="target"/>, peaking
        /// <paramref name="apexHeight"/> above the start. Records who touched it.
        /// A positive <paramref name="flightTime"/> overrides the apex-derived arc: the ball
        /// still lands exactly on the target but takes exactly that long — shorter = flatter
        /// and faster (the perfect jump serve). The caller guarantees net clearance.
        /// </summary>
        public void LaunchTo(Vector3 target, float apexHeight, TeamSide team, VolleyPlayer player,
                             HitType type, float flightTime = 0f, bool driveDown = false)
        {
            _rb.isKinematic = false;
            _freezeLeft = 0f;
            _carried = false;
            ExtraAccel = Vector3.zero;

            Vector3 start = transform.position;
            float g = -Physics.gravity.y;

            // A spike is struck from BEHIND the net, so it only earns the downward drive from
            // comfortably above the tape — a low contact driven down clips the net.
            bool spikeFromHigh = type == HitType.Spike && start.y > CourtGeometry.NetTop + 0.6f;

            // A block is different, and used to be held to the spike's rule for no reason: its
            // contact has already been nudged PAST the net plane (see ExecuteBlockAuthoritative),
            // so driving down clears the tape by construction as long as the ball is heading
            // away from it. Whether it earns the drive at all is the caller's call — that's the
            // difference between a stuff and a deflection.
            bool outward = CourtGeometry.SideSign(team.Other()) * (target.z - start.z) > 0f;
            bool blockStuff = driveDown && start.y > CourtGeometry.NetTop && outward;

            Vector3 velocity;
            if (spikeFromHigh || blockStuff)
            {
                // jump spike / over-the-net block: drive it straight down at the target with
                // real pace that scales with how high it was hit, instead of lobbing it.
                // The hitter's power stat then scales that pace directly — raw strength
                // puts real mass behind net contacts, so strong animals hit harder balls
                // (and harder incoming balls are harder for the receiver to control).
                Vector3 dir = (target - start).normalized;
                float pace = Mathf.Clamp(16f + (start.y - CourtGeometry.NetTop) * 4f, 16f, 28f);
                if (player != null) pace *= player.Character.power * player.Power.AttackPaceMult;
                velocity = dir * pace;
            }
            else
            {
                float t, vy;
                if (flightTime > 0f)
                {
                    // explicit flight time: solve the launch velocity that covers the whole
                    // trip in exactly t — flat, fast, and still dead on the target
                    t = flightTime;
                    vy = (target.y - start.y + 0.5f * g * t * t) / t;
                }
                else
                {
                    apexHeight = Mathf.Max(apexHeight, 0.3f);
                    float apexY = start.y + apexHeight;
                    float dropHeight = Mathf.Max(apexY - target.y, 0.05f);
                    float tUp = Mathf.Sqrt(2f * apexHeight / g);
                    float tDown = Mathf.Sqrt(2f * dropHeight / g);
                    t = Mathf.Max(tUp + tDown, 0.1f);
                    vy = Mathf.Sqrt(2f * g * apexHeight);
                }

                Vector3 horizontal = new Vector3(target.x - start.x, 0f, target.z - start.z) / t;
                velocity = new Vector3(horizontal.x, vy, horizontal.z);
            }

            _rb.linearVelocity = velocity * TimeScale; // slow-mo: same arc, slower
            _rb.angularVelocity = Vector3.zero;

            // visual spin by contact type (rolled by the sprite, in the travel direction)
            float travelDir = velocity.z >= 0f ? 1f : -1f;
            switch (type)
            {
                case HitType.Spike: Spin = 900f * travelDir; SpinWobble = 0f; break;
                case HitType.Block: Spin = 700f * travelDir; SpinWobble = 0.4f; break;
                case HitType.Serve: Spin = 520f * travelDir; SpinWobble = 0f; break;
                case HitType.Bump:  Spin = Random.Range(200f, 480f) * (Random.value < 0.5f ? -1f : 1f); SpinWobble = 1f; break;
                case HitType.Dive:  Spin = Random.Range(300f, 600f) * (Random.value < 0.5f ? -1f : 1f); SpinWobble = 1f; break; // shanked off the platform
                default:            Spin = 0f; SpinWobble = 0f; break; // Set: no spin
            }

            LastTouchTeam = team;
            LastTouchPlayer = player;
            LastHitType = type;
            _hitLockUntil = Time.time + 0.12f;
            // The single consolidated contact log is emitted by the caller (which also knows
            // the resulting touch count), so we don't log here.
            OnLaunched?.Invoke();
        }

        /// <summary>Client-side mirror of a server launch: the visual/bookkeeping state a
        /// launch sets, without simulating one — position keeps streaming in snapshots.</summary>
        internal void MirrorLaunch(float spin, float spinWobble, TeamSide team,
                                   VolleyPlayer player, HitType type)
        {
            Spin = spin;
            SpinWobble = spinWobble;
            LastTouchTeam = team;
            LastTouchPlayer = player;
            LastHitType = type;
        }

        /// <summary>Client-side mirror of the ball becoming held for a serve.</summary>
        internal void MirrorHold()
        {
            Spin = 0f;
            SpinWobble = 0f;
            LastTouchTeam = TeamSide.None;
            LastTouchPlayer = null;
            LastHitType = HitType.Serve;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.95f, 0.4f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }

        void OnCollisionEnter(Collision c)
        {
            if (c.gameObject.GetComponent<GroundMarker>() != null)
            {
                // Oasis: a ball landing in the pool splashes back up — the point is not over
                if (FieldZones.Any(ZoneKind.Oasis, transform.position, out _))
                {
                    Vector3 v = _rb.linearVelocity;
                    _rb.linearVelocity = new Vector3(v.x * 0.25f, 7.5f, v.z * 0.25f);
                    GameAudio.PlayNet(transform.position);
                    Splashed?.Invoke(transform.position);
                    VBLog.Event($"OASIS SPLASH at {VBLog.V(transform.position)}");
                    return;
                }
                SandMarks.BallImpact(transform.position, c.relativeVelocity.magnitude);
                // the single landing log is emitted by MatchManager (it also resolves in/out)
                OnGroundHit?.Invoke(transform.position, c.relativeVelocity);
                return;
            }

            // The net rustles; the solid set dressing (stands, palms — see DecorColliders) gives a
            // dull thump instead. The name check keeps arenas built before NetMarker existed right.
            bool isNet = c.gameObject.GetComponent<NetMarker>() != null || c.gameObject.name == "Net";
            if (isNet) GameAudio.PlayNet(transform.position);
            else GameAudio.PlayScenery(transform.position);

            VBLog.Event($"{(isNet ? "NET" : "SCENERY")}/{c.gameObject.name} at {VBLog.V(transform.position)} " +
                        $"impactVel={VBLog.V(c.relativeVelocity)} lastTouch={LastTouchTeam}/'{(LastTouchPlayer != null ? LastTouchPlayer.name : "-")}'");
        }
    }
}
