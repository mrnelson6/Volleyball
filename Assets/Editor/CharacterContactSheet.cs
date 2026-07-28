using System.IO;
using UnityEditor;
using UnityEngine;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// Dev utility: tiles a few key frames (idle / swing / dive-toward) of every roster animal
    /// into one contact-sheet PNG at the project root, for a quick visual pass over the whole
    /// cast without opening a scene. Bakes in memory — the Resources cache is untouched.
    /// </summary>
    public static class CharacterContactSheet
    {
        const int Cols = 7;
        const int CellW = CharacterSprites.BaseCanvasWidth + 12;
        const int CellH = 96; // tallest canvas is 64 * 1.30 ≈ 83

        [MenuItem("Volleyball/Save Character Contact Sheet", priority = 27)]
        public static void Save()
        {
            int[] frames = { 0, 4, 10 }; // idle, swing, diveDown (face toward camera)
            var roster = CharacterRoster.All;
            int rows = Mathf.CeilToInt(roster.Length / (float)Cols) * frames.Length;

            var sheet = new Texture2D(Cols * CellW, rows * CellH, TextureFormat.RGBA32, false);
            var bg = new Color(0.20f, 0.45f, 0.55f, 1f);
            var px = new Color[sheet.width * sheet.height];
            for (int i = 0; i < px.Length; i++) px[i] = bg;
            sheet.SetPixels(px);

            int animalRows = Mathf.CeilToInt(roster.Length / (float)Cols);
            for (int f = 0; f < frames.Length; f++)
                for (int i = 0; i < roster.Length; i++)
                {
                    var tex = CharacterArt.BakeFrameTexture(frames[f], PlayerColors.Human, roster[i]);
                    int cx = (i % Cols) * CellW + (CellW - tex.width) / 2;
                    int rowFromTop = f * animalRows + i / Cols;
                    int cy = sheet.height - (rowFromTop + 1) * CellH; // top-to-bottom, roster order
                    for (int y = 0; y < tex.height; y++)
                        for (int x = 0; x < tex.width; x++)
                        {
                            Color c = tex.GetPixel(x, y);
                            if (c.a > 0.5f) sheet.SetPixel(cx + x, cy + y, c);
                        }
                    Object.DestroyImmediate(tex);
                }

            sheet.Apply();
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                                       "character_sheet.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Debug.Log("[Volleyball] Character contact sheet saved to " + path);
        }
    }
}
