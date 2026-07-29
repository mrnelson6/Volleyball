using UnityEngine;
using UnityEngine.UI;

namespace Volleyball
{
    /// <summary>
    /// The chat feature's artwork, generated in code at runtime — same spirit as the baked
    /// character sprites and the synthesised audio, so chat ships no assets and needs no scene
    /// rebuild. Two things: a 9-sliceable rounded panel (speech bubbles and buttons) and a
    /// little emoji face per <see cref="ChatFace"/>. Everything is cached for the session.
    /// </summary>
    public static class ChatArt
    {
        const int PanelSize = 64;
        const int PanelRadius = 18;
        const int PanelBorder = 20;  // 9-slice inset, must stay under PanelSize/2
        const int FaceSize = 96;

        static Sprite _panel;
        static Font _font;
        static readonly Sprite[] _faces = new Sprite[8];

        /// <summary>
        /// False on a dedicated server or a <c>-nographics</c> run: there is no screen and no
        /// graphics device, so nothing should generate textures or build widgets. Every runtime
        /// UI that leans on this class checks it first.
        /// </summary>
        public static bool CanRender
            => SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;

        /// <summary>White rounded-rect panel, 9-sliced (tint it per use).</summary>
        public static Sprite Panel
        {
            get
            {
                if (_panel != null) return _panel;
                var p = new Painter(PanelSize, PanelSize);
                p.RoundRect(PanelRadius, Color.white);
                _panel = p.ToSprite("chat_panel",
                    new Vector4(PanelBorder, PanelBorder, PanelBorder, PanelBorder));
                return _panel;
            }
        }

        /// <summary>The emoji face for an emote, or null for <see cref="ChatFace.None"/>.</summary>
        public static Sprite Face(ChatFace kind)
        {
            int i = (int)kind;
            if (kind == ChatFace.None || i >= _faces.Length) return null;
            if (_faces[i] != null) return _faces[i];
            _faces[i] = BuildFace(kind);
            return _faces[i];
        }

        /// <summary>
        /// A font that is guaranteed to exist in a player build. The built-in legacy font is
        /// the one every generated HUD uses; if a platform ever refuses it we borrow the font
        /// off the live HUD rather than render nothing.
        /// </summary>
        public static Font UIFont()
        {
            if (_font != null) return _font;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null)
                foreach (var t in Object.FindObjectsByType<Text>(FindObjectsSortMode.None))
                    if (t.font != null) { _font = t.font; break; }
            return _font;
        }

        // ----------------------------------------------------------------- faces

        static readonly Color Skin = new Color(1f, 0.80f, 0.20f);
        static readonly Color Rim = new Color(0.85f, 0.60f, 0.10f);
        static readonly Color Ink = new Color(0.16f, 0.12f, 0.05f);

        static Sprite BuildFace(ChatFace kind)
        {
            const float S = FaceSize;
            var p = new Painter(FaceSize, FaceSize);

            // the head: a rimmed disc
            p.Disc(S * 0.5f, S * 0.5f, S * 0.48f, Rim);
            p.Disc(S * 0.5f, S * 0.5f, S * 0.455f, Skin);

            float eyeY = S * 0.62f;
            float eyeL = S * 0.34f, eyeR = S * 0.66f;
            float eyeR0 = S * 0.055f;

            switch (kind)
            {
                case ChatFace.Smile:
                    p.Disc(eyeL, eyeY, eyeR0, Ink);
                    p.Disc(eyeR, eyeY, eyeR0, Ink);
                    p.Arc(S * 0.5f, S * 0.52f, S * 0.24f, S * 0.055f, 205f, 335f, Ink);
                    break;

                case ChatFace.Sad:
                    p.Disc(eyeL, eyeY, eyeR0, Ink);
                    p.Disc(eyeR, eyeY, eyeR0, Ink);
                    p.Arc(S * 0.5f, S * 0.20f, S * 0.24f, S * 0.055f, 25f, 155f, Ink);
                    break;

                case ChatFace.Shocked:
                    p.Disc(eyeL, eyeY + S * 0.02f, eyeR0 * 1.35f, Ink);
                    p.Disc(eyeR, eyeY + S * 0.02f, eyeR0 * 1.35f, Ink);
                    p.Disc(S * 0.5f, S * 0.33f, S * 0.10f, Ink);
                    break;

                case ChatFace.Cool: // shades on, quiet little smirk
                    p.Bar(S * 0.16f, eyeY - S * 0.085f, S * 0.84f, eyeY + S * 0.085f, Ink);
                    p.Bar(S * 0.47f, eyeY - S * 0.02f, S * 0.53f, eyeY + S * 0.085f, Skin); // lens bridge
                    p.Arc(S * 0.5f, S * 0.50f, S * 0.20f, S * 0.05f, 235f, 320f, Ink);
                    break;

                case ChatFace.Laugh: // squeezed-shut eyes, wide open mouth
                    p.Arc(eyeL, eyeY - S * 0.03f, S * 0.075f, S * 0.045f, 30f, 150f, Ink);
                    p.Arc(eyeR, eyeY - S * 0.03f, S * 0.075f, S * 0.045f, 30f, 150f, Ink);
                    p.HalfDisc(S * 0.5f, S * 0.42f, S * 0.22f, Ink);
                    break;

                case ChatFace.Wink:
                    p.Disc(eyeL, eyeY, eyeR0, Ink);
                    p.Bar(eyeR - S * 0.075f, eyeY - S * 0.022f, eyeR + S * 0.075f, eyeY + S * 0.022f, Ink);
                    p.Arc(S * 0.5f, S * 0.52f, S * 0.24f, S * 0.055f, 210f, 330f, Ink);
                    break;
            }

            return p.ToSprite("chat_face_" + kind, Vector4.zero);
        }

        // ----------------------------------------------------------------- painter

        /// <summary>Tiny software rasteriser with 1px-feather anti-aliasing. Origin bottom-left,
        /// which is how Texture2D pixels (and therefore sprites) are indexed.</summary>
        class Painter
        {
            readonly Color[] _d;
            readonly int _w, _h;

            public Painter(int w, int h)
            {
                _w = w;
                _h = h;
                _d = new Color[w * h];
                for (int i = 0; i < _d.Length; i++) _d[i] = new Color(1f, 1f, 1f, 0f);
            }

            void Blend(int x, int y, Color c, float a)
            {
                if (a <= 0f || x < 0 || y < 0 || x >= _w || y >= _h) return;
                a = Mathf.Clamp01(a);
                int i = y * _w + x;
                Color dst = _d[i];
                float outA = a + dst.a * (1f - a);
                if (outA <= 0f) { _d[i] = new Color(1f, 1f, 1f, 0f); return; }
                Color rgb = (c * a + dst * dst.a * (1f - a)) / outA;
                _d[i] = new Color(rgb.r, rgb.g, rgb.b, outA);
            }

            /// <summary>Coverage of a pixel by a half-plane <paramref name="signedDist"/> units
            /// inside the shape (positive = inside), feathered over one pixel.</summary>
            static float Cover(float signedDist) => Mathf.Clamp01(signedDist + 0.5f);

            public void Disc(float cx, float cy, float r, Color c)
            {
                int x0 = Mathf.FloorToInt(cx - r - 1), x1 = Mathf.CeilToInt(cx + r + 1);
                int y0 = Mathf.FloorToInt(cy - r - 1), y1 = Mathf.CeilToInt(cy + r + 1);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx)
                                             + (y + 0.5f - cy) * (y + 0.5f - cy));
                        Blend(x, y, c, Cover(r - d));
                    }
            }

            /// <summary>Filled lower half of a disc — an open mouth.</summary>
            public void HalfDisc(float cx, float cy, float r, Color c)
            {
                int x0 = Mathf.FloorToInt(cx - r - 1), x1 = Mathf.CeilToInt(cx + r + 1);
                int y0 = Mathf.FloorToInt(cy - r - 1), y1 = Mathf.CeilToInt(cy + 1);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float py = y + 0.5f;
                        float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (py - cy) * (py - cy));
                        Blend(x, y, c, Mathf.Min(Cover(r - d), Cover(cy - py)));
                    }
            }

            /// <summary>Stroked arc between two angles (degrees, CCW from +x).</summary>
            public void Arc(float cx, float cy, float r, float thick, float from, float to, Color c)
            {
                float half = thick * 0.5f;
                int x0 = Mathf.FloorToInt(cx - r - thick), x1 = Mathf.CeilToInt(cx + r + thick);
                int y0 = Mathf.FloorToInt(cy - r - thick), y1 = Mathf.CeilToInt(cy + r + thick);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                        float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                        if (ang < 0f) ang += 360f;
                        if (ang < from || ang > to) continue;
                        float d = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - r);
                        Blend(x, y, c, Cover(half - d));
                    }
            }

            public void Bar(float x0, float y0, float x1, float y1, Color c)
            {
                for (int y = Mathf.FloorToInt(y0) - 1; y <= Mathf.CeilToInt(y1) + 1; y++)
                    for (int x = Mathf.FloorToInt(x0) - 1; x <= Mathf.CeilToInt(x1) + 1; x++)
                    {
                        float px = x + 0.5f, py = y + 0.5f;
                        float a = Mathf.Min(Mathf.Min(Cover(px - x0), Cover(x1 - px)),
                                            Mathf.Min(Cover(py - y0), Cover(y1 - py)));
                        Blend(x, y, c, a);
                    }
            }

            /// <summary>Fill the whole canvas with a rounded rectangle.</summary>
            public void RoundRect(float radius, Color c)
            {
                for (int y = 0; y < _h; y++)
                    for (int x = 0; x < _w; x++)
                    {
                        float px = x + 0.5f, py = y + 0.5f;
                        // rounded-box SDF: distance to the radius-inset rectangle, minus the radius
                        float ax = Mathf.Abs(px - _w * 0.5f) - (_w * 0.5f - radius);
                        float ay = Mathf.Abs(py - _h * 0.5f) - (_h * 0.5f - radius);
                        float qx = Mathf.Max(ax, 0f), qy = Mathf.Max(ay, 0f);
                        float d = Mathf.Sqrt(qx * qx + qy * qy) - radius; // < 0 inside
                        Blend(x, y, c, Cover(-d));
                    }
            }

            public Sprite ToSprite(string name, Vector4 border)
            {
                var tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false)
                {
                    name = name,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                tex.SetPixels(_d);
                tex.Apply();

                Sprite s = Sprite.Create(tex, new Rect(0f, 0f, _w, _h), new Vector2(0.5f, 0.5f),
                                         100f, 0, SpriteMeshType.FullRect, border);
                s.name = name;
                s.hideFlags = HideFlags.HideAndDontSave;
                return s;
            }
        }
    }
}
