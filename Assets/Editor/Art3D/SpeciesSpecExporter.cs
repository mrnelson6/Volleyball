using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// Dumps the roster's body parameters (<see cref="CharacterDef.height"/> and
    /// <see cref="SpeciesArt"/>) plus preview colours to <c>Tools/blender/species.json</c>, which
    /// the Blender animal generator (<c>Tools/blender/animal_gen.py</c>) reads. CharacterDef stays
    /// the single source of truth: tweak an animal there, re-export, regenerate the model.
    /// Colours are only for Blender's preview renders — in game the palette is applied at
    /// runtime by <c>AnimalPalette</c>, so jerseys can change.
    /// </summary>
    public static class SpeciesSpecExporter
    {
        public static string OutputPath =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Tools", "blender", "species.json");

        [MenuItem("Volleyball/3D/Export Species Specs for Blender", priority = 200)]
        public static void Export()
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"species\": [\n");
            var all = CharacterRoster.All;
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                var a = c.art;
                sb.Append("    {");
                sb.Append($"\"id\": \"{c.id}\", \"height\": {F(c.height)}, ");
                sb.Append($"\"head\": \"{a.head}\", \"ears\": \"{a.ears}\", \"horns\": \"{a.horns}\", ");
                sb.Append($"\"neck\": {F(a.neck)}, \"tail\": {F(a.tail)}, \"markings\": \"{a.markings}\", ");
                sb.Append($"\"fur\": {C(c.fur)}, \"accent\": {C(c.furAccent)}, ");
                sb.Append($"\"marking\": {C(a.markingColor)}, \"nose\": {C(a.noseColor)}");
                sb.Append(i < all.Length - 1 ? "},\n" : "}\n");
            }
            sb.Append("  ]\n}\n");

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllText(OutputPath, sb.ToString());
            Debug.Log($"[Volleyball] Exported {all.Length} species specs to {OutputPath}");
        }

        static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        // sRGB (gamma) values exactly as authored in CharacterDef
        static string C(Color c) => $"[{F(c.r)}, {F(c.g)}, {F(c.b)}]";
    }
}
