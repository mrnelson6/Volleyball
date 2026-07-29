using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Volleyball
{
    /// <summary>
    /// The on-screen chat controls: a compact row along the bottom of the screen — the two
    /// gameplay callouts plus a fold-out strip of emoji emotes. Every widget is built here at runtime
    /// (from <see cref="ChatArt"/>) rather than by the scene builders, so chat works in all
    /// fifteen baked arenas without regenerating any of them — the same trick the power-up glow
    /// uses. Clicking a button and pressing its hotkey are the same act: both go through
    /// <see cref="GameInput"/>, so the command stream carries exactly one kind of chat press.
    /// </summary>
    public class ChatBar : MonoBehaviour
    {
        GameObject _emotePanel;

        /// <summary>Touch has no keyboard, so the "(Z)" style hints are desktop-only.</summary>
        static bool ShowKeyHints => Touchscreen.current == null && !Application.isMobilePlatform;

        void Start()
        {
            if (!ChatArt.CanRender) { enabled = false; return; } // headless server: no HUD at all
            if (GetComponentInParent<Canvas>() == null) { enabled = false; return; }
            Build();
        }

        /// <summary>An emote was picked — fold the strip back up so it stays out of the way.</summary>
        internal void OnEmotePicked()
        {
            if (_emotePanel != null) _emotePanel.SetActive(false);
        }

        // one compact row: [I GOT IT] [YOU GOT IT] [:)], with the emote strip popping up above it
        static readonly Vector2 CallSize = new Vector2(196f, 54f);
        static readonly Vector2 ToggleSize = new Vector2(84f, 54f);
        const float Gap = 8f;
        const float EmoteCell = 64f;

        void Build()
        {
            // Bottom centre, just above the power meter: the one strip of screen that is free on
            // desktop AND on touch (clear of the joystick at bottom-left and the action cluster
            // at bottom-right).
            var root = NewRect("Chat Bar", transform, new Vector2(560f, 200f), new Vector2(0f, 108f));
            // built last, so drop to the bottom of the canvas stack: the pause overlay and the
            // touch controls must still draw (and take clicks) over the bar
            root.SetAsFirstSibling();

            float rowWidth = CallSize.x * 2f + ToggleSize.x + Gap * 2f;
            float x = -rowWidth * 0.5f;

            // --- the two calls the AI listens to ---
            foreach (var call in new[] { ChatCall.IGotIt, ChatCall.YouGotIt })
            {
                MakeCallButton(root, ChatCalls.Get(call), CallSize, new Vector2(x + CallSize.x * 0.5f, 0f));
                x += CallSize.x + Gap;
            }

            // --- the emote strip: one row, folded away behind a toggle ---
            int count = ChatCalls.All.Length - 2; // minus the two callouts above
            float stripWidth = count * EmoteCell + (count - 1) * Gap;
            _emotePanel = NewRect("Emotes", root, new Vector2(stripWidth + 12f, EmoteCell + 12f),
                                  new Vector2(0f, CallSize.y + Gap)).gameObject;
            var bg = _emotePanel.AddComponent<Image>();
            bg.sprite = ChatArt.Panel;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.07f, 0.09f, 0.13f, 0.55f);

            int i = 0;
            foreach (var def in ChatCalls.All)
            {
                if (def.isTeamCall) continue; // the two callouts have their own buttons
                float cx = (i - (count - 1) * 0.5f) * (EmoteCell + Gap);
                MakeEmoteButton(_emotePanel.transform, def,
                                new Vector2(EmoteCell, EmoteCell), new Vector2(cx, 0f));
                i++;
            }
            _emotePanel.SetActive(false);

            GameObject toggle = MakeButton(root, "EmoteToggle", ":)", ToggleSize,
                new Vector2(x + ToggleSize.x * 0.5f, 0f),
                new Color(0.24f, 0.26f, 0.34f, 0.8f), 26);
            var tb = toggle.AddComponent<ChatButton>();
            tb.togglePanel = _emotePanel;
        }

        void MakeCallButton(Transform parent, in ChatCallDef def, Vector2 size, Vector2 pos)
        {
            string hint = ChatCalls.KeyHint(def.call);
            string label = ShowKeyHints && hint != "" ? $"{def.buttonLabel}  ({hint})" : def.buttonLabel;
            Color bg = def.color;
            bg.a = 0.85f;

            GameObject go = MakeButton(parent, def.call + "Button", label, size, pos, bg, 22);
            var cb = go.AddComponent<ChatButton>();
            cb.call = def.call;
            cb.bar = this;
        }

        void MakeEmoteButton(Transform parent, in ChatCallDef def, Vector2 size, Vector2 pos)
        {
            GameObject go = MakeButton(parent, def.call + "Button", "", size, pos,
                                       new Color(1f, 1f, 1f, 0.14f), 0, Centre);

            // the emoji itself, with its hotkey digit tucked into the corner on desktop
            var face = NewRect("Face", go.transform, size * 0.78f, Vector2.zero, Centre);
            var img = face.gameObject.AddComponent<Image>();
            img.sprite = ChatArt.Face(def.face);
            img.raycastTarget = false;

            if (!ShowKeyHints) return;
            Text hint = MakeText(go.transform, "Key", ChatCalls.KeyHint(def.call), 16);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax =
                hint.rectTransform.pivot = new Vector2(1f, 0f);
            hint.rectTransform.sizeDelta = new Vector2(20f, 20f);
            hint.rectTransform.anchoredPosition = new Vector2(-2f, 1f);
            hint.alignment = TextAnchor.LowerRight;
            hint.color = new Color(1f, 1f, 1f, 0.7f);
        }

        // ----------------------------------------------------------------- widget helpers

        GameObject MakeButton(Transform parent, string name, string label, Vector2 size,
                              Vector2 pos, Color color, int fontSize, Vector2? anchor = null)
        {
            RectTransform rt = NewRect(name, parent, size, pos, anchor ?? BottomCentre);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = ChatArt.Panel;
            img.type = Image.Type.Sliced;
            img.color = color;

            if (!string.IsNullOrEmpty(label))
            {
                Text t = MakeText(rt, "Label", label, fontSize);
                t.rectTransform.anchorMin = t.rectTransform.anchorMax =
                    t.rectTransform.pivot = Centre;
                t.rectTransform.sizeDelta = size;
                t.rectTransform.anchoredPosition = Vector2.zero;
            }
            return rt.gameObject;
        }

        static Text MakeText(Transform parent, string name, string content, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = ChatArt.UIFont();
            t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = content;
            return t;
        }

        // the bar sits on the screen's bottom edge; cells inside a strip centre on their panel
        static readonly Vector2 BottomCentre = new Vector2(0.5f, 0f);
        static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);

        static RectTransform NewRect(string name, Transform parent, Vector2 size, Vector2 pos)
            => NewRect(name, parent, size, pos, BottomCentre);

        static RectTransform NewRect(string name, Transform parent, Vector2 size, Vector2 pos,
                                     Vector2 anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }
    }

    /// <summary>
    /// One chat widget's click behaviour: either it sends a <see cref="ChatCall"/> (through
    /// <see cref="GameInput"/>, exactly as the hotkey does) or it folds a panel open/shut.
    /// Handling the pointer itself — rather than a serialized Button callback — is what lets the
    /// bar be assembled in code, the same approach <see cref="VirtualButton"/> takes.
    /// </summary>
    public class ChatButton : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        public ChatCall call;
        public GameObject togglePanel;
        public ChatBar bar;

        Image _img;
        Color _idle;

        void Awake()
        {
            _img = GetComponent<Image>();
            if (_img != null) _idle = _img.color;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (_img != null) _img.color = _idle * new Color(0.75f, 0.75f, 0.75f, 1.2f);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (_img != null) _img.color = _idle;
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (togglePanel != null)
            {
                togglePanel.SetActive(!togglePanel.activeSelf);
                return;
            }
            GameInput.Instance?.RequestChat(call);
            if (bar != null) bar.OnEmotePicked();
        }
    }
}
