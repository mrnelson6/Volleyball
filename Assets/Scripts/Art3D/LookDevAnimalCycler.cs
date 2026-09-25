using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Volleyball
{
    /// <summary>
    /// Look-dev only: loops through an animal's generated clips (Idle, Run, Spike, ...) in play
    /// mode, and holds a chosen pose in edit mode so the generated scene/contact sheet shows
    /// animals mid-action. Phase 1 replaces this with a real Animator driven by VolleyPlayer.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Animator))]
    public class LookDevAnimalCycler : MonoBehaviour
    {
        public AnimationClip[] clips = new AnimationClip[0];
        [Tooltip("Clip + time posed in edit mode (and used by the contact sheet).")]
        public int poseClip;
        public float poseTime;
        public float secondsPerClip = 2.5f;

        PlayableGraph _graph;
        AnimationClipPlayable _playable;
        AnimationPlayableOutput _output;
        int _current = -1;
        float _nextSwitch;

        void OnEnable()
        {
            if (!Application.isPlaying) Pose();
        }

        public void Pose()
        {
            if (clips == null || clips.Length == 0) return;
            var c = clips[Mathf.Clamp(poseClip, 0, clips.Length - 1)];
            if (c != null) c.SampleAnimation(gameObject, poseTime);
        }

        void Start()
        {
            if (!Application.isPlaying || clips.Length == 0) return;
            _graph = PlayableGraph.Create(name + "_LookDev");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _output = AnimationPlayableOutput.Create(_graph, "Anim", GetComponent<Animator>());
            _current = poseClip - 1;
            Next();
            _graph.Play();
        }

        void Next()
        {
            _current = (_current + 1) % clips.Length;
            if (_playable.IsValid()) _playable.Destroy();
            _playable = AnimationClipPlayable.Create(_graph, clips[_current]);
            _output.SetSourcePlayable(_playable);
            // one-shot clips replay a few times so they're readable
            float len = Mathf.Max(0.1f, clips[_current].length);
            _nextSwitch = Time.time + Mathf.Max(secondsPerClip, len);
        }

        void Update()
        {
            if (!Application.isPlaying || !_graph.IsValid()) return;
            var clip = clips[_current];
            if (!clip.isLooping && _playable.GetTime() > clip.length + 0.4f) _playable.SetTime(0);
            if (Time.time >= _nextSwitch) Next();
        }

        void OnDisable()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }
    }
}
