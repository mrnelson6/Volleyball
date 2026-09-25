using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// Shared look + layout rules for every generated UI canvas (menu, HUD, prototype).
    ///
    /// Scaling: canvases use <see cref="CanvasScaler.ScreenMatchMode.Expand"/> against a 1920x1080
    /// reference. Expand guarantees the whole 1920x1080 design area is on screen at ANY aspect
    /// ratio — ultrawide monitors, 20:9 phones in landscape, 4:3 tablets, odd browser windows —
    /// with extra screen space becoming margin. (The old 50/50 width/height match shrank the
    /// canvas below 1080 units tall on anything wider than 16:9, pushing bottom-row buttons off
    /// screen.) Content sits under a <see cref="SafeArea"/> root so notches and rounded phone
    /// corners can't clip it either.
    ///
    /// Fonts: Lilita One (body/buttons, OFL) and Luckiest Guy (logo, Apache 2.0), both from
    /// Google Fonts, licences alongside in Assets/Fonts. Falls back to Unity's built-in font.
    /// </summary>
    public static class UIStyle
    {
        public static readonly Vector2 Reference = new Vector2(1920f, 1080f);

        public static Font Body => Load("Assets/Fonts/LilitaOne-Regular.ttf");
        public static Font Title => Load("Assets/Fonts/LuckiestGuy-Regular.ttf");

        static Font Load(string path)
        {
            var f = AssetDatabase.LoadAssetAtPath<Font>(path);
            return f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public static void ConfigureScaler(CanvasScaler scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }

        /// <summary>A full-canvas child fitted to the device safe area; build UI under it.</summary>
        public static Transform SafeRoot(Transform canvas)
        {
            var go = new GameObject("Safe Area", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.AddComponent<SafeArea>();
            return go.transform;
        }

        /// <summary>Dark outline + soft drop shadow: text that reads over any backdrop.</summary>
        public static void Pop(Graphic g, float outline = 2.5f, Color? outlineColor = null)
        {
            var o = g.gameObject.AddComponent<Outline>();
            o.effectColor = outlineColor ?? new Color(0.10f, 0.08f, 0.14f, 0.85f);
            o.effectDistance = new Vector2(outline, -outline);
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.35f);
            s.effectDistance = new Vector2(0f, -outline * 2f);
        }
    }
}
