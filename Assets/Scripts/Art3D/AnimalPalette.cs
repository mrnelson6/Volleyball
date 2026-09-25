using System.Collections.Generic;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Builds the 16x1 palette strip a generated 3D animal samples (see the Volleyball/Stylized
    /// shader and Tools/blender/animal_gen.py — every face's UVs point at one slot). The slot
    /// layout MUST match SLOT_* in animal_gen.py. One tiny cached texture per
    /// (character, jersey) pair, so jerseys recolour at runtime without new meshes.
    /// </summary>
    public static class AnimalPalette
    {
        public const int Slots = 16;
        public const int Fur = 0, Accent = 1, Jersey = 2, Marking = 3, Nose = 4, EyeWhite = 5,
                         EyeDark = 6, Shorts = 7, Horn = 8, Trim = 9;

        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(CharacterDef ch, Color jersey)
        {
            string key = ch.id + "#" + ColorUtility.ToHtmlStringRGB(jersey);
            if (Cache.TryGetValue(key, out var tex) && tex != null) return tex;

            var px = new Color[Slots];
            for (int i = 0; i < Slots; i++) px[i] = Color.magenta; // unused slots stand out
            px[Fur] = ch.fur;
            px[Accent] = ch.furAccent;
            px[Jersey] = jersey;
            px[Marking] = ch.art.markingColor;
            px[Nose] = ch.art.noseColor;
            px[EyeWhite] = new Color(0.97f, 0.97f, 0.97f);
            px[EyeDark] = new Color(0.06f, 0.05f, 0.06f);
            px[Shorts] = new Color(jersey.r * 0.35f + 0.05f, jersey.g * 0.35f + 0.05f, jersey.b * 0.35f + 0.05f);
            px[Horn] = ch.art.horns == HornStyle.Antlers ? new Color(0.62f, 0.50f, 0.36f) : new Color(0.92f, 0.87f, 0.74f);
            px[Trim] = new Color(0.97f, 0.97f, 0.95f);

            // sRGB texture: palette values are authored gamma-space, like every Color in CharacterDef
            tex = new Texture2D(Slots, 1, TextureFormat.RGBA32, false, false)
            {
                name = "Palette_" + key,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            tex.SetPixels(px);
            tex.Apply(false, false);
            Cache[key] = tex;
            return tex;
        }
    }
}
