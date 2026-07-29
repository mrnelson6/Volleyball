using UnityEngine;
using UnityEngine.InputSystem;

namespace Volleyball
{
    /// <summary>
    /// The one <see cref="IInputSource"/> that reads real devices, by delegating to the
    /// <see cref="GameInput"/> singleton (which stays the keyboard/mouse/touch aggregator).
    /// This is the only class allowed to touch <c>GameInput.Instance</c> — everything else
    /// receives commands, so a second (remote) human never collides with the local devices.
    /// </summary>
    public class LocalInputSource : IInputSource
    {
        bool _jump, _dive, _power, _bump, _set, _spike;

        public Vector2 Move
            => GameInput.Instance != null ? GameInput.Instance.Move : Vector2.zero;

        public void PollFrame()
        {
            var gi = GameInput.Instance;
            if (gi == null) return;
            _jump |= gi.JumpPressed;
            _dive |= gi.DivePressed;
            _power |= gi.PowerPressed;
            _bump |= gi.BumpPressed;
            _set |= gi.SetPressed;
            _spike |= gi.SpikePressed;
        }

        public void ConsumeTick(out bool jump, out bool dive, out bool power,
                                out bool hitPressed, out HitType hitType)
        {
            jump = _jump;
            dive = _dive;
            power = _power;

            // same priority as the old TryGetDesiredHit: an ambiguous multi-press favours
            // the bigger swing (Spike > Set > Bump)
            hitPressed = _bump || _set || _spike;
            hitType = _spike ? HitType.Spike : _set ? HitType.Set : HitType.Bump;

            _jump = _dive = _power = _bump = _set = _spike = false;
        }

        public string PowerHintLabel => Touchscreen.current != null ? "" : "E";
    }
}
