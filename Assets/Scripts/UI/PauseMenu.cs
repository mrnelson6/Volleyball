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
    ///
    /// It doubles as the controls reference: the card above the title is built here at runtime
    /// from <see cref="ControlsHelp"/> — the keybindings live in code, so the page can't fall out
    /// of date, and no arena needs rebuilding when a binding changes.
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
            BuildControlsCard();
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

        // ----------------------------------------------------------------- controls card

        /// <summary>
        /// Build the controls reference into the free space above the "Paused" title (which sits
        /// at +160, with the buttons below it), so pausing IS the controls page — no second click.
        /// Idempotent: only ever one card per panel.
        /// </summary>
        void BuildControlsCard()
        {
            if (panel == null || !ChatArt.CanRender) return;      // headless server: no HUD at all
            if (panel.transform.Find(CardName) != null) return;   // already built

            var card = new GameObject(CardName, typeof(RectTransform), typeof(Image));
            card.transform.SetParent(panel.transform, false);
            var rt = card.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            // 320 tall, centred at +368: the band between the screen top and the "Paused" title
            rt.sizeDelta = new Vector2(1000f, 320f);
            rt.anchoredPosition = new Vector2(0f, 368f);
            var bg = card.GetComponent<Image>();
            bg.sprite = ChatArt.Panel;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.05f, 0.07f, 0.10f, 0.75f);

            Label(card.transform, "Heading", "Controls", 34, TextAnchor.UpperCenter,
                  new Vector2(960f, 44f), new Vector2(0f, 138f)).color = new Color(1f, 0.85f, 0.35f);

            // two columns under the heading: playing on the left, team/serve on the right
            Label(card.transform, "Playing", string.Join("\n", ControlsHelp.PlayingLines()),
                  24, TextAnchor.UpperLeft, new Vector2(470f, 196f), new Vector2(-247f, 10f));
            Label(card.transform, "Team", string.Join("\n", ControlsHelp.TeamLines()),
                  24, TextAnchor.UpperLeft, new Vector2(470f, 196f), new Vector2(247f, 10f));

            Label(card.transform, "Footer", ControlsHelp.Footer, 20, TextAnchor.LowerCenter,
                  new Vector2(940f, 26f), new Vector2(0f, -145f)).color = new Color(1f, 1f, 1f, 0.7f);
        }

        const string CardName = "Controls Card";

        static Text Label(Transform parent, string name, string content, int fontSize,
                          TextAnchor align, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            var t = go.GetComponent<Text>();
            t.font = ChatArt.UIFont();
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.lineSpacing = 1.15f;
            t.text = content;
            return t;
        }
    }
}
