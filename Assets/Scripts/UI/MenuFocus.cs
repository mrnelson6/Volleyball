using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Volleyball
{
    /// <summary>
    /// Controller (and keyboard) menu flow for one screen: when the screen opens — or the
    /// moment a gamepad starts navigating with nothing focused — highlight a sensible first
    /// control; B / Esc presses the screen's Back button. Mouse users never see a stray
    /// focus: nothing is auto-selected unless a gamepad is in use.
    /// </summary>
    public class MenuFocus : MonoBehaviour
    {
        public Selectable first;
        public Button back;

        void OnEnable() => FocusFirst(force: false);

        void FocusFirst(bool force)
        {
            var es = EventSystem.current;
            if (es == null || first == null || !first.gameObject.activeInHierarchy) return;
            if (!force && !GameInput.UsingGamepad) return;
            es.SetSelectedGameObject(first.gameObject);
        }

        void Update()
        {
            var es = EventSystem.current;
            if (es == null) return;
            var gp = Gamepad.current;

            // nothing focused (e.g. opened with the mouse) and the pad starts navigating: pick up
            bool padNav = gp != null && (gp.leftStick.ReadValue().sqrMagnitude > 0.25f
                                         || gp.dpad.ReadValue() != Vector2.zero
                                         || gp.buttonSouth.wasPressedThisFrame);
            GameObject sel = es.currentSelectedGameObject;
            if (padNav && (sel == null || !sel.activeInHierarchy || !sel.transform.IsChildOf(transform)))
                FocusFirst(force: true);

            bool backPressed = (gp != null && gp.buttonEast.wasPressedThisFrame)
                               || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);
            if (backPressed && back != null && back.gameObject.activeInHierarchy && back.interactable)
                back.onClick.Invoke();
        }
    }
}
