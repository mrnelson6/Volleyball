using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Rolls the 3D beach-ball mesh (a visual child of the ball) about the axis its motion
    /// implies, so it reads as spinning through the air. View-only: the ball's root transform
    /// IS the simulation state, so this only ever rotates the child it sits on.
    /// </summary>
    public class BallSpin : MonoBehaviour
    {
        public float radius = 0.3f;
        [Tooltip("Extra spin on top of rolling contact, for readability.")]
        public float spinBoost = 0.6f;

        Vector3 _last;

        void OnEnable() => _last = transform.position;

        void LateUpdate()
        {
            Vector3 p = transform.position;
            Vector3 v = p - _last;
            _last = p;
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            float dist = flat.magnitude;
            if (dist < 1e-5f || dist > 3f) return; // resting, or a teleport (serve reset)
            Vector3 axis = Vector3.Cross(Vector3.up, flat / dist);
            float deg = dist / radius * Mathf.Rad2Deg * spinBoost;
            transform.Rotate(axis, deg, Space.World);
        }
    }
}
