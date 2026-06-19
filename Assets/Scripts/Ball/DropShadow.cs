using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// A flat blob shadow that tracks a target on the ground plane and shrinks/fades the
    /// higher the target is. Used under the ball and under players when they jump.
    /// </summary>
    public class DropShadow : MonoBehaviour
    {
        public Transform target;
        public float baseSize = 0.75f;
        public float maxHeight = 6f;

        SpriteRenderer _sr;
        Color _baseColor = new Color(0f, 0f, 0f, 0.4f);

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _baseColor = _sr.color;
        }

        void LateUpdate()
        {
            if (target == null) return;

            float h = Mathf.Max(0f, target.position.y);
            float t = Mathf.Clamp01(h / maxHeight);

            transform.position = new Vector3(target.position.x, 0.03f, target.position.z);

            float s = baseSize * Mathf.Lerp(1f, 0.35f, t);
            transform.localScale = new Vector3(s, s, s);

            if (_sr != null)
            {
                Color c = _baseColor;
                c.a = _baseColor.a * Mathf.Lerp(1f, 0.25f, t);
                _sr.color = c;
            }
        }
    }
}
