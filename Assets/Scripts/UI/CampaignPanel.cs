using UnityEngine;
using UnityEngine.UI;

namespace Volleyball
{
    /// <summary>
    /// The world-tour map: a stylized world with one pin per region, a dotted travel path
    /// between them in tour order, and the protagonist fox standing on the current stop.
    /// When a region has been conquered since the map was last seen, the fox visibly travels
    /// along the path to the next stop (tracked via PlayerPrefs so the trip plays exactly
    /// once). Pins tint by state (locked / current / conquered), clicking any pin previews
    /// its region, clicking the current pin plays the next match. Built from
    /// <see cref="RegionRoster"/> by MainMenuSceneBuilder; this refreshes from the save.
    /// </summary>
    public class CampaignPanel : MonoBehaviour
    {
        [System.Serializable]
        public class MapPin
        {
            public string regionId;
            public Button button;
            public Image pin;    // the dot on the map, tinted by state
            public Text label;   // "1. Sunny Savanna"
        }

        /// <summary>The dotted trail from region i to region i+1.</summary>
        [System.Serializable]
        public class PathLeg { public Image[] dots; }

        public MapPin[] pins;       // one per region, in tour (ladder) order
        public PathLeg[] legs;      // pins.Length - 1 legs
        public RectTransform marker; // the travelling fox, parented to the map like the pins
        public Text statusLabel;    // tour-wide summary line
        public Text infoLabel;      // the previewed region's details
        public Button playButton;
        public Text playButtonLabel;
        public Button newGameButton;
        public Text newGameButtonLabel;
        public Button backButton;

        static readonly Color PinLocked = new Color(0.70f, 0.70f, 0.70f, 0.45f);
        static readonly Color PinDone = new Color(0.35f, 0.85f, 0.45f, 1f);
        static readonly Color PinCurrent = new Color(1f, 0.80f, 0.20f, 1f);
        static readonly Color DotTravelled = new Color(0.95f, 0.85f, 0.55f, 0.9f);
        static readonly Color DotFuture = new Color(1f, 1f, 1f, 0.18f);
        static readonly Color TextDim = new Color(1f, 1f, 1f, 0.45f);

        /// <summary>Which region the fox was last SEEN standing on — when the save is ahead
        /// of this, the travel animation plays and then catches it up.</summary>
        const string MarkerPrefKey = "vb.tour.marker";

        const float TravelSpeed = 420f;  // map-canvas units per second
        const float HopLength = 60f;     // one little hop roughly every this many units

        bool _confirmingNewGame;
        int _currentRegion;
        bool _tourComplete;

        // travel animation state: waypoints are pin anchored-positions, walked in order
        Vector2[] _travelPath;
        int _travelLeg;      // index of the leg currently being walked
        float _travelDist;   // distance covered along that leg
        bool _travelling;

        void Awake()
        {
            if (playButton != null) playButton.onClick.AddListener(Play);
            if (newGameButton != null) newGameButton.onClick.AddListener(NewGame);
            if (backButton != null) backButton.onClick.AddListener(Close);

            if (pins != null)
                for (int i = 0; i < pins.Length; i++)
                {
                    int index = i; // capture per-pin
                    if (pins[i].button != null)
                        pins[i].button.onClick.AddListener(() => OnPinClicked(index));
                }
        }

        void OnEnable()
        {
            _confirmingNewGame = false;
            Refresh();
        }

        void Refresh()
        {
            CampaignSave save = SaveSystem.Load();
            bool hasSave = save != null;
            _currentRegion = hasSave ? Mathf.Clamp(save.regionIndex, 0, RegionRoster.All.Length - 1) : 0;
            _tourComplete = hasSave && save.tourComplete;

            if (statusLabel != null)
            {
                if (!hasSave)
                    statusLabel.text = "Your world tour awaits — two travellers, nine wild courts.";
                else if (_tourComplete)
                    statusLabel.text = $"WORLD TOUR CHAMPIONS!   W{save.matchesWon} / L{save.matchesLost}";
                else
                    statusLabel.text = $"On tour — W{save.matchesWon} / L{save.matchesLost}";
            }

            if (playButtonLabel != null)
                playButtonLabel.text = !hasSave ? "Start the Tour"
                    : _tourComplete ? "Replay the Grand Final"
                    : "Play Next Match";

            if (newGameButtonLabel != null && !_confirmingNewGame)
                newGameButtonLabel.text = "New Game";

            RefreshPins();
            ShowRegionInfo(_currentRegion, save);
            SetUpMarker();
        }

        void RefreshPins()
        {
            if (pins != null)
                for (int i = 0; i < pins.Length; i++)
                {
                    bool done = IsDone(i);
                    bool current = IsCurrent(i);
                    if (pins[i].pin != null)
                        pins[i].pin.color = done ? PinDone : current ? PinCurrent : PinLocked;
                    if (pins[i].label != null)
                        pins[i].label.color = done || current ? Color.white : TextDim;
                }

            if (legs != null)
                for (int i = 0; i < legs.Length; i++)
                {
                    // leg i is behind us once we've arrived at region i+1
                    Color c = (_tourComplete || _currentRegion > i) ? DotTravelled : DotFuture;
                    foreach (var dot in legs[i].dots)
                        if (dot != null) dot.color = c;
                }
        }

        bool IsDone(int i) => _tourComplete || (SaveSystem.Exists() && i < _currentRegion);
        bool IsCurrent(int i) => !_tourComplete && i == _currentRegion;

        void OnPinClicked(int i)
        {
            if (IsCurrent(i) || (_tourComplete && i == _currentRegion))
            {
                Play(); // the pulsing pin doubles as a play button — travel there and play
                return;
            }
            ShowRegionInfo(i, SaveSystem.Load());
        }

        /// <summary>Fill the info line with one region's story: the next match when it's the
        /// current stop, a victory note when conquered, a nudge when still locked.</summary>
        void ShowRegionInfo(int i, CampaignSave save)
        {
            if (infoLabel == null || i < 0 || i >= RegionRoster.All.Length) return;
            RegionDef region = RegionRoster.All[i];

            string line;
            if (IsCurrent(i) || (_tourComplete && i == _currentRegion))
            {
                int mi = save != null ? Mathf.Clamp(save.matchIndex, 0, region.matches.Length - 1) : 0;
                MatchDef next = region.matches[mi];
                line = $"{region.displayName} — Match {mi + 1}/{region.matches.Length} vs {next.teamName}";
                if (save != null && save.attemptsThisMatch > 0)
                    line += $"  (attempt {save.attemptsThisMatch + 1})";
                if (!string.IsNullOrEmpty(region.env.bannerNote))
                    line += "\n" + region.env.bannerNote;
            }
            else if (IsDone(i))
            {
                line = $"{region.displayName} — CONQUERED\n{region.blurb}";
            }
            else
            {
                line = $"{region.displayName} — locked. Conquer the stops before it to travel here.\n{region.blurb}";
            }
            infoLabel.text = line;
        }

        // ---- the travelling fox ------------------------------------------------

        void SetUpMarker()
        {
            if (marker == null || pins == null || pins.Length == 0) return;

            int seen = PlayerPrefs.GetInt(MarkerPrefKey, -1);
            if (seen < 0 || seen > _currentRegion)
            {
                // first ever look at the map, or a fresh tour after New Game: no trip to show
                SnapMarkerTo(_currentRegion);
                return;
            }

            if (seen == _currentRegion) { SnapMarkerTo(_currentRegion); return; }

            // a region fell since we last looked — walk the fox along every leg in between
            _travelPath = new Vector2[_currentRegion - seen + 1];
            for (int i = 0; i < _travelPath.Length; i++)
                _travelPath[i] = PinPos(seen + i);
            _travelLeg = 0;
            _travelDist = 0f;
            _travelling = true;
            marker.anchoredPosition = _travelPath[0] + MarkerOffset;
        }

        void SnapMarkerTo(int region)
        {
            _travelling = false;
            marker.anchoredPosition = PinPos(region) + MarkerOffset;
            PlayerPrefs.SetInt(MarkerPrefKey, region);
        }

        Vector2 PinPos(int i)
        {
            i = Mathf.Clamp(i, 0, pins.Length - 1);
            return pins[i].pin != null ? pins[i].pin.rectTransform.anchoredPosition : Vector2.zero;
        }

        static readonly Vector2 MarkerOffset = new Vector2(0f, 34f); // fox stands atop the pin

        void Update()
        {
            // the current stop pulses so "where do I go" needs no reading
            if (pins != null && !_tourComplete
                && _currentRegion < pins.Length && pins[_currentRegion].pin != null)
                pins[_currentRegion].pin.color = Color.Lerp(
                    PinCurrent, Color.white, Mathf.PingPong(Time.time * 2.2f, 0.5f));

            if (marker == null) return;

            if (_travelling)
                TickTravel(Time.deltaTime);
            else
                // idle: a gentle bob on the spot
                marker.anchoredPosition = PinPos(_currentRegion) + MarkerOffset
                    + new Vector2(0f, Mathf.Sin(Time.time * 2.4f) * 5f);
        }

        void TickTravel(float dt)
        {
            Vector2 a = _travelPath[_travelLeg];
            Vector2 b = _travelPath[_travelLeg + 1];
            float legLen = Mathf.Max(Vector2.Distance(a, b), 0.01f);

            _travelDist += TravelSpeed * dt;
            if (_travelDist >= legLen)
            {
                _travelDist -= legLen;
                _travelLeg++;
                if (_travelLeg >= _travelPath.Length - 1)
                {
                    SnapMarkerTo(_currentRegion); // arrived — remember the fox lives here now
                    return;
                }
                a = _travelPath[_travelLeg];
                b = _travelPath[_travelLeg + 1];
                legLen = Mathf.Max(Vector2.Distance(a, b), 0.01f);
            }

            float t = Mathf.Clamp01(_travelDist / legLen);
            Vector2 pos = Vector2.Lerp(a, b, t);
            // little hops along the way — a traveller, not a slide puzzle
            float hops = Mathf.Max(1f, Mathf.Round(legLen / HopLength));
            pos.y += Mathf.Abs(Mathf.Sin(t * hops * Mathf.PI)) * 12f;
            marker.anchoredPosition = pos + MarkerOffset;

            // face the direction of travel (sprite art faces right by default)
            float dx = b.x - a.x;
            if (Mathf.Abs(dx) > 1f)
            {
                Vector3 s = marker.localScale;
                s.x = Mathf.Abs(s.x) * (dx < 0f ? -1f : 1f);
                marker.localScale = s;
            }
        }

        // ---- buttons -----------------------------------------------------------

        void Play()
        {
            if (!SaveSystem.Exists()) SaveSystem.NewGame();
            SceneFlow.LoadCampaignMatch();
        }

        void NewGame()
        {
            // restarting over an existing tour wipes it — ask once before doing it
            if (SaveSystem.Exists() && !_confirmingNewGame)
            {
                _confirmingNewGame = true;
                if (newGameButtonLabel != null) newGameButtonLabel.text = "Really restart?";
                return;
            }
            PlayerPrefs.SetInt(MarkerPrefKey, 0); // the fox flies home with us
            SaveSystem.NewGame();
            SceneFlow.LoadCampaignMatch();
        }

        void Close() => gameObject.SetActive(false);
    }
}
