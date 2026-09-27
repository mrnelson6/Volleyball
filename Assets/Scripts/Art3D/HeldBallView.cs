using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Puts the beach-ball mesh in the server's paw while they wait to serve. View-only: sits on
    /// the ball's visual child and only ever moves that child — the ball's root transform is its
    /// simulation state (and, online, what the server's snapshots carry), so the held hold point
    /// the match computes stays authoritative and identical everywhere. When the ball is tossed
    /// or served, the visual eases back onto the real ball over a few frames.
    /// Runs after the character views so the paw's pose for this frame is already evaluated.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class HeldBallView : MonoBehaviour
    {
        public float radius = 0.3f;
        [Tooltip("How fast the visual settles back onto the real ball after a toss (1/s).")]
        public float release = 22f;

        Vector3 _baseLocal;
        Vector3 _offset;
        MatchManager _match;

        void Awake() => _baseLocal = transform.localPosition;

        void LateUpdate()
        {
            Transform root = transform.parent;
            if (root == null) return;
            Vector3 home = root.TransformPoint(_baseLocal);

            if (_match == null) _match = FindAnyObjectByType<MatchManager>();
            bool held = false;
            if (_match != null && _match.BallInServerHands)
            {
                var view = CharacterView.Of(_match.CurrentServer) as ModelCharacterView;
                if (view != null && view.TryGetHeldBallPoint(radius, out Vector3 p))
                {
                    _offset = p - home;
                    held = true;
                }
            }
            if (!held)
            {
                if (_offset.sqrMagnitude > 25f) _offset = Vector3.zero; // stale (rally reset teleport)
                _offset = Vector3.Lerp(_offset, Vector3.zero, 1f - Mathf.Exp(-release * Time.deltaTime));
            }
            transform.position = home + _offset;
        }
    }
}
