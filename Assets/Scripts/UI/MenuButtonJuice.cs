using UnityEngine;
using UnityEngine.EventSystems;

namespace Volleyball
{
    /// <summary>
    /// Toy-like button feel: grows a little on hover or controller focus (plus a focus ring),
    /// squashes on press, springs back. Works for mouse, touch and gamepad navigation.
    /// Unscaled time, so it works while paused.
    /// </summary>
    public class MenuButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                                   IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        public float hoverScale = 1.06f;
        public float pressScale = 0.94f;
        [Tooltip("Shown while the button has controller/keyboard focus.")]
        public GameObject focusRing;

        bool _hover, _down, _selected;
        Vector3 _base = Vector3.one;

        void Awake() => _base = transform.localScale;
        void OnDisable()
        {
            _hover = _down = _selected = false;
            transform.localScale = _base;
            if (focusRing != null) focusRing.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData e) => _hover = true;
        public void OnPointerExit(PointerEventData e) { _hover = false; _down = false; }
        public void OnPointerDown(PointerEventData e) => _down = true;
        public void OnPointerUp(PointerEventData e) => _down = false;

        public void OnSelect(BaseEventData e)
        {
            _selected = true;
            if (focusRing != null) focusRing.SetActive(true);
        }

        public void OnDeselect(BaseEventData e)
        {
            _selected = false;
            if (focusRing != null) focusRing.SetActive(false);
        }

        void Update()
        {
            float target = _down ? pressScale : (_hover || _selected) ? hoverScale : 1f;
            transform.localScale = Vector3.Lerp(transform.localScale, _base * target,
                1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        }
    }
}
