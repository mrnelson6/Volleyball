using UnityEngine;
using UnityEngine.UI;

namespace Volleyball
{
    /// <summary>
    /// The Quick Play character-select screen: a grid of roster portraits, a preview pane
    /// (portrait, name, blurb, stat bars) for whoever is highlighted, and Play/Back. Play
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
            public Image portrait; // baked idle sprite, also reused by the preview pane
        }

        [System.Serializable]
        public class StatBar
        {
            public RectTransform fill; // anchors set 0..fraction
            public Text valueLabel;
        }

        public Entry[] entries;

        [Header("Preview pane")]
        public Image previewPortrait;
        public Text previewName;
        public Text previewBlurb;
        public StatBar heightBar, speedBar, powerBar, controlBar, jumpBar;

        public Button playButton;
        public Button backButton;
        public Button venueButton;  // cycles through SceneFlow.Arenas
        public Text venueLabel;

        // stats live in this range across the roster; bars are drawn against it
        const float StatMin = 0.7f, StatMax = 1.4f;
        const string PrefKey = "vb.character";
        const string VenuePrefKey = "vb.arena";

        int _venueIndex;

        static readonly Color FrameNormal = new Color(1f, 1f, 1f, 0.10f);
        static readonly Color FrameSelected = new Color(0.30f, 0.65f, 1f, 0.55f);

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

        void Select(string id)
        {
            _selectedId = id;
            CharacterDef ch = CharacterRoster.Get(id);

            foreach (var e in entries)
                if (e.frame != null)
                    e.frame.color = e.characterId == ch.id ? FrameSelected : FrameNormal;

            if (previewName != null) previewName.text = ch.displayName;
            if (previewBlurb != null)
            {
                PowerUpDef pu = PowerUpRoster.Get(ch.powerUp);
                previewBlurb.text = $"{ch.blurb}\nPower-up: {pu.displayName} — {pu.blurb}";
                previewBlurb.resizeTextForBestFit = true; // the second line must still fit
            }
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
