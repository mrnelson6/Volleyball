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

            // any connected pad counts (not just Gamepad.current — some setups expose two)
            bool padNav = false, padBack = false;
            foreach (var gp in Gamepad.all)
            {
                padNav |= gp.leftStick.ReadValue().sqrMagnitude > 0.25f || gp.dpad.ReadValue() != Vector2.zero
                          || gp.buttonSouth.wasPressedThisFrame;
                padBack |= gp.buttonEast.wasPressedThisFrame;
            }
            if (padNav || padBack) GameInput.UsingGamepad = true;

            // Lost focus (screen just opened/closed, or opened with the mouse): with a pad in use,
            // land on this screen's first control straight away. Only when focus is truly gone —
            // focus on ANOTHER live screen is left alone, or two screens would fight over it
            // mid-transition (the old screen stole focus back just as it hid, stranding it on an
            // invisible button).
            GameObject sel = es.currentSelectedGameObject;
            if ((GameInput.UsingGamepad || padNav) && (sel == null || !sel.activeInHierarchy))
                FocusFirst(force: true);

            bool backPressed = padBack
                               || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);
            if (backPressed && back != null && back.gameObject.activeInHierarchy && back.interactable)
                back.onClick.Invoke();
        }
    }
}
