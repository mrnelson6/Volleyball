using UnityEngine;
using UnityEngine.InputSystem;

namespace Volleyball
{
    /// <summary>Shows the on-screen controls only when a touchscreen is present.</summary>
    public class TouchControls : MonoBehaviour
    {
        public GameObject panel;

        void Start()
        {
            bool touch = Touchscreen.current != null || Application.isMobilePlatform;
            if (panel != null) panel.SetActive(touch);
        }
    }
}
