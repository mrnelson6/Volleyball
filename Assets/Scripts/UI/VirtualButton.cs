using UnityEngine;
using UnityEngine.EventSystems;

namespace Volleyball
{
    public enum VirtualButtonKind { Jump, Hit }

    /// <summary>On-screen hold button that drives a <see cref="GameInput"/> virtual button.</summary>
    public class VirtualButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public VirtualButtonKind kind;

        public void OnPointerDown(PointerEventData e) => Set(true);
        public void OnPointerUp(PointerEventData e) => Set(false);

        void Set(bool held)
        {
            if (GameInput.Instance == null) return;
            if (kind == VirtualButtonKind.Jump) GameInput.Instance.SetVirtualJump(held);
            else GameInput.Instance.SetVirtualHit(held);
        }
    }
}
