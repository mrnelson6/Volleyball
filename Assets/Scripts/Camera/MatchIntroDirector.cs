using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Volleyball
{
    /// <summary>
    /// Plays the pre-match cinematic (<see cref="MatchIntro"/>) on this machine's screen: the
    /// camera sweeps over the court, cuts to an anime-style close-up of each player's eyes,
    /// then all of them at once in a team-vs-team split screen, counts down 3-2-1 while gliding
    /// back into the broadcast view, and hands the camera back a breath before the whistle.
    ///
    /// Pure view, like the character views: it never touches match state (apart from the
    /// offline skip press, which the match itself vets). The authority holds the match in
    /// <see cref="MatchState.Intro"/>; this reads <see cref="MatchManager.IntroElapsed"/> — on
    /// the shared clock, so every client shows the same beat at the same moment — and renders
    /// whatever beat that is, which also makes it correct to join or resume mid-intro.
    /// Added at runtime by <see cref="MatchManager"/> (no scene rebuild); absent on a headless
    /// server. Runs its LateUpdate after the character views so it frames this frame's pose.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class MatchIntroDirector : MonoBehaviour
    {
        public MatchManager match;

        Camera _cam;
        bool _haveHome, _active;
        Vector3 _homePos;
        Quaternion _homeRot;
        float _homeFov;
        int _homeMask;
        CameraClearFlags _homeClear;
        Color _homeBackground;
        int _beat = int.MinValue; // which beat played last frame — sounds fire on beat changes

        // eye framing, measured once per player (the idle bob must not breathe the lens)
        readonly Dictionary<VolleyPlayer, EyeRig> _rigs = new Dictionary<VolleyPlayer, EyeRig>();
        readonly List<VolleyPlayer> _cast = new List<VolleyPlayer>();

        // overlay (built on first use)
        Canvas _canvas;
        RectTransform _barTop, _barBottom, _edgeTop, _edgeBottom;
        RawImage _linesTop, _linesBottom;
        Image _flash, _glint, _edgeTopImg, _edgeBottomImg;
        Text _name, _tag, _count, _title, _skip;
        CanvasGroup _hud;
        PauseMenu _pause;

        // versus split screen: one extra camera per player, drawn over a team-coloured backdrop
        // canvas that the main camera renders alone (a screen-space-overlay canvas would paint
        // over the strip cameras; one bound to the main camera draws beneath them)
        readonly List<Camera> _stripCams = new List<Camera>();
        bool _versusOn;
        Canvas _backdrop;
        Image _sideA, _sideB;
        RawImage _backLines;
        Text _headA, _headB, _vs;
        RectTransform _divider;
        readonly List<Text> _stripNames = new List<Text>();
        readonly List<Image> _shutters = new List<Image>();
        readonly List<Image> _nameTabs = new List<Image>();

        struct EyeRig
        {
            public Transform head;   // the rig's Head bone (null: sprite view — use the body)
            public float headRadius; // world size of the head, which scales the whole framing
            public float up, fwd;    // eye centre above / in front of the Head bone, in head radii
        }

        void Start()
        {
            if (match == null) match = GetComponent<MatchManager>();
        }

        void OnDestroy()
        {
            if (_hud != null) _hud.alpha = 1f;
        }

        // ------------------------------------------------------------------ driver

        void LateUpdate()
        {
            if (match == null) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            if (!match.InIntro)
            {
                if (_active) Finish();
                return;
            }
            if (!_active) Begin();

            bool paused = _pause != null && _pause.panel != null && _pause.panel.activeInHierarchy;
            _canvas.enabled = !paused; // the pause menu lives on the HUD — let it show

            // offline, any hit (or jump) skips to the end — online the intro is everyone's
            if (!paused && !NetworkSession.IsOnline && match.IntroElapsed > 0.4f && SkipPressed())
                match.SkipIntro();

            BuildCast(); // every frame: a dropped player's AI takeover swaps the component mid-intro
            float t = match.IntroElapsed;
            int shots = _cast.Count;

            if (t < MatchIntro.EyesStart)
                FlyOver(t / MatchIntro.FlyLength);
            else if (t < MatchIntro.VersusStart(shots))
            {
                float local = t - MatchIntro.EyesStart;
                int i = Mathf.Min(shots - 1, (int)(local / MatchIntro.EyeShotLength));
                EyeShot(i, local / MatchIntro.EyeShotLength - i);
            }
            else if (t < MatchIntro.CountStart(shots))
                Versus((t - MatchIntro.VersusStart(shots)) / MatchIntro.VersusLength);
            else if (t < MatchIntro.SettleStart(shots))
            {
                float local = t - MatchIntro.CountStart(shots);
                int k = Mathf.Min(MatchIntro.CountFrom - 1, (int)(local / MatchIntro.CountBeat));
                Countdown(k, local / MatchIntro.CountBeat - k, local / (MatchIntro.CountFrom * MatchIntro.CountBeat));
            }
            else
                Settle((t - MatchIntro.SettleStart(shots)) / MatchIntro.SettleLength);

            if (_hud != null) _hud.alpha = paused ? 1f : _hudAlpha;
        }

        float _hudAlpha;

        void Begin()
        {
            _active = true;
            _beat = int.MinValue;
            if (!_haveHome)
            {
                // the arena's broadcast camera never moves during play, so its pose at the
                // first intro frame IS the game view to come back to
                _homePos = _cam.transform.position;
                _homeRot = _cam.transform.rotation;
                _homeFov = _cam.fieldOfView;
                _homeMask = _cam.cullingMask;
                _homeClear = _cam.clearFlags;
                _homeBackground = _cam.backgroundColor;
                _haveHome = true;
            }
            EnsureOverlay();
            _canvas.gameObject.SetActive(true);

            var hud = FindAnyObjectByType<ScoreHUD>();
            Canvas hudCanvas = hud != null ? hud.GetComponentInParent<Canvas>() : null;
            if (hudCanvas != null)
            {
                hudCanvas = hudCanvas.rootCanvas;
                _hud = hudCanvas.GetComponent<CanvasGroup>();
                if (_hud == null) _hud = hudCanvas.gameObject.AddComponent<CanvasGroup>();
            }
            _pause = FindAnyObjectByType<PauseMenu>();
            _title.text = TitleText();
            _skip.gameObject.SetActive(!NetworkSession.IsOnline);
        }

        void Finish()
        {
            _active = false;
            ShowVersus(false);
            _cam.transform.SetPositionAndRotation(_homePos, _homeRot);
            _cam.fieldOfView = _homeFov;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            if (_hud != null) _hud.alpha = 1f;
        }

        /// <summary>Eye shots in a fixed order every machine agrees on: team A then team B,
        /// left half first.</summary>
        void BuildCast()
        {
            _cast.Clear();
            foreach (var p in match.players)
                if (p != null) _cast.Add(p);
            _cast.Sort((a, b) => a.team != b.team ? a.team.CompareTo(b.team) : a.halfSign.CompareTo(b.halfSign));
        }

        bool SkipPressed()
        {
            var input = GameInput.Instance;
            return input != null && (input.AnyHitPressed || input.JumpPressed);
        }

        string TitleText()
        {
            string label = MatchSetup.Current != null ? MatchSetup.Current.matchLabel : null;
            return string.IsNullOrEmpty(label) ? "MATCH DAY" : label.ToUpperInvariant();
        }

        /// <summary>A beat just started this frame (and remember it).</summary>
        bool Enter(int beat)
        {
            if (beat == _beat) return false;
            _beat = beat;
            return true;
        }

        // ------------------------------------------------------------------ shots

        /// <summary>
        /// Establishing sweep: from high behind team B's end, down the length of the court and
        /// over the net, banking out to team A's corner. Positions scale with the court so every
        /// arena gets the same move.
        /// </summary>
        void FlyOver(float u)
        {
            ShowVersus(false);
            if (Enter(-1))
            {
                GameAudio.PlayIntroWhoosh();
                GameAudio.PlayCrowd(0.35f);
            }

            float hw = CourtGeometry.HalfWidth, hd = CourtGeometry.HalfDepth;
            float e = Smooth(u);
            Vector3 p = Bezier(new Vector3(0f, 15f, hd * 3.4f),
                               new Vector3(0f, 9f, hd * 1.2f),
                               new Vector3(hw * 0.7f, 7.5f, -hd * 0.8f),
                               new Vector3(hw * 3f, 7f, -hd * 1.75f), e); // above the corner stands
            Vector3 look = Vector3.Lerp(new Vector3(0f, 0.3f, -hd * 0.3f), new Vector3(0f, 1.2f, 0f), e);
            float roll = Mathf.Sin(e * Mathf.PI) * -7f; // bank through the turn
            Place(p, look, roll, Mathf.Lerp(52f, 42f, e));

            // cinema bars + the match title as a lower third
            SetBars(0.11f);
            SetEdges(0f, Color.clear);
            SetLines(0f, 0f);
            _hudAlpha = 0f;
            float titleIn = Mathf.Clamp01(u * 5f) * Mathf.Clamp01((1f - u) * 6f);
            SetText(_title, titleIn, new Vector2(Mathf.Lerp(-80f, 0f, Smooth(Mathf.Clamp01(u * 3f))), 0f));
            SetText(_name, 0f, Vector2.zero);
            SetText(_tag, 0f, Vector2.zero);
            SetText(_count, 0f, Vector2.zero);
            SetFlash(0f);
            SetGlint(0f, Vector2.zero, 0f);
        }

        /// <summary>
        /// The anime eye close-up: a long lens straight into the player's face, the frame crushed
        /// to a thin band across the eyes, speed lines streaking through the bars in the team's
        /// direction, a white flash on the cut and a glint off one eye.
        /// </summary>
        void EyeShot(int i, float u)
        {
            if (i < 0 || i >= _cast.Count || _cast[i] == null) return;
            VolleyPlayer p = _cast[i];
            ShowVersus(false);
            bool enter = Enter(i);
            if (enter) GameAudio.PlayEyeSting(1f + 0.05f * i);

            const float band = 0.32f; // the screen band left between the bars
            float dir = p.team == TeamSide.A ? 1f : -1f; // team A rushes left→right, team B back
            AimAtEyes(_cam, p, band, u, dir, out Vector3 eye, out Vector3 screenRight, out float hr);

            // bars slam in on the cut
            float slam = Smooth(Mathf.Clamp01(u * MatchIntro.EyeShotLength / 0.12f));
            float bar = Mathf.Lerp(0.11f, (1f - band) * 0.5f, slam);
            SetBars(bar);
            Color team = p.jerseyColor;
            team.a = 1f;
            SetEdges(bar, team);
            SetLines(0.22f, -dir * Time.time * 3.2f); // uv scrolling back = streaks rushing forward
            _hudAlpha = 0f;

            // name card: slides in along the streaks, holds, snaps off before the next cut
            float inT = Smooth(Mathf.Clamp01((u - 0.05f) / 0.18f));
            float outT = Mathf.Clamp01((1f - u) / 0.08f);
            float slide = (1f - inT) * 420f * -dir;
            _name.text = p.Character != null ? p.Character.displayName.ToUpperInvariant() : p.name;
            _name.alignment = dir > 0f ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            _tag.text = TagFor(p);
            _tag.alignment = _name.alignment;
            _tag.color = Color.Lerp(team, Color.white, 0.35f);
            SetText(_name, inT * outT, new Vector2(slide + dir * 60f * u, 0f));
            SetText(_tag, inT * outT, new Vector2(slide * 1.3f + dir * 40f * u, 0f));
            SetText(_title, 0f, Vector2.zero);
            SetText(_count, 0f, Vector2.zero);

            // white flash on the cut, then the glint off the eye nearest screen-right
            SetFlash(Mathf.Clamp01(1f - u * MatchIntro.EyeShotLength / 0.14f));
            float g = Mathf.Clamp01((u - 0.32f) / 0.35f);
            float glint = g > 0f && g < 1f ? Mathf.Sin(g * Mathf.PI) : 0f;
            Vector3 eyeWorld = eye + screenRight * (0.38f * hr) + Vector3.up * (0.08f * hr);
            Vector3 sp = _cam.WorldToScreenPoint(eyeWorld);
            SetGlint(glint, new Vector2(sp.x, sp.y), g * 120f);
        }

        /// <summary>3 · 2 · 1 over one continuous pull-back that lands exactly on the broadcast
        /// camera — so "back to the game view" is a glide, not a cut.</summary>
        void Countdown(int k, float beatU, float u)
        {
            int number = MatchIntro.CountFrom - k;
            ShowVersus(false);
            if (Enter(100 + k)) GameAudio.PlayCountdown(number);

            // start pushed in toward the court and swung round a little, then ease out home
            Vector3 focus = HomeFocus();
            Vector3 startPos = Vector3.Lerp(focus, _homePos, 0.38f) + (_homeRot * Vector3.right) * -3f;
            Quaternion startRot = Quaternion.LookRotation(focus + Vector3.up * 0.4f - startPos, Vector3.up);
            float e = EaseOutCubic(u);
            _cam.transform.SetPositionAndRotation(Vector3.Lerp(startPos, _homePos, e),
                                                  Quaternion.Slerp(startRot, _homeRot, e));
            _cam.fieldOfView = Mathf.Lerp(46f, _homeFov, e);

            SetBars(Mathf.Lerp(0.11f, 0f, Mathf.Clamp01((u - 0.55f) / 0.45f)));
            SetEdges(0f, Color.clear);
            SetLines(0f, 0f);
            SetFlash(k == 0 ? Mathf.Clamp01(1f - beatU / 0.12f) * 0.8f : 0f); // flash out of the last eyes
            _hudAlpha = 0f;

            // the number punches in big and settles, then fades as its beat ends
            _count.text = number.ToString();
            float punch = 1f + 0.9f * Mathf.Pow(1f - Mathf.Clamp01(beatU / 0.18f), 2f);
            float alpha = Mathf.Clamp01(beatU / 0.05f) * Mathf.Clamp01((1f - beatU) / 0.2f);
            _count.enabled = alpha > 0.001f;
            _count.color = number == 1 ? new Color(1f, 0.82f, 0.25f, alpha) : new Color(1f, 1f, 1f, alpha);
            _count.rectTransform.localScale = Vector3.one * punch;
            _count.rectTransform.anchoredPosition = Vector2.zero;
            SetText(_title, 0f, Vector2.zero);
            SetText(_name, 0f, Vector2.zero);
            SetText(_tag, 0f, Vector2.zero);
            SetGlint(0f, Vector2.zero, 0f);
        }

        /// <summary>Back in the game view with the HUD fading up — the whistle ends it.</summary>
        void Settle(float u)
        {
            ShowVersus(false);
            Enter(200);
            _cam.transform.SetPositionAndRotation(_homePos, _homeRot);
            _cam.fieldOfView = _homeFov;
            SetBars(0f);
            SetEdges(0f, Color.clear);
            SetLines(0f, 0f);
            SetFlash(0f);
            SetGlint(0f, Vector2.zero, 0f);
            SetText(_title, 0f, Vector2.zero);
            SetText(_name, 0f, Vector2.zero);
            SetText(_tag, 0f, Vector2.zero);
            SetText(_count, 0f, Vector2.zero);
            _hudAlpha = Mathf.Clamp01(u * 2f);
        }

        /// <summary>
        /// Everyone at once, and who's with whom: every player's eyes in a full-width strip,
        /// stacked — team A's pair on top over their team colour, team B's pair below over
        /// theirs — with a gap across the middle where a slanted rule and a big VS slam down.
        /// The strips wipe in one after another (team A from the left, team B from the right),
        /// then the whole line-up holds.
        /// </summary>
        void Versus(float u)
        {
            ShowVersus(true);
            float s = u * MatchIntro.VersusLength; // seconds into the shot
            const float slamAt = 0.95f;
            int beat = s < slamAt ? 50 : 51;
            if (Enter(beat))
            {
                if (beat == 50) GameAudio.PlayIntroWhoosh();
                else { GameAudio.PlayEyeSting(0.78f); GameAudio.PlayCrowd(0.5f); }
            }

            var teamA = new List<VolleyPlayer>();
            var teamB = new List<VolleyPlayer>();
            foreach (var p in _cast)
                (p.team == TeamSide.A ? teamA : teamB).Add(p);
            Color colA = TeamColor(teamA, new Color(0.2f, 0.5f, 1f));
            Color colB = TeamColor(teamB, new Color(1f, 0.35f, 0.3f));

            // backdrop: top half team A's colour, bottom half team B's, streaks rushing through
            _sideA.color = Color.Lerp(colA, Color.black, 0.5f);
            _sideB.color = Color.Lerp(colB, Color.black, 0.5f);
            _backLines.uvRect = new Rect(Time.time * 1.1f, 0f, 2.2f, 1f);

            // team headings in the middle gap: A's just above the rule, B's just below it
            string mine = ViewerTeamLabel(out TeamSide viewerTeam);
            _headA.text = mine != null ? (viewerTeam == TeamSide.A ? "YOUR TEAM" : "RIVALS") : "TEAM 1";
            _headB.text = mine != null ? (viewerTeam == TeamSide.B ? "YOUR TEAM" : "RIVALS") : "TEAM 2";
            _headA.color = Color.Lerp(colA, Color.white, 0.5f);
            _headB.color = Color.Lerp(colB, Color.white, 0.5f);
            float headIn = Smooth(Mathf.Clamp01((s - 0.1f) / 0.3f));
            _headA.rectTransform.anchoredPosition = new Vector2((1f - headIn) * -400f, 0f);
            _headB.rectTransform.anchoredPosition = new Vector2((1f - headIn) * 400f, 0f);

            int slot = 0;
            for (int side = 0; side < 2; side++)
            {
                List<VolleyPlayer> team = side == 0 ? teamA : teamB;
                float dir = side == 0 ? 1f : -1f; // A sweeps in from the left, B from the right
                for (int k = 0; k < team.Count; k++, slot++)
                {
                    VolleyPlayer p = team[k];
                    Rect strip = StripRect(side, k, team.Count);
                    Camera sc = StripCam(slot);
                    sc.enabled = true;
                    sc.rect = strip;
                    AimAtEyes(sc, p, 1f, u, dir, out _, out _, out _, 0.62f);

                    // a team-coloured shutter wipes off the strip toward the far side
                    float reveal = Smooth(Mathf.Clamp01((s - 0.05f - 0.16f * slot) / 0.28f));
                    Image sh = Shutter(slot);
                    sh.enabled = reveal < 1f;
                    sh.color = Color.Lerp(side == 0 ? colA : colB, Color.white, 0.3f);
                    float left = strip.xMin, right = strip.xMax;
                    if (side == 0) left = Mathf.Lerp(strip.xMin, strip.xMax, reveal);
                    else right = Mathf.Lerp(strip.xMax, strip.xMin, reveal);
                    sh.rectTransform.anchorMin = new Vector2(left, strip.yMin);
                    sh.rectTransform.anchorMax = new Vector2(right, strip.yMax);

                    // the name rides on a team-coloured tab at the strip's open side, clear of the
                    // face in the middle (bare lettering washes out against sand and sky)
                    Image tab = NameTab(slot);
                    tab.enabled = reveal > 0f;
                    float tabW = 0.3f * reveal;
                    tab.rectTransform.anchorMin = new Vector2(side == 0 ? strip.xMin : strip.xMax - tabW, strip.yMin);
                    tab.rectTransform.anchorMax = new Vector2(side == 0 ? strip.xMin + tabW : strip.xMax, strip.yMax);
                    Color tc = Color.Lerp(side == 0 ? colA : colB, Color.black, 0.35f);
                    tc.a = 0.82f;
                    tab.color = tc;

                    Text nm = StripName(slot);
                    nm.enabled = reveal > 0f;
                    var nrt = nm.rectTransform;
                    nrt.anchorMin = new Vector2(strip.xMin + 0.025f, strip.yMin);
                    nrt.anchorMax = new Vector2(strip.xMax - 0.025f, strip.yMax);
                    nrt.anchoredPosition = new Vector2((1f - reveal) * -60f * dir, 0f);
                    string who = p.Character != null ? p.Character.displayName.ToUpperInvariant() : p.name;
                    nm.text = TagFor(p) == "YOU" ? who + "\n(YOU)" : who;
                    nm.alignment = side == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
                    nm.color = new Color(1f, 1f, 1f, reveal);
                }
            }
            for (int i = slot; i < _stripCams.Count; i++) _stripCams[i].enabled = false;
            for (int i = slot; i < _shutters.Count; i++) _shutters[i].enabled = false;
            for (int i = slot; i < _stripNames.Count; i++) _stripNames[i].enabled = false;
            for (int i = slot; i < _nameTabs.Count; i++) _nameTabs[i].enabled = false;

            // the rule sweeps across the gap, then the VS slams onto it
            float slam = Mathf.Clamp01((s - slamAt) / 0.14f);
            _divider.gameObject.SetActive(s > slamAt - 0.15f);
            _divider.localScale = new Vector3(Smooth(Mathf.Clamp01((s - slamAt + 0.15f) / 0.15f)), 1f, 1f);
            _vs.enabled = s >= slamAt;
            _vs.rectTransform.localScale = Vector3.one * (1f + 1.4f * (1f - Smooth(slam)));
            _vs.color = new Color(1f, 0.85f, 0.2f, Smooth(slam));

            SetBars(0f);
            SetEdges(0f, Color.clear);
            SetLines(0f, 0f);
            SetFlash(s >= slamAt ? Mathf.Clamp01(1f - (s - slamAt) / 0.14f) * 0.85f
                                 : Mathf.Clamp01(1f - s / 0.1f) * 0.6f);
            SetGlint(0f, Vector2.zero, 0f);
            SetText(_title, 0f, Vector2.zero);
            SetText(_name, 0f, Vector2.zero);
            SetText(_tag, 0f, Vector2.zero);
            SetText(_count, 0f, Vector2.zero);
            _hudAlpha = 0f;
        }

        /// <summary>
        /// Hand the screen to the split-screen setup (the main camera draws only the backdrop,
        /// the strip cameras draw the eyes over it), or give it back.
        /// </summary>
        void ShowVersus(bool on)
        {
            if (on == _versusOn) return;
            _versusOn = on;
            if (on)
            {
                EnsureBackdrop();
                _backdrop.gameObject.SetActive(true);
                _cam.cullingMask = 1 << UILayer;
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = Color.black;
                return;
            }
            if (_haveHome && _cam != null)
            {
                _cam.cullingMask = _homeMask;
                _cam.clearFlags = _homeClear;
                _cam.backgroundColor = _homeBackground;
            }
            foreach (var c in _stripCams) if (c != null) c.enabled = false;
            foreach (var s in _shutters) if (s != null) s.enabled = false;
            if (_backdrop != null) _backdrop.gameObject.SetActive(false);
            if (_divider != null) _divider.gameObject.SetActive(false);
            if (_vs != null) _vs.enabled = false;
            foreach (var n in _stripNames) if (n != null) n.enabled = false;
            foreach (var t in _nameTabs) if (t != null) t.enabled = false;
        }

        const int UILayer = 5; // Unity's built-in "UI" layer

        /// <summary>
        /// Where strip <paramref name="k"/> of <paramref name="count"/> sits: full screen width,
        /// team A's strips stacked in the top block (0), team B's in the bottom block (1), with
        /// the VS gap between the blocks.
        /// </summary>
        static Rect StripRect(int side, int k, int count)
        {
            const float margin = 0.03f, vsGap = 0.16f, pairGap = 0.018f;
            float blockH = (1f - 2f * margin - vsGap) * 0.5f;
            float blockTop = side == 0 ? 1f - margin : margin + blockH;
            count = Mathf.Max(1, count);
            float stripH = (blockH - (count - 1) * pairGap) / count;
            float yTop = blockTop - k * (stripH + pairGap);
            return new Rect(0f, yTop - stripH, 1f, stripH);
        }

        static Color TeamColor(List<VolleyPlayer> team, Color fallback)
        {
            if (team.Count == 0) return fallback;
            Color c = team[0].jerseyColor;
            c.a = 1f;
            return c;
        }

        /// <summary>Non-null when this machine has a local human (whose team is "YOUR TEAM").</summary>
        string ViewerTeamLabel(out TeamSide team)
        {
            team = TeamSide.A;
            foreach (var q in _cast)
                if (q != null && q.IsHuman && q.IsLocallyControlled) { team = q.team; return "you"; }
            return null;
        }

        Camera StripCam(int i)
        {
            while (_stripCams.Count <= i)
            {
                var go = new GameObject("IntroStripCam" + _stripCams.Count);
                go.transform.SetParent(transform, false);
                var c = go.AddComponent<Camera>();
                c.CopyFrom(_cam);
                // copy the GAME look, not the backdrop-only state the main camera is in now
                c.cullingMask = _homeMask;
                c.clearFlags = _homeClear;
                c.backgroundColor = _homeBackground;
                c.depth = _cam.depth + 1 + _stripCams.Count;
                c.enabled = false;
                _stripCams.Add(c);
            }
            return _stripCams[i];
        }

        Image Shutter(int i)
        {
            while (_shutters.Count <= i)
            {
                var rt = Rect(_canvas.transform, "Shutter" + _shutters.Count, Vector2.zero, Vector2.zero);
                rt.SetSiblingIndex(_flash.transform.GetSiblingIndex()); // under the VS and the flash
                var img = AddImage(rt, Color.white);
                img.enabled = false;
                _shutters.Add(img);
            }
            return _shutters[i];
        }

        Image NameTab(int i)
        {
            while (_nameTabs.Count <= i)
            {
                var rt = Rect(_canvas.transform, "NameTab" + _nameTabs.Count, Vector2.zero, Vector2.zero);
                rt.SetSiblingIndex(_flash.transform.GetSiblingIndex()); // under the names and flash
                var img = AddImage(rt, Color.clear);
                img.enabled = false;
                _nameTabs.Add(img);
            }
            return _nameTabs[i];
        }

        Text StripName(int i)
        {
            while (_stripNames.Count <= i)
            {
                var t = MakeText(_canvas.transform, "StripName" + _stripNames.Count, 46, FontStyle.BoldAndItalic,
                                 Vector2.zero, Vector2.zero);
                t.transform.SetSiblingIndex(_flash.transform.GetSiblingIndex()); // under the flash
                _stripNames.Add(t);
            }
            return _stripNames[i];
        }

        void EnsureBackdrop()
        {
            if (_backdrop != null) return;

            var go = new GameObject("MatchIntroBackdrop");
            go.transform.SetParent(transform, false);
            _backdrop = go.AddComponent<Canvas>();
            _backdrop.renderMode = RenderMode.ScreenSpaceCamera;
            _backdrop.worldCamera = _cam;
            _backdrop.planeDistance = _cam.nearClipPlane + 0.2f;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _sideA = AddImage(Rect(go.transform, "SideA", new Vector2(0f, 0.5f), Vector2.one), Color.black);
            _sideB = AddImage(Rect(go.transform, "SideB", Vector2.zero, new Vector2(1f, 0.5f)), Color.black);
            var lines = Rect(go.transform, "Streaks", Vector2.zero, Vector2.one);
            _backLines = lines.gameObject.AddComponent<RawImage>();
            _backLines.texture = MakeStreakTexture();
            _backLines.color = new Color(1f, 1f, 1f, 0.13f);
            _backLines.raycastTarget = false;

            _headA = MakeText(go.transform, "HeadA", 52, FontStyle.BoldAndItalic,
                              new Vector2(0.04f, 0.5f), new Vector2(0.42f, 0.58f));
            _headA.alignment = TextAnchor.MiddleLeft;
            _headB = MakeText(go.transform, "HeadB", 52, FontStyle.BoldAndItalic,
                              new Vector2(0.58f, 0.42f), new Vector2(0.96f, 0.5f));
            _headB.alignment = TextAnchor.MiddleRight;
            SetLayer(go, UILayer);

            // the divider and the VS badge go on the overlay: they sit over the strip seams
            _divider = Rect(_canvas.transform, "Divider", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _divider.sizeDelta = new Vector2(2400f, 10f);
            _divider.localRotation = Quaternion.Euler(0f, 0f, 4f);
            _divider.SetSiblingIndex(_flash.transform.GetSiblingIndex());
            AddImage(_divider, Color.white);
            _divider.gameObject.SetActive(false);

            _vs = MakeText(_canvas.transform, "VS", 160, FontStyle.BoldAndItalic,
                           new Vector2(0.4f, 0.4f), new Vector2(0.6f, 0.6f));
            _vs.alignment = TextAnchor.MiddleCenter;
            _vs.text = "VS";
            _vs.transform.SetSiblingIndex(_flash.transform.GetSiblingIndex());
            var outline = _vs.GetComponent<Outline>();
            outline.effectDistance = new Vector2(7f, -7f);
            outline.effectColor = Color.black;
            _vs.enabled = false;
        }

        static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayer(c.gameObject, layer);
        }

        // ------------------------------------------------------------------ framing helpers

        /// <summary>
        /// Point <paramref name="cam"/> straight into <paramref name="p"/>'s eyes with a long
        /// lens. <paramref name="band"/> is the fraction of the camera's height that will show
        /// (the rest hidden under bars; 1 = its whole viewport): that visible band holds the eyes
        /// plus a little brow, widening on narrow frames so both eyes fit. A slow push-in over
        /// <paramref name="u"/>, and a sideways drift against the team's rush direction
        /// <paramref name="dir"/> so the face rides with the speed lines.
        /// </summary>
        void AimAtEyes(Camera cam, VolleyPlayer p, float band, float u, float dir,
                       out Vector3 eye, out Vector3 screenRight, out float hr, float content = 0.7f)
        {
            EyeRig rig = RigFor(p);
            EyeFrame(p, rig, out eye, out Vector3 fwd);
            hr = rig.headRadius;

            float aspect = Mathf.Max(0.3f, cam.aspect);
            float frameH = Mathf.Max(content * hr / band, 2.6f * hr / aspect);
            const float lensFov = 14f;
            float dist = frameH / (2f * Mathf.Tan(lensFov * 0.5f * Mathf.Deg2Rad));

            // the camera looks along -fwd, so this is screen-right
            screenRight = Vector3.Cross(Vector3.up, -fwd).normalized;
            Vector3 camPos = eye + fwd * dist - screenRight * (dir * (u - 0.5f) * 0.2f * hr);
            float push = Mathf.Lerp(1f, 0.9f, Smooth(u));
            float fov = 2f * Mathf.Atan(frameH * push / (2f * dist)) * Mathf.Rad2Deg;
            PlaceCam(cam, camPos, eye, 0f, fov);
        }

        void Place(Vector3 pos, Vector3 look, float roll, float fov) => PlaceCam(_cam, pos, look, roll, fov);

        static void PlaceCam(Camera cam, Vector3 pos, Vector3 look, float roll, float fov)
        {
            Vector3 d = look - pos;
            if (d.sqrMagnitude < 1e-6f) d = Vector3.forward;
            Quaternion rot = Quaternion.LookRotation(d, Vector3.up) * Quaternion.Euler(0f, 0f, roll);
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = fov;
        }

        /// <summary>Where the broadcast camera looks: its forward ray meeting chest height.</summary>
        Vector3 HomeFocus()
        {
            Vector3 f = _homeRot * Vector3.forward;
            float t = f.y < -0.01f ? (_homePos.y - 1.2f) / -f.y : 20f;
            return _homePos + f * t;
        }

        /// <summary>
        /// Measure a player's head once. The generated rigs (Tools/blender/animal_gen.py) put
        /// the Head bone's root at the neck top and the eyes ~1 head radius above it and ~0.9
        /// in front; the head radius is 0.30 in a body whose hips sit at 0.56, so the hips'
        /// height gives the world scale of any animal at any size.
        /// </summary>
        EyeRig RigFor(VolleyPlayer p)
        {
            if (_rigs.TryGetValue(p, out EyeRig rig) && (rig.head != null || !HasModel(p))) return rig;

            rig = new EyeRig { headRadius = 0.3f * Mathf.Max(0.3f, p.Character != null ? p.Character.height : 1f),
                               up = 0f, fwd = 0f };
            var view = p.GetComponentInChildren<ModelCharacterView>();
            if (view != null)
            {
                Transform head = FindDeep(view.transform, "Head");
                Transform hips = FindDeep(view.transform, "Hips");
                if (head != null && hips != null)
                {
                    float scale = Mathf.Max(0.05f, (hips.position.y - view.transform.position.y) / 0.56f);
                    rig.head = head;
                    rig.headRadius = 0.30f * scale;
                    rig.up = 1.03f;  // 0.85 (head centre) + 0.18 (eyes above centre)
                    rig.fwd = 0.88f; // the face plane
                }
            }
            _rigs[p] = rig;
            return rig;
        }

        static bool HasModel(VolleyPlayer p) => p.GetComponentInChildren<ModelCharacterView>() != null;

        /// <summary>This frame's eye centre and the direction the face looks.</summary>
        void EyeFrame(VolleyPlayer p, in EyeRig rig, out Vector3 eye, out Vector3 fwd)
        {
            if (rig.head != null)
            {
                var view = rig.head.GetComponentInParent<ModelCharacterView>();
                fwd = view != null ? view.transform.forward : Vector3.forward;
                fwd.y = 0f;
                fwd = fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector3.forward;
                eye = rig.head.position + Vector3.up * (rig.up * rig.headRadius) + fwd * (rig.fwd * rig.headRadius);
                return;
            }
            // sprite fallback: billboards face the camera, so shoot from the broadcast side
            fwd = -(_homeRot * Vector3.forward);
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector3.forward;
            float h = p.Character != null ? p.Character.height : 1f;
            eye = p.transform.position + Vector3.up * (1.45f * h);
        }

        string TagFor(VolleyPlayer p)
        {
            VolleyPlayer viewer = null;
            foreach (var q in _cast)
                if (q != null && q.IsHuman && q.IsLocallyControlled) { viewer = q; break; }
            if (p == viewer) return "YOU";
            TeamSide mine = viewer != null ? viewer.team : TeamSide.A;
            return p.team == mine ? "TEAMMATE" : "RIVAL";
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        static float EaseOutCubic(float x) { x = 1f - Mathf.Clamp01(x); return 1f - x * x * x; }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float s = 1f - t;
            return s * s * s * a + 3f * s * s * t * b + 3f * s * t * t * c + t * t * t * d;
        }

        // ------------------------------------------------------------------ overlay

        void SetBars(float frac)
        {
            _barTop.anchorMin = new Vector2(0f, 1f - frac);
            _barBottom.anchorMax = new Vector2(1f, frac);
        }

        /// <summary>Thin team-coloured rules along the inner edges of the bars.</summary>
        void SetEdges(float barFrac, Color c)
        {
            bool on = c.a > 0f && barFrac > 0f;
            _edgeTopImg.enabled = _edgeBottomImg.enabled = on;
            if (!on) return;
            _edgeTopImg.color = _edgeBottomImg.color = c;
            _edgeTop.anchorMin = new Vector2(0f, 1f - barFrac);
            _edgeTop.anchorMax = new Vector2(1f, 1f - barFrac);
            _edgeBottom.anchorMin = new Vector2(0f, barFrac);
            _edgeBottom.anchorMax = new Vector2(1f, barFrac);
        }

        void SetLines(float alpha, float scroll)
        {
            foreach (var r in new[] { _linesTop, _linesBottom })
            {
                r.enabled = alpha > 0f;
                r.color = new Color(1f, 1f, 1f, alpha);
                r.uvRect = new Rect(scroll + (r == _linesBottom ? 0.37f : 0f), 0f, 1.6f, 1f);
            }
        }

        void SetFlash(float a) => _flash.color = new Color(1f, 1f, 1f, a);

        void SetGlint(float amount, Vector2 screen, float spin)
        {
            _glint.enabled = amount > 0.001f;
            if (!_glint.enabled) return;
            var rt = _glint.rectTransform;
            rt.position = new Vector3(screen.x, screen.y, 0f);
            rt.localScale = Vector3.one * (0.4f + 0.9f * amount);
            rt.localRotation = Quaternion.Euler(0f, 0f, spin);
            _glint.color = new Color(1f, 1f, 1f, amount);
        }

        static void SetText(Text t, float alpha, Vector2 offset)
        {
            t.enabled = alpha > 0.001f;
            Color c = t.color;
            c.a = alpha;
            t.color = c;
            t.rectTransform.anchoredPosition = offset;
            t.rectTransform.localScale = Vector3.one;
        }

        void EnsureOverlay()
        {
            if (_canvas != null) return;

            var go = new GameObject("MatchIntroOverlay");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50; // over the HUD
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            // purely presentational: no GraphicRaycaster, so it can never eat a click

            Texture2D streaks = MakeStreakTexture();
            _barTop = Rect(go.transform, "BarTop", new Vector2(0f, 0.9f), Vector2.one);
            _barBottom = Rect(go.transform, "BarBottom", Vector2.zero, new Vector2(1f, 0.1f));
            AddImage(_barTop, Color.black);
            AddImage(_barBottom, Color.black);
            _linesTop = AddLines(_barTop, streaks);
            _linesBottom = AddLines(_barBottom, streaks);

            _edgeTop = Rect(go.transform, "EdgeTop", Vector2.zero, Vector2.one);
            _edgeBottom = Rect(go.transform, "EdgeBottom", Vector2.zero, Vector2.one);
            _edgeTop.sizeDelta = _edgeBottom.sizeDelta = new Vector2(0f, 6f);
            _edgeTopImg = AddImage(_edgeTop, Color.white);
            _edgeBottomImg = AddImage(_edgeBottom, Color.white);

            // name card rides in the bottom bar during the eye shots
            _name = MakeText(_barBottom, "Name", 64, FontStyle.BoldAndItalic,
                             new Vector2(0.06f, 0.35f), new Vector2(0.94f, 0.95f));
            _tag = MakeText(_barBottom, "Tag", 30, FontStyle.Bold,
                            new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.4f));

            _title = MakeText(_barBottom, "Title", 42, FontStyle.BoldAndItalic,
                              new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));
            _title.alignment = TextAnchor.MiddleLeft;

            _count = MakeText(go.transform, "Count", 260, FontStyle.Bold,
                              new Vector2(0.3f, 0.25f), new Vector2(0.7f, 0.75f));
            _count.alignment = TextAnchor.MiddleCenter;
            var shadow = _count.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(8f, -8f);

            var skipRt = Rect(_barTop, "Skip", new Vector2(0.6f, 0f), new Vector2(0.98f, 1f));
            _skip = skipRt.gameObject.AddComponent<Text>();
            _skip.font = ChatArt.UIFont();
            _skip.fontSize = 24;
            _skip.alignment = TextAnchor.MiddleRight;
            _skip.color = new Color(1f, 1f, 1f, 0.55f);
            _skip.text = GameInput.UsingGamepad ? "A / X — skip" : "Space / J — skip";
            _skip.raycastTarget = false;

            var glintRt = Rect(go.transform, "Glint", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            glintRt.sizeDelta = new Vector2(150f, 150f);
            _glint = glintRt.gameObject.AddComponent<Image>();
            _glint.sprite = MakeGlintSprite();
            _glint.raycastTarget = false;

            var flashRt = Rect(go.transform, "Flash", Vector2.zero, Vector2.one);
            _flash = AddImage(flashRt, Color.clear);
        }

        static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static Image AddImage(RectTransform rt, Color c)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        static RawImage AddLines(RectTransform bar, Texture2D tex)
        {
            var rt = Rect(bar, "SpeedLines", Vector2.zero, Vector2.one);
            var img = rt.gameObject.AddComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            return img;
        }

        static Text MakeText(Transform parent, string name, int size, FontStyle style, Vector2 min, Vector2 max)
        {
            var rt = Rect(parent, name, min, max);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = ChatArt.UIFont();
            t.fontSize = size;
            t.fontStyle = style;
            t.color = Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(3f, -3f);
            return t;
        }

        /// <summary>Horizontal anime speed lines: thin streaks with a bright head and a fading
        /// tail, tiling sideways so a scrolling uvRect reads as rushing motion.</summary>
        static Texture2D MakeStreakTexture()
        {
            const int w = 512, h = 64;
            var px = new Color32[w * h];
            var rng = new System.Random(7); // fixed look, every run
            for (int k = 0; k < 46; k++)
            {
                int y = rng.Next(h);
                int thick = rng.Next(1, 3);
                int x0 = rng.Next(w);
                int len = rng.Next(60, 260);
                float bright = 0.4f + 0.6f * (float)rng.NextDouble();
                for (int i = 0; i < len; i++)
                {
                    float a = bright * (1f - (float)i / len); // head → tail fade
                    int x = (x0 + i) % w;
                    for (int dy = 0; dy < thick; dy++)
                    {
                        int yy = Mathf.Min(h - 1, y + dy);
                        int idx = yy * w + x;
                        byte v = (byte)Mathf.Clamp(px[idx].a + a * 255f, 0f, 255f);
                        px[idx] = new Color32(255, 255, 255, v);
                    }
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                name = "IntroSpeedLines",
            };
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>A four-point sparkle with a soft core — the anime eye glint.</summary>
        static Sprite MakeGlintSprite()
        {
            const int n = 96;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float ax = Mathf.Abs(u), ay = Mathf.Abs(v);
                float rays = Mathf.Exp(-ax * 22f) * (1f - ay) + Mathf.Exp(-ay * 22f) * (1f - ax);
                float core = Mathf.Exp(-(u * u + v * v) * 30f);
                float a = Mathf.Clamp01(rays * 0.95f + core);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "IntroGlint" };
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }
    }
}
