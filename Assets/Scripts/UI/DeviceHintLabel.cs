using UnityEngine;
using UnityEngine.UI;

namespace Volleyball
{
    /// <summary>A label with a keyboard and a gamepad version ("I GOT IT  (Z)" vs
    /// "I GOT IT  (LB+Up)"), swapped live whenever the player switches device.</summary>
    [RequireComponent(typeof(Text))]
    public class DeviceHintLabel : MonoBehaviour
    {
        public string keyboardText;
        public string gamepadText;

        Text _text;
        bool? _shownPad;

        void Awake() => _text = GetComponent<Text>();

        void Update()
        {
            bool pad = GameInput.UsingGamepad;
            if (_shownPad == pad) return;
            _shownPad = pad;
            _text.text = pad ? gamepadText : keyboardText;
        }
    }
}
