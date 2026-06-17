using UnityEngine;
using UnityEngine.InputSystem;

namespace Volleyball
{
    /// <summary>
    /// Unified input for the single human player. Aggregates keyboard/mouse (read via the
    /// Input System) with "virtual" input pushed in by the on-screen touch controls, so
    /// gameplay code reads one source regardless of platform.
    /// Runs early (DefaultExecutionOrder) so edge flags (Pressed) are fresh for readers.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameInput : MonoBehaviour
    {
        public static GameInput Instance { get; private set; }

        Vector2 _virtualMove;
        bool _virtualJump;
        bool _virtualHit;
        bool _jumpPrev;
        bool _hitPrev;

        public Vector2 Move { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool HitHeld { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool HitPressed { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("GameInput");
            go.AddComponent<GameInput>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // Called by the on-screen touch controls.
        public void SetVirtualMove(Vector2 v) => _virtualMove = v;
        public void SetVirtualJump(bool held) => _virtualJump = held;
        public void SetVirtualHit(bool held) => _virtualHit = held;

        void Update()
        {
            Vector2 kb = Vector2.zero;
            bool jump = _virtualJump;
            bool hit = _virtualHit;

            var k = Keyboard.current;
            if (k != null)
            {
                if (k.wKey.isPressed || k.upArrowKey.isPressed) kb.y += 1f;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) kb.y -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) kb.x += 1f;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) kb.x -= 1f;
                if (k.spaceKey.isPressed) jump = true;
                if (k.jKey.isPressed || k.enterKey.isPressed) hit = true;
            }

            var m = Mouse.current;
            if (m != null && m.leftButton.isPressed) hit = true;

            Vector2 mv = kb + _virtualMove;
            if (mv.sqrMagnitude > 1f) mv = mv.normalized;
            Move = mv;

            JumpHeld = jump;
            HitHeld = hit;
            JumpPressed = jump && !_jumpPrev;
            HitPressed = hit && !_hitPrev;
            _jumpPrev = jump;
            _hitPrev = hit;
        }
    }
}
