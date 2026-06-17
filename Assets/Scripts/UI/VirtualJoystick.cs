using UnityEngine;
using UnityEngine.EventSystems;

namespace Volleyball
{
    /// <summary>
    /// On-screen drag joystick. Feeds a normalised move vector into <see cref="GameInput"/>.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform background;
        public RectTransform handle;
        public float radius = 90f;

        Vector2 _value;

        public void OnPointerDown(PointerEventData e) => OnDrag(e);

        public void OnDrag(PointerEventData e)
        {
            if (background == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                background, e.position, e.pressEventCamera, out Vector2 local);

            Vector2 v = local / radius;
            if (v.sqrMagnitude > 1f) v = v.normalized;
            _value = v;
            if (handle != null) handle.anchoredPosition = v * radius;
            Push();
        }

        public void OnPointerUp(PointerEventData e)
        {
            _value = Vector2.zero;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
            Push();
        }

        void Push()
        {
            if (GameInput.Instance != null) GameInput.Instance.SetVirtualMove(_value);
        }
    }
}
