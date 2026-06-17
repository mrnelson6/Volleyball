using UnityEngine;

namespace Volleyball
{
    /// <summary>Fits a RectTransform to the device safe area (notches, rounded corners).</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        RectTransform _rt;
        Rect _last;

        void Awake()
        {
            _rt = GetComponent<RectTransform>();
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea != _last) Apply();
        }

        void Apply()
        {
            _last = Screen.safeArea;
            if (Screen.width <= 0 || Screen.height <= 0) return;

            Vector2 min = _last.position;
            Vector2 max = _last.position + _last.size;
            min.x /= Screen.width; min.y /= Screen.height;
            max.x /= Screen.width; max.y /= Screen.height;

            _rt.anchorMin = min;
            _rt.anchorMax = max;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }
    }
}
