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
        public Sprite bumpPose, setPose, blockPose, divePose;
        [Tooltip("Foreshortened lay-out poses for dives away from / toward the camera. " +
                 "The sideways 'divePose' (rolled flat) is used when these are unset.")]
        public Sprite diveUpPose, diveDownPose;

        [Tooltip("Seconds the swing pose is held after a contact.")]
        public float swingHold = 0.28f;
        [Tooltip("Ground speed (units/sec) above which the run cycle plays.")]
        public float runThreshold = 0.4f;
        [Tooltip("Run cycle speed — frames alternate at half this rate.")]
        public float runFrameRate = 9f;

        SpriteRenderer _sr;
        VolleyPlayer _player;
        BillboardSprite _billboard;
        Vector3 _lastPos;
        float _swingTimer;
        HitType _swingType;
        float _runPhase;
        int _facing = 1;
        float _baseLocalY;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _player = GetComponentInParent<VolleyPlayer>();
            _billboard = GetComponent<BillboardSprite>();
            _baseLocalY = transform.localPosition.y;
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

        /// <summary>Re-read the sprite's resting local height. Call after moving the sprite
        /// child (e.g. a runtime character swap to a taller/shorter figure), or the dive
        /// lay-down offset keeps easing toward the old character's height.</summary>
        public void CaptureBaseLocalY() => _baseLocalY = transform.localPosition.y;

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
                case HitType.Dive:  return DiveFrame();
                default:            return swing; // Spike, Serve
            }
        }

        Sprite DiveFrame()
        {
            if (DiveIsDepthwise(out bool away))
                return away ? diveUpPose : diveDownPose;
            return divePose != null ? divePose : (bumpPose != null ? bumpPose : swing);
        }

        /// <summary>
        /// True when the current dive travels mostly toward/away from the camera (and the
        /// matching foreshortened pose exists) — those dives show a dedicated up/down frame
        /// instead of the sideways pose rolled flat. <paramref name="away"/>: heading away.
        /// </summary>
        bool DiveIsDepthwise(out bool away)
        {
            away = false;
            if (_player == null) return false;

            Vector3 d = _player.DiveDir;
            Camera cam = Camera.main;
            Vector3 fwd = cam != null ? cam.transform.forward : Vector3.forward;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            fwd.y = 0f; fwd.Normalize();
            right.y = 0f; right.Normalize();

            float depth = Vector3.Dot(d, fwd);
            float side = Vector3.Dot(d, right);
            away = depth > 0f;
            return Mathf.Abs(depth) > Mathf.Abs(side)
                   && (away ? diveUpPose : diveDownPose) != null;
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

            // Face the way we're moving across the SCREEN — the movement delta projected onto
            // the camera's right axis — not raw world X. The broadcast camera looks across the
            // court, so world X is mostly screen depth; judging facing by it reverses the flip
            // (and the dive roll direction) on down-left / up-right diagonals.
            Camera cam = Camera.main;
            Vector3 camRight = cam != null ? cam.transform.right : Vector3.right;
            camRight.y = 0f; camRight.Normalize();
            float sideDelta = delta.x * camRight.x + delta.z * camRight.z;
            if (Mathf.Abs(sideDelta) > 0.0005f) _facing = sideDelta > 0f ? 1 : -1;
            _sr.flipX = _facing < 0;

            if (_swingTimer > 0f) _swingTimer -= dt;

            Sprite frame;
            if (_player.IsDiving) frame = DiveFrame();      // laid out — hold it through the whole slide + get-up
            else if (_swingTimer > 0f) frame = SwingFrame(); // a contact wins (covers airborne spikes)
            else if (!_player.IsGrounded) frame = jump;
            else if (speed > runThreshold)
            {
                _runPhase += dt * runFrameRate;
                frame = ((int)_runPhase & 1) == 0 ? run0 : run1;
            }
            else { _runPhase = 0f; frame = idle; }

            if (frame != null) _sr.sprite = frame;

            // Dive layout for a SIDEWAYS dive: roll the billboarded sprite toward horizontal as
            // the dive progresses (head pointing the way we're diving) and lower it so the body
            // lies on the sand instead of hovering at standing height; it eases back upright
            // during the get-up. A depth-wise dive keeps the sprite upright and unlowered — its
            // dedicated pose is already drawn laid out along the ground, feet at the baseline.
            float flat = _player.DiveFlat01;
            bool depthDive = flat > 0f && DiveIsDepthwise(out _);
            if (_billboard != null) _billboard.extraRoll = depthDive ? 0f : -_facing * 90f * flat;
            Vector3 lp = transform.localPosition;
            lp.y = Mathf.Lerp(_baseLocalY, _baseLocalY * 0.5f, depthDive ? 0f : flat);
            transform.localPosition = lp;
        }
    }
}
