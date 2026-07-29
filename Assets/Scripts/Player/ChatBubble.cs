using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// The speech bubble over a player's head. Added on demand by <see cref="ChatDirector"/>
    /// (like <see cref="PowerUpGlow"/>) and builds its own billboarded panel + caption + emoji
    /// face out of <see cref="ChatArt"/>, so no baked arena scene needs rebuilding for chat.
    ///
    /// Pure view: it reads nothing the simulation owns, and its clocks are wall-clock
    /// (<c>Time.time</c>) because it animates per rendered frame, not per 50Hz tick.
    /// </summary>
    public class ChatBubble : MonoBehaviour
    {
        const float Life = 1.5f;      // seconds a call stays up
        const float FadeTime = 0.35f; // tail of Life spent fading out
        const float PopTime = 0.11f;  // scale-up on appearance

        VolleyPlayer _player;
        Transform _root;
        SpriteRenderer _panel;
        SpriteRenderer _face;
        TextMesh _text;
        MeshRenderer _textRenderer;

        float _shownAt = -99f;
        Color _panelColor = Color.white;
        Color _textColor = Color.white;
        bool _needsFit;

        void Awake()
        {
            _player = GetComponent<VolleyPlayer>();
            Build();
            _root.gameObject.SetActive(false);
        }

        /// <summary>Say something: swap in this call's look and restart the pop/fade.</summary>
        public void Say(ChatCall call)
        {
            if (_root == null) return;
            ChatCallDef def = ChatCalls.Get(call);
            if (def.call == ChatCall.None) return;

            bool words = !string.IsNullOrEmpty(def.bubbleText);
            Sprite face = words ? null : ChatArt.Face(def.face);

            _text.text = words ? def.bubbleText : "";
            _textRenderer.enabled = words;
            _face.enabled = face != null;
            _face.sprite = face;

            if (words)
            {
                // a shout: the call's own colour, darkened so white lettering reads on it
                _panelColor = Color.Lerp(def.color, Color.black, 0.38f);
                _panelColor.a = 0.92f;
                _textColor = Color.white;
                SetPanelSize(0.34f + def.bubbleText.Length * 0.16f, 0.56f);
                _needsFit = true; // exact size once the text mesh has been generated
            }
            else
            {
                _panelColor = new Color(0.97f, 0.97f, 1f, 0.92f);
                SetPanelSize(0.8f, 0.8f);
                _needsFit = false;
            }

            // sit clear of the tallest head this character has
            float headY = 1.9f * (_player != null ? _player.Character.height : 1f) + 0.5f;
            _root.localPosition = new Vector3(0f, headY, 0f);

            _shownAt = Time.time;
            _root.gameObject.SetActive(true);
            Apply(0f);
        }

        void LateUpdate()
        {
            if (_root == null || !_root.gameObject.activeSelf) return;
            if (_player == null) _player = GetComponent<VolleyPlayer>(); // survives a controller swap

            float age = Time.time - _shownAt;
            if (age >= Life) { _root.gameObject.SetActive(false); return; }

            if (_needsFit) FitToText();
            Apply(age);
        }

        /// <summary>Pop in, hold, fade out.</summary>
        void Apply(float age)
        {
            float grow = PopTime > 0f ? Mathf.Clamp01(age / PopTime) : 1f;
            float scale = Mathf.Lerp(0.55f, 1f, grow) * (1f + 0.12f * Mathf.Sin(grow * Mathf.PI));
            _root.localScale = new Vector3(scale, scale, 1f);

            float fade = Mathf.Clamp01((Life - age) / FadeTime);
            _panel.color = new Color(_panelColor.r, _panelColor.g, _panelColor.b, _panelColor.a * fade);
            _text.color = new Color(_textColor.r, _textColor.g, _textColor.b, fade);
            if (_face.enabled) _face.color = new Color(1f, 1f, 1f, fade);
        }

        /// <summary>Size the panel to the caption once Unity has actually laid the glyphs out
        /// (the mesh doesn't exist on the frame the text is assigned).</summary>
        void FitToText()
        {
            Vector3 size = _textRenderer.localBounds.size;
            if (size.x <= 0.01f) return; // not generated yet — keep the estimate one more frame
            SetPanelSize(size.x + 0.30f, Mathf.Max(0.5f, size.y + 0.24f));
            _needsFit = false;
        }

        void SetPanelSize(float w, float h)
        {
            _panel.size = new Vector2(w, h);
            _face.transform.localScale = Vector3.one * (Mathf.Min(w, h) * 0.78f);
        }

        // ----------------------------------------------------------------- construction

        void Build()
        {
            var rootGO = new GameObject("Chat Bubble");
            _root = rootGO.transform;
            _root.SetParent(transform, false);
            rootGO.AddComponent<BillboardSprite>().yAxisOnly = true;

            var panelGO = new GameObject("Panel");
            panelGO.transform.SetParent(_root, false);
            _panel = panelGO.AddComponent<SpriteRenderer>();
            _panel.sprite = ChatArt.Panel;
            _panel.drawMode = SpriteDrawMode.Sliced;
            _panel.size = new Vector2(1.4f, 0.56f);
            _panel.sortingOrder = 20;

            // the face sprite is 1 world unit wide at 100 PPU, so localScale IS its size
            var faceGO = new GameObject("Face");
            faceGO.transform.SetParent(_root, false);
            faceGO.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            _face = faceGO.AddComponent<SpriteRenderer>();
            _face.sortingOrder = 21;
            _face.enabled = false;

            var textGO = new GameObject("Caption");
            textGO.transform.SetParent(_root, false);
            textGO.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            _text = textGO.AddComponent<TextMesh>();
            _text.font = ChatArt.UIFont();
            _text.fontSize = 60;
            _text.fontStyle = FontStyle.Bold;
            _text.characterSize = 0.05f;
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            _text.color = Color.white;
            _textRenderer = textGO.GetComponent<MeshRenderer>();
            if (_text.font != null) _textRenderer.sharedMaterial = _text.font.material;
            _textRenderer.sortingOrder = 21;
        }
    }
}
