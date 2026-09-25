using UnityEngine;

namespace Volleyball
{
    /// <summary>Slow drifting sway for the menu's scenic camera (view only).</summary>
    public class MenuCameraDrift : MonoBehaviour
    {
        public Vector3 lookAt = new Vector3(0f, 1.2f, 2f);
        public float swayDegrees = 6f;
        public float speed = 0.12f;

        Vector3 _offset;

        void Awake() => _offset = transform.position - lookAt;

        void LateUpdate()
        {
            float a = Mathf.Sin(Time.unscaledTime * speed * Mathf.PI * 2f) * swayDegrees;
            transform.position = lookAt + Quaternion.Euler(0f, a, 0f) * _offset;
            transform.LookAt(lookAt);
        }
    }
}
