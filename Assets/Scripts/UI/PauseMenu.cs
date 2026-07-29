using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Volleyball
{
    /// <summary>
    /// In-match overlay that lets the player pause (freezing the match with
    /// <see cref="Time.timeScale"/>) and either resume or quit back to the main menu. Reachable
    /// any time via the <c>Escape</c> key or the on-screen Back button, including after the match
    /// is over, so it doubles as the post-match exit. Built into every playable scene by
    /// <c>CourtKit.EnsureUI</c>.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public GameObject panel;        // the dimmed overlay (hidden when not paused)
        public Button openButton;       // small on-screen "Back/Pause" affordance
        public Button resumeButton;
        public Button menuButton;

        bool _paused;

        void Start()
        {
            if (openButton != null) openButton.onClick.AddListener(Pause);
            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (menuButton != null) menuButton.onClick.AddListener(SceneFlow.LoadMenu);
            SetPaused(false);
        }

        void Update()
        {
            var k = Keyboard.current;
            if (k != null && k.escapeKey.wasPressedThisFrame)
                SetPaused(!_paused);
        }

        void Pause() => SetPaused(true);
        void Resume() => SetPaused(false);

        void SetPaused(bool paused)
        {
            _paused = paused;
            if (panel != null) panel.SetActive(paused);
            // The open affordance hides while the menu is up to avoid overlap.
            if (openButton != null) openButton.gameObject.SetActive(!paused);
            // Online, the match belongs to everyone: Esc is a local overlay while play
            // continues — freezing timeScale would stall this machine's sim (and, on the
            // host, the whole server).
            if (!NetworkSession.IsOnline)
                Time.timeScale = paused ? 0f : 1f;
        }

        void OnDestroy()
        {
            // Leaving the scene while paused must not freeze the next one.
            Time.timeScale = 1f;
        }
    }
}
