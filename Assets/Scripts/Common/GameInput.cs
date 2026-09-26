using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Volleyball
{
    /// <summary>
    /// Unified input for the single human player. Aggregates keyboard/mouse and gamepad (read
    /// via the Input System) with "virtual" input pushed in by the on-screen touch controls, so
    /// gameplay code reads one source regardless of platform. The three hit types are
    /// separate inputs so the player explicitly chooses bump / set / spike each contact.
    /// Runs early (DefaultExecutionOrder) so the edge flags are fresh for readers.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameInput : MonoBehaviour
    {
        public static GameInput Instance { get; private set; }

        Vector2 _virtualMove;
        bool _vJump, _vBump, _vSet, _vSpike, _vPower, _vDive;
        bool _jumpPrev, _bumpPrev, _setPrev, _spikePrev, _divePrev, _powerPrev;
        ChatCall _chatRequest;  // pushed in by an on-screen chat button, consumed next Update
        ChatCall _chatKeyPrev;  // held chat key last frame, for edge detection

        public Vector2 Move { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool BumpPressed { get; private set; }
        public bool SetPressed { get; private set; }
        public bool SpikePressed { get; private set; }
        public bool DivePressed { get; private set; }
        public bool PowerPressed { get; private set; }

        /// <summary>A team callout requested this frame — hotkey or on-screen button
        /// (<see cref="ChatCall.None"/> when nothing was said).</summary>
        public ChatCall ChatPressed { get; private set; }

        /// <summary>The last input came from a gamepad (vs keyboard/mouse/touch) — hints and
        /// menus follow it. Static so UI can ask without holding the instance.</summary>
        public static bool UsingGamepad { get; internal set; }

        /// <summary>Radial deadzone for the left stick, rescaled so movement starts smoothly.</summary>
        const float StickDeadzone = 0.2f;
        ChatCall _padChatPrev;

        /// <summary>Any hit input this frame — used to trigger the serve / restart.</summary>
        public bool AnyHitPressed => BumpPressed || SetPressed || SpikePressed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("GameInput");
            go.AddComponent<GameInput>();

            // controller diagnostics in Player.log: what the Input System actually sees
            foreach (var d in InputSystem.devices)
                Debug.Log($"[Volleyball] input device: {d.layout} '{d.displayName}' ({d.GetType().Name})");
            InputSystem.onDeviceChange += (d, change) =>
                Debug.Log($"[Volleyball] input device {change}: {d.layout} '{d.displayName}' ({d.GetType().Name})");
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // Called by the on-screen touch controls.
        public void SetVirtualMove(Vector2 v) => _virtualMove = v;
        public void SetVirtualJump(bool held) => _vJump = held;
        public void SetVirtualBump(bool held) => _vBump = held;
        public void SetVirtualSet(bool held) => _vSet = held;
        public void SetVirtualSpike(bool held) => _vSpike = held;
        public void SetVirtualPower(bool held) => _vPower = held;
        public void SetVirtualDive(bool held) => _vDive = held;

        /// <summary>Called by an on-screen chat button (see <see cref="ChatBar"/>). Chat is a
        /// discrete press rather than a held control, so it queues instead of latching a bool —
        /// and the queue survives UI clicks that land after this frame's Update.</summary>
        public void RequestChat(ChatCall call)
        {
            if (call != ChatCall.None) _chatRequest = call;
        }

        void Update()
        {
            Vector2 kb = Vector2.zero;
            bool jump = _vJump, bump = _vBump, set = _vSet, spike = _vSpike, dive = _vDive,
                 power = _vPower;

            var k = Keyboard.current;
            if (k != null && k.anyKey.isPressed) UsingGamepad = false;
            if (k != null)
            {
                if (k.wKey.isPressed || k.upArrowKey.isPressed) kb.y += 1f;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) kb.y -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) kb.x += 1f;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) kb.x -= 1f;
                if (k.spaceKey.isPressed) jump = true;
                if (k.jKey.isPressed) bump = true;
                if (k.kKey.isPressed) set = true;
                if (k.lKey.isPressed) spike = true;
                if (k.semicolonKey.isPressed || k.leftShiftKey.isPressed) dive = true;
                if (k.eKey.isPressed) power = true;
            }

            // A click that lands on the HUD (a chat button, MENU) is a UI click, not a swing —
            // otherwise tapping "I GOT IT" also bumps, which can wreck a real contact.
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            var m = Mouse.current;
            if (m != null && (m.delta.ReadValue().sqrMagnitude > 16f || m.leftButton.isPressed))
                UsingGamepad = false; // back on the mouse: hints and menus follow
            if (_virtualMove != Vector2.zero) UsingGamepad = false; // touch controls
            if (m != null && !overUI)
            {
                if (m.leftButton.isPressed) bump = true;
                if (m.rightButton.isPressed) spike = true;
                if (m.middleButton.isPressed) power = true;
            }

            // Gamepad: left stick (or D-pad) moves; A jump, X bump, Y set, B spike, RT/RB dive,
            // LT power. Holding LB turns the D-pad into callouts (see ReadChat).
            var gp = Gamepad.current;
            if (gp != null)
            {
                Vector2 stick = gp.leftStick.ReadValue();
                float mag = stick.magnitude;
                stick = mag < StickDeadzone ? Vector2.zero
                      : stick / mag * Mathf.InverseLerp(StickDeadzone, 1f, Mathf.Min(mag, 1f));
                Vector2 dpad = gp.leftShoulder.isPressed ? Vector2.zero : gp.dpad.ReadValue();
                kb += stick + dpad;
                if (gp.buttonSouth.isPressed) jump = true;
                if (gp.buttonWest.isPressed) bump = true;
                if (gp.buttonNorth.isPressed) set = true;
                if (gp.buttonEast.isPressed) spike = true;
                if (gp.rightTrigger.ReadValue() > 0.5f || gp.rightShoulder.isPressed) dive = true;
                if (gp.leftTrigger.ReadValue() > 0.5f) power = true;
                if (stick != Vector2.zero || dpad != Vector2.zero || jump || bump || set || spike || dive || power
                    || gp.leftShoulder.isPressed || gp.startButton.isPressed)
                    UsingGamepad = true;
            }

            Vector2 mv = kb + _virtualMove;
            if (mv.sqrMagnitude > 1f) mv = mv.normalized;
            Move = mv;

            JumpHeld = jump;
            JumpPressed = jump && !_jumpPrev;
            BumpPressed = bump && !_bumpPrev;
            SetPressed = set && !_setPrev;
            SpikePressed = spike && !_spikePrev;
            DivePressed = dive && !_divePrev;
            PowerPressed = power && !_powerPrev;

            _jumpPrev = jump;
            _bumpPrev = bump;
            _setPrev = set;
            _spikePrev = spike;
            _divePrev = dive;
            _powerPrev = power;

            ReadChat(k, gp);
        }

        /// <summary>Resolve this frame's callout: a freshly-pressed chat hotkey wins, otherwise
        /// whatever an on-screen button queued. Held keys don't repeat.</summary>
        void ReadChat(Keyboard k, Gamepad gp)
        {
            ChatCall held = ChatCall.None;
            if (k != null)
                foreach (var def in ChatCalls.All)
                    if (def.hotkey != Key.None && k[def.hotkey].isPressed) { held = def.call; break; }

            // LB + D-pad: up "I got it", down "You got it", left "Nice!", right "Let's go!"
            ChatCall pad = ChatCall.None;
            if (gp != null && gp.leftShoulder.isPressed)
            {
                if (gp.dpad.up.isPressed) pad = ChatCall.IGotIt;
                else if (gp.dpad.down.isPressed) pad = ChatCall.YouGotIt;
                else if (gp.dpad.left.isPressed) pad = ChatCall.Nice;
                else if (gp.dpad.right.isPressed) pad = ChatCall.LetsGo;
            }

            if (held != ChatCall.None && held != _chatKeyPrev) ChatPressed = held;
            else if (pad != ChatCall.None && pad != _padChatPrev) ChatPressed = pad;
            else ChatPressed = _chatRequest;
            _chatKeyPrev = held;
            _padChatPrev = pad;
            _chatRequest = ChatCall.None;
        }

        /// <summary>Gamepad label for a callout ("LB↑" = hold LB, press D-pad up), or "" if unbound.</summary>
        public static string PadChatHint(ChatCall call)
        {
            switch (call)
            {
                case ChatCall.IGotIt: return "LB↑";
                case ChatCall.YouGotIt: return "LB↓";
                case ChatCall.Nice: return "LB←";
                case ChatCall.LetsGo: return "LB→";
                default: return "";
            }
        }
    }
}
