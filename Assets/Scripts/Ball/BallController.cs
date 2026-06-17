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

        /// <summary>Raised on the first frame the ball touches the ground.</summary>
        public System.Action<Vector3> OnGroundHit;

        public Rigidbody Body => _rb;
        public bool CanBeHit => Time.time >= _hitLockUntil;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        /// <summary>Freeze the ball at a position (used while waiting to serve).</summary>
        public void Hold(Vector3 pos)
        {
            _rb.isKinematic = true;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            transform.position = pos;
            LastTouchTeam = TeamSide.None;
            LastTouchPlayer = null;
        }

        /// <summary>
        /// Launch from the current position to <paramref name="target"/>, peaking
        /// <paramref name="apexHeight"/> above the start. Records who touched it.
        /// </summary>
        public void LaunchTo(Vector3 target, float apexHeight, TeamSide team, VolleyPlayer player)
        {
            _rb.isKinematic = false;

            Vector3 start = transform.position;
            float g = -Physics.gravity.y;
            apexHeight = Mathf.Max(apexHeight, 0.3f);

            float apexY = start.y + apexHeight;
            float dropHeight = Mathf.Max(apexY - target.y, 0.05f);
            float tUp = Mathf.Sqrt(2f * apexHeight / g);
            float tDown = Mathf.Sqrt(2f * dropHeight / g);
            float t = Mathf.Max(tUp + tDown, 0.1f);

            Vector3 horizontal = new Vector3(target.x - start.x, 0f, target.z - start.z) / t;
            float vy = Mathf.Sqrt(2f * g * apexHeight);

            _rb.linearVelocity = new Vector3(horizontal.x, vy, horizontal.z);
            _rb.angularVelocity = Vector3.zero;

            LastTouchTeam = team;
            LastTouchPlayer = player;
            _hitLockUntil = Time.time + 0.12f;
        }

        void OnCollisionEnter(Collision c)
        {
            if (c.gameObject.GetComponent<GroundMarker>() != null)
                OnGroundHit?.Invoke(transform.position);
        }
    }
}
