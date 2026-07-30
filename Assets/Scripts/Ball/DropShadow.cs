using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// A flat blob shadow that tracks a target on the ground plane and shrinks/fades the
    /// higher the target is. Used under the ball and under players when they jump.
    /// </summary>
    public class DropShadow : MonoBehaviour
    {
        public Transform target;
        public float baseSize = 0.75f;
        public float maxHeight = 6f;

        SpriteRenderer _sr;
        Color _baseColor = new Color(0f, 0f, 0f, 0.4f);
        VolleyPlayer _player; // set when the target is a player, for the surface they stand on

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _baseColor = _sr.color;

            // Found rather than wired by the builders, so no scene needs rebaking for it.
            if (target != null) _player = target.GetComponent<VolleyPlayer>();
        }

        void LateUpdate()
        {
            if (target == null) return;

            // A player standing on the bleachers casts their shadow on the tread they're on,
            // not on the sand two metres below — so height is measured from what holds them
            // up. The ball has no such notion and keeps the court floor.
            float ground = _player != null ? _player.GroundHeight : 0f;
            float h = Mathf.Max(0f, target.position.y - ground);
            float t = Mathf.Clamp01(h / maxHeight);

            transform.position = new Vector3(target.position.x, ground + 0.03f, target.position.z);

            float s = baseSize * Mathf.Lerp(1f, 0.35f, t);
            transform.localScale = new Vector3(s, s, s);

            if (_sr != null)
            {
                Color c = _baseColor;
                c.a = _baseColor.a * Mathf.Lerp(1f, 0.25f, t);
                _sr.color = c;
            }
        }
    }
}
