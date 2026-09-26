using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Volleyball
{
    /// <summary>
    /// The Quick Play character-select screen: a grid of roster portraits (3D headshots baked by
    /// PortraitBaker), a live 3D preview of whoever is highlighted (<see cref="CharacterShowcase"/>)
    /// with name, blurb and stat bars, and Play/Back. Play
    /// launches Quick Play as the selected character with the AI players randomised. The last
    /// pick is remembered in PlayerPrefs. References are wired by MainMenuSceneBuilder.
    /// </summary>
    public class CharacterSelectPanel : MonoBehaviour
    {
        [System.Serializable]
        public class Entry
        {
            public string characterId;
            public Button button;
            public Image frame;    // button background, tinted to show selection
            public Image portrait; // 3D headshot (or baked idle sprite), also reused by the preview pane
            public Color baseColor = new Color(1f, 1f, 1f, 0.10f); // region tint when not selected
        }

        [System.Serializable]
        public class StatBar
        {
            public RectTransform fill; // anchors set 0..fraction
            public Text valueLabel;
        }

        public Entry[] entries;

        [Header("Preview pane")]
        public CharacterShowcase showcase;
        public Image previewPortrait; // 2D fallback when no showcase is wired
        public Text previewName;
        public Text previewBlurb;
        public StatBar heightBar, speedBar, powerBar, controlBar, jumpBar;

        public ScrollRect scroll; // roster grid — kept scrolled to the focused tile
        public Button playButton;
        public Button backButton;
        public Button venueButton;  // cycles through SceneFlow.Arenas
        public Text venueLabel;

        // stats live in this range across the roster; bars are drawn against it
        const float StatMin = 0.7f, StatMax = 1.4f;
        const string PrefKey = "vb.character";
        const string VenuePrefKey = "vb.arena";

        int _venueIndex;

        static readonly Color FrameSelected = new Color(1f, 0.85f, 0.30f, 0.95f);

        string _selectedId;

        void Awake()
        {
            foreach (var e in entries)
            {
                string id = e.characterId; // capture per-iteration for the closure
                if (e.button != null) e.button.onClick.AddListener(() => Select(id));
            }
            if (playButton != null) playButton.onClick.AddListener(Play);
            if (backButton != null) backButton.onClick.AddListener(Close);
            if (venueButton != null) venueButton.onClick.AddListener(CycleVenue);
        }

        void OnEnable()
        {
            _venueIndex = Mathf.Clamp(PlayerPrefs.GetInt(VenuePrefKey, 0),
                                      0, SceneFlow.Arenas.Length - 1);
            UpdateVenueLabel();
            Select(PlayerPrefs.GetString(PrefKey, CharacterRoster.DefaultId));

            // a controller starts on the animal you last played
            if (GameInput.UsingGamepad && EventSystem.current != null)
                foreach (var e in entries)
                    if (e.characterId == _selectedId && e.button != null)
                        EventSystem.current.SetSelectedGameObject(e.button.gameObject);
        }

        GameObject _lastFocus;

        /// <summary>Controller browsing: moving focus onto a tile previews that animal (no
        /// press needed), and the grid scrolls to keep it in view.</summary>
        void Update()
        {
            var es = EventSystem.current;
            GameObject focus = es != null ? es.currentSelectedGameObject : null;
            if (focus == _lastFocus) return;
            _lastFocus = focus;
            if (focus == null) return;
            foreach (var e in entries)
                if (e.button != null && e.button.gameObject == focus)
                {
                    if (e.characterId != _selectedId) Select(e.characterId);
                    ScrollTo(e.button.transform as RectTransform);
                    break;
                }
        }

        void ScrollTo(RectTransform tile)
        {
            if (scroll == null || tile == null || scroll.content == null) return;
            RectTransform content = scroll.content, view = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
            float contentH = content.rect.height, viewH = view.rect.height;
            if (contentH <= viewH) return;
            float tileTop = -tile.anchoredPosition.y - tile.rect.height * (1f - tile.pivot.y); // distance from content top
            float tileBottom = tileTop + tile.rect.height;
            float scrolled = (1f - scroll.verticalNormalizedPosition) * (contentH - viewH);
            if (tileTop < scrolled) scrolled = tileTop - 10f;
            else if (tileBottom > scrolled + viewH) scrolled = tileBottom - viewH + 10f;
            scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(scrolled / (contentH - viewH));
        }

        void CycleVenue()
        {
            _venueIndex = (_venueIndex + 1) % SceneFlow.Arenas.Length;
            PlayerPrefs.SetInt(VenuePrefKey, _venueIndex);
            UpdateVenueLabel();
        }

        void UpdateVenueLabel()
        {
            if (venueLabel != null)
                venueLabel.text = $"Venue:  {SceneFlow.ArenaNames[_venueIndex]}  ▶";
        }

        public void Select(string id)
        {
            _selectedId = id;
            CharacterDef ch = CharacterRoster.Get(id);

            foreach (var e in entries)
                if (e.frame != null)
                    e.frame.color = e.characterId == ch.id ? FrameSelected : e.baseColor;

            if (previewName != null) previewName.text = ch.displayName;
            if (previewBlurb != null)
            {
                PowerUpDef pu = PowerUpRoster.Get(ch.powerUp);
                previewBlurb.text = $"{ch.blurb}\nPower-up: {pu.displayName} — {pu.blurb}";
                previewBlurb.resizeTextForBestFit = true; // the second line must still fit
            }
            if (showcase != null) showcase.Show(ch.id, PlayerColors.Human);
            if (previewPortrait != null)
            {
                // reuse the entry's baked portrait so no sprite loading happens here
                foreach (var e in entries)
                    if (e.characterId == ch.id && e.portrait != null)
                        previewPortrait.sprite = e.portrait.sprite;
                previewPortrait.preserveAspect = true;
            }

            SetBar(heightBar, ch.height);
            SetBar(speedBar, ch.speed);
            SetBar(powerBar, ch.power);
            SetBar(controlBar, ch.control);
            SetBar(jumpBar, ch.jump);
        }

        static void SetBar(StatBar bar, float stat)
        {
            if (bar == null) return;
            float frac = Mathf.Clamp01(Mathf.InverseLerp(StatMin, StatMax, stat));
            if (bar.fill != null)
            {
                bar.fill.anchorMin = new Vector2(0f, 0f);
                bar.fill.anchorMax = new Vector2(Mathf.Max(frac, 0.02f), 1f);
                bar.fill.offsetMin = Vector2.zero;
                bar.fill.offsetMax = Vector2.zero;
            }
            if (bar.valueLabel != null) bar.valueLabel.text = $"×{stat:0.00}";
        }

        void Play()
        {
            PlayerPrefs.SetString(PrefKey, _selectedId);
            SceneFlow.LoadQuickPlay(_selectedId, _venueIndex);
        }

        void Close() => gameObject.SetActive(false);
    }
}
