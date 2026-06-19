using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Drives a player's billboard sprite through a tiny hand-baked frame set
    /// (idle / run / jump / swing) based on the <see cref="VolleyPlayer"/> state. Movement is
    /// read from the parent's ground-position delta, a swing pose is held briefly after every
    /// contact (bump/set/spike/serve/block), and the sprite flips to face the way it's moving.
    /// Frames are assigned by the scene builder (see CharacterArt); if any are missing it
    /// gracefully falls back to whatever sprite is already on the renderer.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CharacterAnimator : MonoBehaviour
    {
        public Sprite idle, run0, run1, jump, swing;
        [Tooltip("Per-contact poses; each falls back to 'swing' if unset.")]
        public Sprite bumpPose, setPose, blockPose;

        [Tooltip("Seconds the swing pose is held after a contact.")]
        public float swingHold = 0.28f;
        [Tooltip("Ground speed (units/sec) above which the run cycle plays.")]
        public float runThreshold = 0.4f;
        [Tooltip("Run cycle speed — frames alternate at half this rate.")]
        public float runFrameRate = 9f;

        SpriteRenderer _sr;
        VolleyPlayer _player;
        Vector3 _lastPos;
        float _swingTimer;
        HitType _swingType;
        float _runPhase;
        int _facing = 1;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _player = GetComponentInParent<VolleyPlayer>();
            if (_player != null)
            {
                _lastPos = _player.GroundPosition;
                _player.Swung += OnSwing;
            }
        }

        void OnDestroy()
        {
            if (_player != null) _player.Swung -= OnSwing;
        }

        void OnSwing(HitType type) { _swingTimer = swingHold; _swingType = type; }

        // The contact pose to hold: bump (hands together), set (both hands up), block (arms
        // overhead), or the default swing (spike/serve). Missing poses fall back to swing.
        Sprite SwingFrame()
        {
            switch (_swingType)
            {
                case HitType.Bump:  return bumpPose != null ? bumpPose : swing;
                case HitType.Set:   return setPose != null ? setPose : swing;
                case HitType.Block: return blockPose != null ? blockPose : swing;
                default:            return swing; // Spike, Serve
            }
        }

        // LateUpdate so we read the position the player settled on this frame (it moves in Update).
        void LateUpdate()
        {
            if (_player == null || _sr == null) return;

            float dt = Time.deltaTime;
            Vector3 pos = _player.GroundPosition;
            Vector3 delta = pos - _lastPos;
            _lastPos = pos;
            float speed = dt > 0f ? new Vector2(delta.x, delta.z).magnitude / dt : 0f;

            // face the way we're moving horizontally; keep the last facing while still
            if (Mathf.Abs(delta.x) > 0.0005f) _facing = delta.x > 0f ? 1 : -1;
            _sr.flipX = _facing < 0;

            if (_swingTimer > 0f) _swingTimer -= dt;

            Sprite frame;
            if (_swingTimer > 0f) frame = SwingFrame();     // a contact wins (covers airborne spikes)
            else if (!_player.IsGrounded) frame = jump;
            else if (speed > runThreshold)
            {
                _runPhase += dt * runFrameRate;
                frame = ((int)_runPhase & 1) == 0 ? run0 : run1;
            }
            else { _runPhase = 0f; frame = idle; }

            if (frame != null) _sr.sprite = frame;
        }
    }
}
