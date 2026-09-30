using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// The net's live height. <see cref="CourtGeometry.NetHeight"/> is the regulation height the
    /// scenes are built at; abilities (Tall Order) raise it for a while. Everything that asks
    /// "how high is the tape" reads <see cref="CourtGeometry.NetTop"/>, and the net's collider and
    /// visuals follow here. Set identically on every machine by the owning ability; the ball's
    /// physics only matters on the authority (clients mirror the ball from snapshots).
    /// </summary>
    public static class NetDynamics
    {
        /// <summary>Metres above regulation height right now.</summary>
        public static float Extra { get; private set; }

        static Transform[] _nets;
        static float[] _baseScaleY;
        static float[] _basePosY;
        static Transform _extension;

        public static void SetExtra(float metres)
        {
            metres = Mathf.Max(0f, metres);
            if (Mathf.Approximately(metres, Extra)) return;
            // capture the regulation net BEFORE changing the height: its base size is the
            // current scale minus whatever extra is on it right now
            if (_nets == null || (_nets.Length > 0 && _nets[0] == null)) Capture();
            Extra = metres;
            Apply();
        }

        public static void Reset() => SetExtra(0f);

        static void Apply()
        {

            // the collider (the NetMarker cube): stretch it up from the ground
            for (int i = 0; i < _nets.Length; i++)
            {
                Transform t = _nets[i];
                if (t == null) continue;
                float h = _baseScaleY[i] + Extra;
                t.localScale = new Vector3(t.localScale.x, h, t.localScale.z);
                t.position = new Vector3(t.position.x, _basePosY[i] + Extra * 0.5f, t.position.z);
            }

            // the visual: a mesh screen stacked on top of the regular net
            if (!AbilityFx.CanRender) return;
            if (_extension == null && Extra > 0f)
            {
                GameObject screen = AbilityFx.Quad("Net Extension", new Color(1f, 0.85f, 0.35f, 0.55f));
                _extension = screen.transform;
                GameObject tape = AbilityFx.Box("Net Extension Tape", new Color(1f, 0.95f, 0.8f, 1f));
                tape.transform.SetParent(_extension, false);
                tape.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                tape.transform.localScale = new Vector3(1f, 0.08f, 0.6f);
            }
            if (_extension == null) return;
            _extension.gameObject.SetActive(Extra > 0.01f);
            float width = CourtGeometry.HalfWidth * 2f + 1f;
            _extension.position = new Vector3(0f, CourtGeometry.NetHeight + Extra * 0.5f, 0f);
            _extension.localScale = new Vector3(width, Mathf.Max(Extra, 0.01f), 1f);
        }

        static void Capture()
        {
            NetMarker[] markers = Object.FindObjectsByType<NetMarker>(FindObjectsSortMode.None);
            _nets = new Transform[markers.Length];
            _baseScaleY = new float[markers.Length];
            _basePosY = new float[markers.Length];
            for (int i = 0; i < markers.Length; i++)
            {
                Transform t = markers[i].transform;
                _nets[i] = t;
                _baseScaleY[i] = t.localScale.y - Extra;          // capture regulation size
                _basePosY[i] = t.position.y - Extra * 0.5f;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Extra = 0f;
            _nets = null;
            _extension = null;
        }

        /// <summary>A new scene: forget the old net objects (and any leftover height).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void HookSceneLoads()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, __) =>
            {
                Extra = 0f;
                _nets = null;
                _extension = null;
            };
        }
    }
}
