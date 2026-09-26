using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// A pulsing ring on the sand showing where your shot will go — the learnability half of
    /// stick aiming (the stick at contact is the aim; see <see cref="VolleyPlayer.SteerAim"/>).
    /// Local human only, pure view. Shown while:
    /// <list type="bullet">
    /// <item>you're lining up a serve (where the serve lands with the stick as held),</item>
    /// <item>a hit press is buffered and waiting for the ball (that hit's target),</item>
    /// <item>you're in the air with the ball close (spike aim), so you can pick a spot before
    /// swinging.</item>
    /// </list>
    /// The real shot still carries the contact error — the ring is the intent, not a promise.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class AimMarker : MonoBehaviour
    {
        public PlayerController player;
        public float radius = 0.7f;
        public int segments = 40;

        LineRenderer _line;
        MatchManager _match;
        BallController _ball;
        float _alpha;

        static readonly Color ServeColor = new Color(1f, 0.85f, 0.25f);
        static readonly Color AttackColor = new Color(1f, 0.40f, 0.30f);
        static readonly Color PassColor = new Color(0.35f, 0.85f, 1f);

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.loop = true;
            _line.useWorldSpace = true;
            _line.positionCount = segments;
            _line.widthMultiplier = 0.09f;
            _line.numCapVertices = 2;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.material = new Material(Shader.Find("Sprites/Default")); // always in builds (sprites use it)
        }

        void LateUpdate()
        {
            if (player == null) { Destroy(gameObject); return; } // controller swapped (AI takeover)
            if (_match == null) _match = FindAnyObjectByType<MatchManager>();
            if (_ball == null) _ball = FindAnyObjectByType<BallController>();

            Vector3 target = default;
            Color color = PassColor;
            bool show = false;
            if (player != null && player.IsLocallyControlled && _match != null)
            {
                Vector2 steer = player.SteerPreview;
                if (_match.State == MatchState.Serving && _match.IsServePhaseFor(player))
                {
                    target = _match.PreviewServeTarget(steer);
                    color = ServeColor;
                    show = true;
                }
                else if (_match.State == MatchState.Rallying && player.HitBuffered)
                {
                    target = player.PreviewAim(player.BufferedHit, steer);
                    color = player.BufferedHit == HitType.Spike ? AttackColor : PassColor;
                    show = true;
                }
                else if (_match.State == MatchState.Rallying && !player.IsGrounded && _ball != null
                         && (_ball.transform.position - player.SimPosition).sqrMagnitude < 12f)
                {
                    target = player.PreviewAim(HitType.Spike, steer);
                    color = AttackColor;
                    show = true;
                }
            }

            _alpha = Mathf.MoveTowards(_alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            _line.enabled = _alpha > 0.01f;
            if (!_line.enabled) return;

            if (show) transform.position = new Vector3(target.x, 0.06f, target.z);
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.08f;
            Vector3 c = transform.position;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                _line.SetPosition(i, c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * pulse);
            }
            color.a = 0.9f * _alpha;
            _line.startColor = _line.endColor = color;
        }
    }
}
