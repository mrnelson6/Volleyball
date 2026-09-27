using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Volleyball
{
    /// <summary>
    /// Plays the pre-match cinematic (<see cref="MatchIntro"/>) on this machine's screen: the
    /// camera sweeps over the court, cuts to an anime-style close-up of each player's eyes,
    /// counts down 3-2-1 while gliding back into the broadcast view, and hands the camera back
    /// a breath before the whistle.
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
            else if (t < MatchIntro.CountStart(shots))
            {
                float local = t - MatchIntro.EyesStart;
                int i = Mathf.Min(shots - 1, (int)(local / MatchIntro.EyeShotLength));
                EyeShot(i, local / MatchIntro.EyeShotLength - i);
            }
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
            bool enter = Enter(i);
            if (enter) GameAudio.PlayEyeSting(1f + 0.05f * i);

            EyeRig rig = RigFor(p);
            EyeFrame(p, rig, out Vector3 eye, out Vector3 fwd);
            float hr = rig.headRadius;

            // Band of the screen left between the bars, and the world height it must hold: the
            // eyes plus a little brow. A long lens from a few head-widths out flattens the face
            // like a telephoto anime cut; the frame widens on narrow screens so both eyes fit.
            const float band = 0.30f;
            float aspect = Mathf.Max(0.3f, _cam.aspect);
            float frameH = Mathf.Max(0.62f * hr / band, 2.6f * hr / aspect);
            const float lensFov = 14f;
            float dist = frameH / (2f * Mathf.Tan(lensFov * 0.5f * Mathf.Deg2Rad));

            // the camera looks along -fwd, so this is screen-right
            Vector3 screenRight = Vector3.Cross(Vector3.up, -fwd).normalized;
            float dir = p.team == TeamSide.A ? 1f : -1f; // team A rushes left→right, team B back
            // a slow sideways drift: the camera slides against the rush so the face rides with it
            Vector3 camPos = eye + fwd * dist - screenRight * (dir * (u - 0.5f) * 0.2f * hr);
            float push = Mathf.Lerp(1f, 0.9f, Smooth(u));              // slow push in
            float fov = 2f * Mathf.Atan(frameH * push / (2f * dist)) * Mathf.Rad2Deg;
            Place(camPos, eye, 0f, fov);

            // bars slam in on the cut
            float slam = Smooth(Mathf.Clamp01(u * MatchIntro.EyeShotLength / 0.12f));
            float bar = Mathf.Lerp(0.11f, (1f - band) * 0.5f, slam);
            SetBars(bar);
            Color team = p.jerseyColor;
            team.a = 1f;
            SetEdges(bar, team);
            SetLines(0.35f, -dir * Time.time * 3.2f); // uv scrolling back = streaks rushing forward
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

        // ------------------------------------------------------------------ framing helpers

        void Place(Vector3 pos, Vector3 look, float roll, float fov)
        {
            Vector3 d = look - pos;
            if (d.sqrMagnitude < 1e-6f) d = Vector3.forward;
            Quaternion rot = Quaternion.LookRotation(d, Vector3.up) * Quaternion.Euler(0f, 0f, roll);
            _cam.transform.SetPositionAndRotation(pos, rot);
            _cam.fieldOfView = fov;
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
