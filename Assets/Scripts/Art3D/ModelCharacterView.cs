using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Volleyball
{
    /// <summary>
    /// 3D view of a player: a generated toon animal (Tools/blender/animal_gen.py → prefab in
    /// Resources/Characters3D, see CharacterPrefabBuilder) animated from the same signals the
    /// sprite view reads — idle/run from the rendered ground-position delta, jump while airborne,
    /// a hit clip after every contact, the dive clip while diving. The body turns to face where
    /// it's running, the dive direction, or the net.
    ///
    /// View-only, like <see cref="CharacterAnimator"/>: reads ViewGroundPosition and Time.deltaTime
    /// (never the 50Hz-stepped sim position — that strobes the run/idle switch), and the
    /// animation graph is evaluated manually in LateUpdate so poses match the rendered frame.
    /// Clips are crossfaded through a two-input mixer; no AnimatorController asset needed.
    /// </summary>
    public class ModelCharacterView : CharacterView
    {
        static readonly int GlowId = Shader.PropertyToID("_GlowColor");

        [Header("Wiring (set by CharacterPrefabBuilder)")]
        public Animator animator;
        public AnimationClip idle, run, jump, spike, bump, set, block, dive, cheer;

        [Header("Tuning")]
        [Tooltip("Ground speed (units/sec) above which the run cycle plays.")]
        public float runThreshold = 0.4f;
        [Tooltip("Ground speed at which the run clip plays at 1x.")]
        public float runReferenceSpeed = 5f;
        [Tooltip("Seconds a contact clip owns the body after a hit.")]
        public float swingHold = 0.45f;
        public float crossfade = 0.09f;
        [Tooltip("Degrees per second the body turns toward its target facing.")]
        public float turnSpeed = 900f;

        public string CharacterId { get; private set; }

        VolleyPlayer _player;
        PlayableGraph _graph;
        AnimationMixerPlayable _mixer;
        AnimationClipPlayable _cur, _prev;
        AnimationClip _curClip;
        float _fade = 1f, _fadeDuration = 0.1f;
        Vector3 _lastPos;
        float _swingTimer;
        HitType _swingType;
        bool _wasDiving;
        Renderer[] _renderers;
        MaterialPropertyBlock _mpb;
        Color _glow = Color.clear;

        /// <summary>Dress this model as <paramref name="ch"/> in <paramref name="jersey"/> and
        /// start reading <paramref name="player"/>.</summary>
        public void Init(VolleyPlayer player, CharacterDef ch, Color jersey)
        {
            CharacterId = ch.id;
            var look = GetComponent<AnimalLook>();
            if (look != null) look.Set(ch.id, jersey);
            Rebind(player);
        }

        public void SetJersey(Color jersey)
        {
            var look = GetComponent<AnimalLook>();
            if (look != null && look.jersey != jersey) look.Set(look.characterId, jersey);
        }

        public override void Rebind(VolleyPlayer p)
        {
            if (_player != null) _player.Swung -= OnSwing;
            _player = p;
            if (_player == null) return;
            _lastPos = _player.ViewGroundPosition;
            _player.Swung += OnSwing;
            transform.rotation = Quaternion.LookRotation(NetFacing());
        }

        void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator == null) return;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            _graph = PlayableGraph.Create(name + "_View");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _mixer = AnimationMixerPlayable.Create(_graph, 2);
            AnimationPlayableOutput.Create(_graph, "Body", animator).SetSourcePlayable(_mixer);
            Play(idle, restart: true, fade: 0f);
            _graph.Play();
        }

        void OnDestroy()
        {
            if (_player != null) _player.Swung -= OnSwing;
            if (_graph.IsValid()) _graph.Destroy();
        }

        void OnSwing(HitType type)
        {
            _swingTimer = swingHold;
            _swingType = type;
            Play(ClipFor(type), restart: true, fade: 0.05f);
        }

        AnimationClip ClipFor(HitType type)
        {
            switch (type)
            {
                case HitType.Bump: return bump;
                case HitType.Set: return set;
                case HitType.Block: return block;
                case HitType.Dive: return dive;
                default: return spike; // Spike, Serve
            }
        }

        /// <summary>Crossfade to <paramref name="clip"/>. Same clip keeps playing unless
        /// <paramref name="restart"/>.</summary>
        void Play(AnimationClip clip, bool restart, float fade)
        {
            if (clip == null || !_graph.IsValid()) return;
            if (clip == _curClip && !restart) return;

            if (_prev.IsValid())
            {
                _graph.Disconnect(_mixer, 1);
                _prev.Destroy();
            }
            if (_cur.IsValid())
            {
                _graph.Disconnect(_mixer, 0);
                _prev = _cur;
                _graph.Connect(_prev, 0, _mixer, 1);
            }
            _cur = AnimationClipPlayable.Create(_graph, clip);
            _graph.Connect(_cur, 0, _mixer, 0);
            _curClip = clip;
            _fade = fade > 0f && _prev.IsValid() ? 0f : 1f;
            _fadeDuration = Mathf.Max(0.0001f, fade);
            ApplyWeights();
        }

        void ApplyWeights()
        {
            float w = Mathf.Clamp01(_fade);
            _mixer.SetInputWeight(0, w);
            _mixer.SetInputWeight(1, _prev.IsValid() ? 1f - w : 0f);
        }

        Vector3 NetFacing()
        {
            // team A plays on -Z and faces +Z toward the net, and vice versa
            float z = _player != null ? -CourtGeometry.SideSign(_player.team) : 1f;
            return new Vector3(0f, 0f, z);
        }

        public override void SetGlow(Color color, float amount)
        {
            Color g = amount > 0f ? new Color(color.r, color.g, color.b, amount * 0.55f) : Color.clear;
            if (g == _glow || _renderers == null) return;
            _glow = g;
            _mpb ??= new MaterialPropertyBlock();
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(GlowId, g);
                r.SetPropertyBlock(_mpb);
            }
        }

        // LateUpdate: the player's transform has been interpolated for this frame in Update.
        void LateUpdate()
        {
            if (_player == null || !_graph.IsValid()) return;

            float dt = Time.deltaTime;
            Vector3 pos = _player.ViewGroundPosition;
            Vector3 delta = pos - _lastPos;
            _lastPos = pos;
            float speed = dt > 0f ? new Vector2(delta.x, delta.z).magnitude / dt : 0f;
            if (_swingTimer > 0f) _swingTimer -= dt;

            // ---- which clip owns the body
            bool diving = _player.IsDiving;
            if (diving)
            {
                if (!_wasDiving) Play(dive, restart: true, fade: 0.05f);
            }
            else if (_swingTimer > 0f)
            {
                // the contact clip was started by OnSwing and plays out
            }
            else if (!_player.IsGrounded) Play(jump, restart: false, fade: crossfade);
            else if (speed > runThreshold) Play(run, restart: false, fade: crossfade);
            else Play(idle, restart: false, fade: crossfade * 1.5f);
            _wasDiving = diving;

            if (_cur.IsValid())
                _cur.SetSpeed(_curClip == run ? Mathf.Clamp(speed / runReferenceSpeed, 0.7f, 1.6f) : 1f);

            // ---- facing
            Vector3 face;
            if (diving && _player.DiveDir.sqrMagnitude > 0.01f) face = _player.DiveDir;
            else if (_swingTimer > 0f) face = NetFacing();
            else if (speed > runThreshold) face = new Vector3(delta.x, 0f, delta.z);
            else face = NetFacing();
            face.y = 0f;
            if (face.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(face.normalized), turnSpeed * dt);

            // ---- advance the crossfade and evaluate the pose for this frame
            if (_fade < 1f)
            {
                _fade += dt / _fadeDuration;
                ApplyWeights();
            }
            _graph.Evaluate(dt);
        }
    }
}
