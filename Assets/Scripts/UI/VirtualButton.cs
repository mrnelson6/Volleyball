using UnityEngine;
using UnityEngine.EventSystems;

namespace Volleyball
{
    public enum VirtualButtonKind { Jump, Bump, Set, Spike, Power, Dive }

    /// <summary>On-screen hold button that drives a <see cref="GameInput"/> virtual button.</summary>
    public class VirtualButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public VirtualButtonKind kind;

        public void OnPointerDown(PointerEventData e) => Set(true);
        public void OnPointerUp(PointerEventData e) => Set(false);

        void Set(bool held)
        {
            var gi = GameInput.Instance;
            if (gi == null) return;
            switch (kind)
            {
                case VirtualButtonKind.Jump: gi.SetVirtualJump(held); break;
                case VirtualButtonKind.Bump: gi.SetVirtualBump(held); break;
                case VirtualButtonKind.Set: gi.SetVirtualSet(held); break;
                case VirtualButtonKind.Spike: gi.SetVirtualSpike(held); break;
                case VirtualButtonKind.Power: gi.SetVirtualPower(held); break;
                case VirtualButtonKind.Dive: gi.SetVirtualDive(held); break;
            }
        }
    }
}
