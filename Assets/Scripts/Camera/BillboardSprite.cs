using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Rotates a sprite to face the camera every frame, giving the 2.5D look.
    /// Characters use Y-axis billboarding (stay upright); the ball uses full billboarding.
    /// </summary>
    public class BillboardSprite : MonoBehaviour
    {
        [Tooltip("If true the sprite only yaws to face the camera (stays vertical).")]
        public bool yAxisOnly = true;

        [Tooltip("Optional: roll the sprite to show this ball's spin (used for the ball).")]
        public BallController spinSource;

        Transform _cam;
        float _roll;

        void LateUpdate()
        {
            if (_cam == null)
            {
                if (Camera.main == null) return;
                _cam = Camera.main.transform;
            }

            if (yAxisOnly)
            {
                Vector3 fwd = _cam.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.0001f) return;
                transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }
            else
            {
                transform.rotation = Quaternion.LookRotation(_cam.forward, _cam.up);
            }

            // roll the sprite about the view axis to convey spin
            if (spinSource != null)
            {
                _roll += spinSource.Spin * Time.deltaTime;
                float wobble = spinSource.SpinWobble * Mathf.Sin(Time.time * 13f) * 35f;
                transform.rotation *= Quaternion.Euler(0f, 0f, _roll + wobble);
            }
        }
    }
}
