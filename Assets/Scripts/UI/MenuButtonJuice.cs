using UnityEngine;
using UnityEngine.EventSystems;

namespace Volleyball
{
    /// <summary>
    /// Toy-like button feel: grows a little on hover, squashes on press, springs back. Works
    /// for mouse and touch (pointer events). Unscaled time, so it works while paused.
    /// </summary>
    public class MenuButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                                   IPointerDownHandler, IPointerUpHandler
    {
        public float hoverScale = 1.06f;
        public float pressScale = 0.94f;

        bool _hover, _down;
        Vector3 _base = Vector3.one;

        void Awake() => _base = transform.localScale;
        void OnDisable() { _hover = _down = false; transform.localScale = _base; }

        public void OnPointerEnter(PointerEventData e) => _hover = true;
        public void OnPointerExit(PointerEventData e) { _hover = false; _down = false; }
        public void OnPointerDown(PointerEventData e) => _down = true;
        public void OnPointerUp(PointerEventData e) => _down = false;

        void Update()
        {
            float target = _down ? pressScale : _hover ? hoverScale : 1f;
            transform.localScale = Vector3.Lerp(transform.localScale, _base * target,
                1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        }
    }
}
