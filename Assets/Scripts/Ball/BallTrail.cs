using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// A speed-reactive motion trail on the ball: it only emits when the ball is moving fast
    /// (hard spikes and jump serves), and the streak gets longer the faster it goes — selling
    /// the pace. Creates its own <see cref="TrailRenderer"/>; added automatically by
    /// <see cref="BallController"/>. Trail length/visibility is driven by GameConfig.trailMinSpeed.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BallTrail : MonoBehaviour
    {
        Rigidbody _rb;
        TrailRenderer _trail;
        Vector3 _lastPos;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _lastPos = transform.position;

            _trail = gameObject.AddComponent<TrailRenderer>();
            _trail.time = 0.2f;
            _trail.minVertexDistance = 0.05f;
            _trail.widthCurve = AnimationCurve.Linear(0f, 0.34f, 1f, 0f); // ball-wide, tapering to a point
            _trail.numCapVertices = 4;
            _trail.alignment = LineAlignment.View; // face the camera (suits the 2.5D view)
            _trail.textureMode = LineTextureMode.Stretch;
            _trail.autodestruct = false;
            _trail.emitting = false;
            _trail.sortingOrder = -1; // behind the ball sprite

            Shader sh = Shader.Find("Sprites/Default");
            if (sh != null) _trail.material = new Material(sh);

            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f),
                        new GradientColorKey(new Color(1f, 0.55f, 0.15f), 1f) },
                new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            _trail.colorGradient = grad;
        }

        void LateUpdate()
        {
            if (_trail == null) return;

            // a teleport (serve hold/reset) must not smear a streak across the court
            if ((transform.position - _lastPos).sqrMagnitude > 4f) _trail.Clear();
            _lastPos = transform.position;

            float speed = (_rb != null && !_rb.isKinematic) ? _rb.linearVelocity.magnitude : 0f;
            float min = GameConfig.Instance.trailMinSpeed;

            bool fast = speed > min;
            _trail.emitting = fast;
            if (fast) _trail.time = Mathf.Lerp(0.12f, 0.28f, Mathf.InverseLerp(min, 28f, speed));
        }
    }
}
