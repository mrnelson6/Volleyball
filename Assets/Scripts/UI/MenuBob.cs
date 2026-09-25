using UnityEngine;

namespace Volleyball
{
    /// <summary>Gentle idle bob + breathe for a logo or badge.</summary>
    public class MenuBob : MonoBehaviour
    {
        public float amplitude = 8f;   // canvas units
        public float speed = 1.3f;
        public float breathe = 0.02f;  // scale wobble

        RectTransform _rt;
        Vector2 _base;

        void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _base = _rt.anchoredPosition;
        }

        void Update()
        {
            float t = Time.unscaledTime * speed;
            _rt.anchoredPosition = _base + new Vector2(0f, Mathf.Sin(t) * amplitude);
            float s = 1f + Mathf.Sin(t * 0.7f + 1f) * breathe;
            _rt.localScale = new Vector3(s, s, 1f);
        }
    }
}
