using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Volleyball
{
    /// <summary>Which way the match camera looks at the court.</summary>
    public enum CameraView { Baseline, Broadcast }

    /// <summary>
    /// The match camera. Two views:
    /// <list type="bullet">
    /// <item><b>Baseline</b> — behind YOUR team's baseline (whichever team the local human is
    /// on), looking down the court over the net, easing sideways with the play.</item>
    /// <item><b>Broadcast</b> — the arena's original fixed sideline camera.</item>
    /// </list>
    /// Pure view, attached at runtime to the scene's main camera (no scene rebuild). Movement
    /// and aim are already camera-relative (PlayerController), so the stick follows whichever
    /// view is on. Places the camera before the pre-match intro starts, so the intro flies back
    /// to it; stands still while the intro runs. C (keyboard) / View (pad) swaps views; the
    /// choice is remembered.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class MatchCameraRig : MonoBehaviour
    {
        const string PrefKey = "cam.view";

        /// <summary>The player's chosen view (remembered).</summary>
        public static CameraView View
        {
            get => (CameraView)PlayerPrefs.GetInt(PrefKey, (int)CameraView.Baseline);
            set { PlayerPrefs.SetInt(PrefKey, (int)value); PlayerPrefs.Save(); }
        }

        Camera _cam;
        MatchManager _match;
        Vector3 _broadcastPos;
        Quaternion _broadcastRot;
        float _broadcastFov;
        float _follow;          // smoothed sideways follow (metres)
        TeamSide _team = TeamSide.A;
        float _teamCheck;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            Attach();
            SceneManager.sceneLoaded += (_, __) => Attach();
        }

        static void Attach()
        {
            if (Application.isBatchMode && SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            Camera cam = Camera.main;
            if (cam == null || cam.GetComponent<MatchCameraRig>() != null) return;
            if (Object.FindAnyObjectByType<MatchManager>() == null) return; // menus keep their own camera
            cam.gameObject.AddComponent<MatchCameraRig>();
        }

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _match = FindAnyObjectByType<MatchManager>();
            _broadcastPos = transform.position;
            _broadcastRot = transform.rotation;
            _broadcastFov = _cam.fieldOfView;
            RefreshTeam();
            Snap(); // before the intro's first frame: it returns to wherever we are now
        }

        void RefreshTeam()
        {
            foreach (var p in FindObjectsByType<VolleyPlayer>(FindObjectsSortMode.None))
                if (p.IsHuman && p.IsLocallyControlled) { _team = p.team; return; }
        }

        void Snap()
        {
            Pose(out Vector3 pos, out Quaternion rot, out float fov);
            transform.SetPositionAndRotation(pos, rot);
            _cam.fieldOfView = fov;
        }

        /// <summary>Where the camera wants to be right now.</summary>
        void Pose(out Vector3 pos, out Quaternion rot, out float fov)
        {
            if (View == CameraView.Broadcast)
            {
                pos = _broadcastPos;
                rot = _broadcastRot;
                fov = _broadcastFov;
                return;
            }
            float s = CourtGeometry.SideSign(_team); // -1: we're on the near (−Z) side
            // low and close behind our baseline: the server's feet stay in shot at the bottom,
            // the far baseline sits well inside the top
            // ~16° down: the court fills the lower ~60% (your server's feet just clear the HUD
            // buttons), the arena's far end and sky get the top ~40% — the backdrop is part of
            // the picture, not a sliver at the top edge
            pos = new Vector3(_follow, 5.6f, s * (CourtGeometry.HalfDepth + 8.0f));
            Vector3 look = new Vector3(_follow * 0.55f, 0.8f, -s * 0.5f);
            rot = Quaternion.LookRotation(look - pos);
            fov = 52f;
        }

        void Update()
        {
            var k = Keyboard.current;
            var gp = Gamepad.current;
            bool swap = (k != null && k.cKey.wasPressedThisFrame) || (gp != null && gp.selectButton.wasPressedThisFrame);
            if (swap)
            {
                View = View == CameraView.Baseline ? CameraView.Broadcast : CameraView.Baseline;
                if (_match != null && !_match.InIntro) Snap();
            }
        }

        void LateUpdate()
        {
            if (_match != null && _match.InIntro) return; // the intro owns the camera

            _teamCheck -= Time.unscaledDeltaTime;
            if (_teamCheck <= 0f) { _teamCheck = 1f; RefreshTeam(); }

            // ease sideways toward the action: half the ball, a little of you
            float target = 0f;
            BallController ball = AbilityDirector.Ball;
            if (ball != null) target += ball.transform.position.x * 0.35f;
            foreach (var p in FindObjectsByType<VolleyPlayer>(FindObjectsSortMode.None))
                if (p.IsHuman && p.IsLocallyControlled) { target += p.transform.position.x * 0.2f; break; }
            target = Mathf.Clamp(target, -2.5f, 2.5f);
            _follow = Mathf.Lerp(_follow, target, 1f - Mathf.Exp(-2.5f * Time.deltaTime));

            Pose(out Vector3 pos, out Quaternion rot, out float fov);
            float k = 1f - Mathf.Exp(-6f * Time.deltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, pos, k), Quaternion.Slerp(transform.rotation, rot, k));
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, fov, k);
        }
    }
}
