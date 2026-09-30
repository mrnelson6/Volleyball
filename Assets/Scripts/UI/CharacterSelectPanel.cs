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
    ///
    /// Controller flow: the stick browses (previewing whoever is focused, without changing the
    /// pick), A locks the focused animal in and jumps to Play, A again starts. B from Play goes
    /// back to your locked tile rather than leaving the screen; Start plays from anywhere.
    /// </summary>
    public class CharacterSelectPanel : MonoBehaviour, IMenuBackHandler
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
        static readonly Color FrameFocused = new Color(1f, 1f, 1f, 0.85f); // controller cursor

        string _selectedId;

        void Awake()
        {
            foreach (var e in entries)
            {
                string id = e.characterId; // capture per-iteration for the closure
                if (e.button != null) e.button.onClick.AddListener(() => Pick(id));
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
        string _previewId;

        /// <summary>A tile was pressed (click, tap or A): lock that animal in. With a controller,
        /// focus jumps to Play so the next A starts the match.</summary>
        void Pick(string id)
        {
            Select(id);
            if (GameInput.UsingGamepad && playButton != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(playButton.gameObject);
        }

        /// <summary>Controller browsing: moving focus onto a tile previews that animal (the
        /// pick only changes on A), and the grid scrolls to keep it in view. Focus off the grid
        /// shows the locked pick again.</summary>
        void Update()
        {
            foreach (var gp in UnityEngine.InputSystem.Gamepad.all)
                if (gp.startButton.wasPressedThisFrame) { Play(); return; }

            var es = EventSystem.current;
            GameObject focus = es != null ? es.currentSelectedGameObject : null;
            if (focus == _lastFocus) return;
            _lastFocus = focus;
            Entry tile = TileFor(focus);
            if (tile != null) ScrollTo(tile.button.transform as RectTransform);
            PaintFrames(tile);
            string show = tile != null ? tile.characterId : _selectedId;
            if (show != _previewId) Preview(show);
        }

        /// <summary>Gold = your pick; white = where the controller cursor is.</summary>
        void PaintFrames(Entry focused)
        {
            foreach (var e in entries)
                if (e.frame != null)
                    e.frame.color = e.characterId == _selectedId ? FrameSelected
                                  : e == focused ? FrameFocused : e.baseColor;
        }

        Entry TileFor(GameObject go)
        {
            if (go == null) return null;
            foreach (var e in entries)
                if (e.button != null && e.button.gameObject == go) return e;
            return null;
        }

        /// <summary>B while on Play / Venue: back to your locked animal instead of leaving.</summary>
        public bool HandleBack()
        {
            var es = EventSystem.current;
            GameObject focus = es != null ? es.currentSelectedGameObject : null;
            if (focus == null || TileFor(focus) != null || (backButton != null && focus == backButton.gameObject))
                return false;
            foreach (var e in entries)
                if (e.characterId == _selectedId && e.button != null)
                {
                    es.SetSelectedGameObject(e.button.gameObject);
                    return true;
                }
            return false;
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

        /// <summary>Lock in an animal: gold frame, preview, and what Play launches.</summary>
        public void Select(string id)
        {
            CharacterDef ch = CharacterRoster.Get(id);
            _selectedId = ch.id;
            PaintFrames(TileFor(EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null));
            Preview(ch.id);
        }

        /// <summary>Show an animal in the preview pane (3D model, name, blurb, stats).</summary>
        void Preview(string id)
        {
            CharacterDef ch = CharacterRoster.Get(id);
            _previewId = ch.id;

            if (previewName != null) previewName.text = ch.displayName;
            if (previewBlurb != null)
            {
                var (puName, puBlurb) = PowerUpState.Describe(ch);
                previewBlurb.text = $"{ch.blurb}\nAbility: {puName} — {puBlurb}";
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
