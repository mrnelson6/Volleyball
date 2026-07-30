using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// Hand-drawn character pipeline: exports a single template PNG an artist can draw on in any
    /// pixel editor, and imports the filled-in sheet back as a complete frame set.
    ///
    /// The round trip is designed so the artist needs to know nothing about the project:
    /// <list type="bullet">
    /// <item>One PNG, two grids. The top grid is a finished character (the procedural fox, baked
    /// with a magenta shirt) showing every pose that's needed; the bottom grid is where they
    /// draw. Only the bottom grid is ever read back.</item>
    /// <item>Labels, borders and titles live in the margins, never inside a drawing cell.</item>
    /// <item>Guides inside the drawing cells (pose silhouette, ground line, centre line) are
    /// stripped on import by exact colour match, so the artist draws straight over them.</item>
    /// <item>The shirt is drawn in magenta and recoloured per team at import (shading preserved),
    /// so one drawing yields all four jersey colours — the game bakes jersey colour into the
    /// frames, and 2v2 is unreadable if both sides look alike.</item>
    /// </list>
    ///
    /// <b>Resolution.</b> The procedural art is 48x64 at 34 px/unit, which is uncomfortably small
    /// to draw by hand. The whole sheet layout is therefore scaled by an integer
    /// <c>detail</c> factor, and imported sprites get <c>34 * detail</c> pixels-per-unit — a
    /// 192x256 sprite at 136 px/unit fills exactly the same world space as 48x64 at 34, so extra
    /// detail costs nothing elsewhere in the game. The sheet's width is always
    /// <c>222 * detail</c>, which is how the importer recovers the factor without being told.
    ///
    /// Output goes to <see cref="CharacterSprites.CustomResourceFolder"/>, not the procedural bake
    /// cache, so <c>Force Rebake Character Sprites</c> can never delete an artist's work.
    /// </summary>
    public static class CustomCharacterSheet
    {
        // ---------------------------------------------------------------- sheet geometry
        // All in base (1x) units; the finished sheet is nearest-neighbour upscaled by `detail`.
        // The only contract with the importer is that a drawing cell is W x cellH at these
        // offsets. Changing a constant invalidates template PNGs already in an artist's hands
        // (the importer rejects them with the expected size), so bump deliberately.

        const int Cols = 4;
        const int Margin = 6;
        const int Gutter = 6;
        const int LabelH = 7;     // 5px glyph + 2px breathing room
        const int HeaderH = 24;   // three lines of instructions at the top
        const int TitleH = 7;     // one line naming each grid
        const int SectionGap = 10;

        /// <summary>Largest detail factor offered/accepted. 8x is a 1776px-wide sheet.</summary>
        const int MaxDetail = 8;

        static int W => CharacterSprites.BaseCanvasWidth;
        static int BaseH => CharacterSprites.BaseCanvasHeight;
        static int FrameCount => CharacterSprites.FrameNames.Length;
        static int Rows => Mathf.CeilToInt(FrameCount / (float)Cols);

        public static string CustomDir => "Assets/Resources/" + CharacterSprites.CustomResourceFolder;

        static int SheetWBase => Margin * 2 + Cols * W + (Cols - 1) * Gutter;
        static int GridH(int cellH) => Rows * (LabelH + cellH) + (Rows - 1) * Gutter;
        static int SectionH(int cellH) => TitleH + GridH(cellH);
        static int SheetHBase(int cellH) => Margin * 2 + HeaderH + 2 * SectionH(cellH) + SectionGap;

        /// <summary>Inverse of <see cref="SheetHBase"/>. Callers must confirm
        /// <c>SheetHBase(result) == sheetHeight</c> — a foreign sheet can land on a plausible
        /// quotient.</summary>
        static int CellHeightFor(int sheetHeight) =>
            (sheetHeight - (Margin * 2 + HeaderH + SectionGap + 2 * TitleH
                            + 2 * (Rows - 1) * Gutter + 2 * Rows * LabelH)) / (2 * Rows);

        static int ExampleGridTop => Margin + HeaderH + TitleH;
        static int DrawGridTop(int cellH) => ExampleGridTop + GridH(cellH) + SectionGap + TitleH;

        /// <summary>Cell interior for frame <paramref name="i"/> of a grid, in top-down coords.</summary>
        static RectInt Cell(int i, int cellH, int gridTop) => new RectInt(
            Margin + (i % Cols) * (W + Gutter),
            gridTop + (i / Cols) * (LabelH + cellH + Gutter) + LabelH,
            W, cellH);

        /// <summary>Where the template lands: project root, beside the contact sheet.</summary>
        public static string TemplatePath(int cellH, int detail) => Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            $"character_template_{W * detail}x{cellH * detail}.png");

        // ---------------------------------------------------------------- palette

        static readonly Color32 Bg = new Color32(26, 30, 38, 255);
        static readonly Color32 PanelCol = new Color32(40, 47, 58, 255);  // example cell backing
        static readonly Color32 BorderCol = new Color32(74, 88, 106, 255);
        static readonly Color32 LabelCol = new Color32(226, 234, 242, 255);
        static readonly Color32 HeaderCol = new Color32(150, 205, 235, 255);
        static readonly Color32 GuideCol = new Color32(120, 170, 210, 90);
        static readonly Color32 GroundCol = new Color32(240, 205, 115, 110);
        static readonly Color32 JerseyKey = new Color32(255, 0, 255, 255);

        /// <summary>Colours the importer strips out of a drawing cell. Anything the artist draws
        /// that happens to match exactly is collateral — these are deliberately odd values.</summary>
        static readonly Color32[] GuideKeys = { GuideCol, GroundCol };

        // ---------------------------------------------------------------- template export

        [MenuItem("Volleyball/Custom Characters/Save Template Sheet", priority = 40)]
        static void SaveTemplateMenu()
        {
            string path = SaveTemplate(1f, 4);
            Debug.Log($"[Volleyball] Character template sheet saved to {path}");
            EditorUtility.RevealInFinder(path);
        }

        /// <summary>
        /// Write the drawing template for a character of stat height <paramref name="heightStat"/>
        /// (1 = baseline; the cell grows taller with the stat, as the baked canvas does), at
        /// <paramref name="detail"/>x the game's native pixel density. Returns the absolute path.
        /// </summary>
        public static string SaveTemplate(float heightStat, int detail)
        {
            detail = Mathf.Clamp(detail, 1, MaxDetail);
            int cellH = Mathf.RoundToInt(BaseH * Mathf.Clamp(heightStat, 0.6f, 1.6f));
            int w = SheetWBase, h = SheetHBase(cellH);

            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = Bg;

            string[] names = CharacterSprites.FrameNames;
            int exTop = ExampleGridTop, drawTop = DrawGridTop(cellH);

            Text(px, w, h, Margin, Margin, "ANIMAL VOLLEYBALL - CHARACTER TEMPLATE", HeaderCol);
            Text(px, w, h, Margin, Margin + 7,
                 $"CELLS {W * detail}X{cellH * detail} - FEET AT BASE LINE", LabelCol);
            Text(px, w, h, Margin, Margin + 14, "MAGENTA SHIRT - TEAM COLOUR. GUIDES DROP", LabelCol);
            Text(px, w, h, Margin, exTop - TitleH, "EXAMPLE - THE 11 POSES YOU NEED", HeaderCol);
            Text(px, w, h, Margin, drawTop - TitleH, "DRAW YOUR CHARACTER IN THESE CELLS", HeaderCol);

            // The example is the protagonist rig at this cell height, wearing the magenta the
            // artist is being asked to use — the convention is easier shown than described.
            CharacterDef fox = System.Array.Find(CharacterRoster.All,
                                                 c => c.id == CharacterRoster.ProtagonistId);
            var model = new CharacterDef
            {
                id = "example",
                height = cellH / (float)BaseH,
                fur = fox != null ? fox.fur : new Color(0.87f, 0.45f, 0.15f),
                furAccent = fox != null ? fox.furAccent : new Color(0.96f, 0.90f, 0.78f),
                art = fox != null ? fox.art : new SpeciesArt(),
            };

            for (int i = 0; i < FrameCount; i++)
            {
                RectInt ex = Cell(i, cellH, exTop), dr = Cell(i, cellH, drawTop);

                Fill(px, w, h, ex, PanelCol);          // opaque backing: "not your canvas"
                Fill(px, w, h, dr, new Color32(0, 0, 0, 0));

                Border(px, w, h, ex.x - 1, ex.y - 1, ex.width + 2, ex.height + 2, BorderCol);
                Border(px, w, h, dr.x - 1, dr.y - 1, dr.width + 2, dr.height + 2, BorderCol);
                Text(px, w, h, ex.x, ex.y - LabelH, names[i].ToUpperInvariant(), LabelCol);
                Text(px, w, h, dr.x, dr.y - LabelH, names[i].ToUpperInvariant(), LabelCol);

                Texture2D pose = CharacterArt.BakeFrameTexture(i, JerseyKey, model);
                Stamp(px, w, h, ex, pose, null);       // full colour into the example
                Stamp(px, w, h, dr, pose, GuideCol);   // silhouette into the drawing cell
                Object.DestroyImmediate(pose);

                for (int x = 0; x < dr.width; x += 2)  // ground line: feet rest here
                    Put(px, w, h, dr.x + x, dr.y + dr.height - 2, GroundCol);
                for (int y = 0; y < dr.height; y += 3) // centre line
                    Put(px, w, h, dr.x + dr.width / 2, dr.y + y, GuideCol);
            }

            if (FrameCount < Rows * Cols)
            {
                DrawSwatches(px, w, h, Cell(FrameCount, cellH, exTop));
                DrawReadMe(px, w, h, Cell(FrameCount, cellH, drawTop));
            }

            if (detail > 1) { px = Upscale(px, w, h, detail); w *= detail; h *= detail; }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            string path = TemplatePath(cellH, detail);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return path;
        }

        /// <summary>Show what the magenta shirt turns into on court.</summary>
        static void DrawSwatches(Color32[] px, int w, int h, RectInt c)
        {
            Fill(px, w, h, c, PanelCol);
            Border(px, w, h, c.x - 1, c.y - 1, c.width + 2, c.height + 2, BorderCol);
            Text(px, w, h, c.x, c.y - LabelH, "TEAM COLOURS", LabelCol);

            Fill(px, w, h, new RectInt(c.x + 4, c.y + 3, 10, 10), JerseyKey);
            Text(px, w, h, c.x + 18, c.y + 5, "SHIRT", LabelCol);
            Text(px, w, h, c.x + 4, c.y + 16, "BECOMES", LabelCol);

            Color[] jerseys = { PlayerColors.Human, PlayerColors.Mate,
                                PlayerColors.Opp1, PlayerColors.Opp2 };
            for (int i = 0; i < jerseys.Length; i++)
                Fill(px, w, h, new RectInt(c.x + 4 + i * 11, c.y + 24, 10, 10), jerseys[i]);

            TextBlock(px, w, h, c.x + 2, c.y + 38, c.y + c.height,
                      new[] { "USE MAGENTA", "ONLY ON THE", "SHIRT" });
        }

        /// <summary>The things a first-timer gets wrong, in the unused drawing slot.</summary>
        static void DrawReadMe(Color32[] px, int w, int h, RectInt c)
        {
            Fill(px, w, h, c, PanelCol);
            Border(px, w, h, c.x - 1, c.y - 1, c.width + 2, c.height + 2, BorderCol);
            Text(px, w, h, c.x, c.y - LabelH, "READ ME", HeaderCol);

            // Most important first: a short character's cell can't fit all of them, and TextBlock
            // drops the tail rather than clipping a glyph in half.
            TextBlock(px, w, h, c.x + 2, c.y + 3, c.y + c.height, new[]
            {
                "FACE THE",
                "CAMERA.",
                "WE MIRROR",
                "IT IN GAME",
                "FEET ON",
                "THE DOTTED",
                "LINE. KEEP",
                "CELLS PUT",
            });
        }

        /// <summary>Copy a baked (bottom-up) frame into a top-down cell, either in full colour
        /// (<paramref name="flat"/> null) or as a flat silhouette.</summary>
        static void Stamp(Color32[] px, int w, int h, RectInt c, Texture2D pose, Color32? flat)
        {
            int ph = Mathf.Min(pose.height, c.height), pw = Mathf.Min(pose.width, c.width);
            for (int ty = 0; ty < ph; ty++)
                for (int tx = 0; tx < pw; tx++)
                {
                    Color32 s = pose.GetPixel(tx, ty);
                    if (s.a < 128) continue;
                    Put(px, w, h, c.x + tx, c.y + c.height - 1 - ty, flat ?? s);
                }
        }

        static Color32[] Upscale(Color32[] src, int w, int h, int k)
        {
            var dst = new Color32[w * k * h * k];
            int nw = w * k;
            for (int y = 0; y < h * k; y++)
            {
                int row = (y / k) * w;
                for (int x = 0; x < nw; x++) dst[y * nw + x] = src[row + x / k];
            }
            return dst;
        }

        // ---------------------------------------------------------------- import

        /// <summary>
        /// Slice <paramref name="sheetPath"/>'s drawing grid into the 11 frames for
        /// <paramref name="characterId"/>, recolour the magenta shirt into each of the four jersey
        /// colours, and write the sprites into Resources. Returns false with a human-readable
        /// <paramref name="report"/> on any problem; on success the report is the summary worth
        /// showing the user.
        /// </summary>
        public static bool Import(string sheetPath, string characterId, out string report)
        {
            var log = new StringBuilder();

            if (string.IsNullOrWhiteSpace(sheetPath) || !File.Exists(sheetPath))
            {
                report = $"No sheet at '{sheetPath}'.";
                return false;
            }

            if (!ValidateId(ref characterId, out report)) return false;

            var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(sheet, File.ReadAllBytes(sheetPath), false))
            {
                Object.DestroyImmediate(sheet);
                report = $"'{Path.GetFileName(sheetPath)}' isn't a readable PNG.";
                return false;
            }

            Color32[] px = sheet.GetPixels32();
            int sw = sheet.width, sh = sheet.height;
            Object.DestroyImmediate(sheet);

            if (!TryGeometry(sw, sh, out int detail, out int cellH))
            {
                report = $"Sheet is {sw}x{sh}, which doesn't match any template. Width must be a " +
                         $"whole multiple of {SheetWBase} (e.g. {SheetWBase * 4}x" +
                         $"{SheetHBase(BaseH) * 4} for a 4x height-1.0 template). Re-export a " +
                         "template and draw on that without resizing it.";
                return false;
            }

            float heightStat = cellH / (float)BaseH;
            int drawTop = DrawGridTop(cellH);
            int outW = W * detail, outH = cellH * detail;
            float ppu = CharacterSprites.PixelsPerUnit * detail;

            string[] names = CharacterSprites.FrameNames;
            var frames = new Color32[FrameCount][];
            var source = new string[FrameCount];

            for (int i = 0; i < FrameCount; i++)
            {
                Color32[] cell = ExtractCell(px, sw, sh, Cell(i, cellH, drawTop), detail,
                                             out int kept, out int lowest, out int highest);
                if (kept == 0) continue;    // left blank — filled in below

                frames[i] = cell;
                source[i] = "drawn";
                if (lowest > 6 * detail)
                    log.AppendLine($"'{names[i]}': lowest pixel is {lowest}px above the cell base — " +
                                   "the character will look like it's floating.");
                if (highest >= outH - 1)
                    log.AppendLine($"'{names[i]}': art touches the top edge — it may be clipped.");
            }

            if (frames[0] == null)
            {
                report = "The 'idle' cell is empty. That's the one frame everything else falls " +
                         "back to, so there's nothing to import.";
                return false;
            }

            return WriteFrameSet(frames, source, characterId, outW, outH, ppu, heightStat,
                                 $"{detail}x detail", log, out report);
        }

        /// <summary>
        /// Import a folder of one-PNG-per-pose art (what an artist sends when they didn't use the
        /// template). Files are matched to frames by name — <c>Rhino_Run0.png</c> to
        /// <c>run0</c> — and any pose they didn't draw is filled in from the closest one they
        /// did, so a partial set still produces a playable character.
        /// </summary>
        /// <param name="heightStat">Height stat to give the character, or 0 to derive it from the
        /// image aspect (the pixel-exact choice). Pixels-per-unit is scaled to match, so the feet
        /// stay planted whatever you pick.</param>
        public static bool ImportLooseFolder(string folder, string characterId, float heightStat,
                                             out string report)
        {
            var log = new StringBuilder();

            if (!ValidateId(ref characterId, out report)) return false;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                report = $"No folder at '{folder}'.";
                return false;
            }

            string[] names = CharacterSprites.FrameNames;
            var frames = new Color32[FrameCount][];
            var source = new string[FrameCount];
            int imgW = 0, imgH = 0;
            var unmatched = new List<string>();

            foreach (string file in Directory.GetFiles(folder, "*.png"))
            {
                int frame = MatchFrame(Path.GetFileNameWithoutExtension(file));
                if (frame < 0) { unmatched.Add(Path.GetFileName(file)); continue; }

                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(file), false))
                {
                    Object.DestroyImmediate(tex);
                    log.AppendLine($"'{Path.GetFileName(file)}' isn't a readable PNG — skipped.");
                    continue;
                }

                if (imgW == 0) { imgW = tex.width; imgH = tex.height; }
                else if (tex.width != imgW || tex.height != imgH)
                {
                    string bad = $"'{Path.GetFileName(file)}' is {tex.width}x{tex.height} but the " +
                                 $"others are {imgW}x{imgH}. Every pose must be the same size.";
                    Object.DestroyImmediate(tex);
                    report = bad;
                    return false;
                }

                frames[frame] = tex.GetPixels32();
                source[frame] = "drawn";
                Object.DestroyImmediate(tex);
            }

            if (imgW == 0)
            {
                report = $"No PNGs in '{folder}' matched a pose name. Expected names containing " +
                         $"{string.Join(", ", names)} — e.g. Rhino_Idle.png.";
                return false;
            }
            if (frames[0] == null)
            {
                report = "No 'idle' pose found. That's the one frame everything else falls back " +
                         "to, so there's nothing to import.";
                return false;
            }
            foreach (string u in unmatched)
                log.AppendLine($"'{u}' didn't match any pose name — ignored.");

            // Default height keeps the art pixel-exact against the 48x64 rig; anything else
            // rescales via pixels-per-unit, which is legal but no longer a whole-number zoom.
            float derived = 0.75f * imgH / imgW;
            if (heightStat <= 0f) heightStat = derived;
            heightStat = Mathf.Clamp(heightStat, 0.6f, 1.6f);

            float ppu = imgH * CharacterSprites.PixelsPerUnit / Mathf.RoundToInt(BaseH * heightStat);
            if (Mathf.Abs(heightStat - derived) > 0.005f)
                log.AppendLine($"Height {heightStat:0.##} isn't the art's natural {derived:0.##} — " +
                               $"drawing it at {ppu:0.#} px/unit to keep the feet planted, so the " +
                               "art is scaled by a non-whole factor.");

            return WriteFrameSet(frames, source, characterId, imgW, imgH, ppu, heightStat,
                                 "loose files", log, out report);
        }

        /// <summary>
        /// Fill every missing frame from the closest pose the artist did draw, recolour the shirt
        /// per jersey, and write the whole set out. Shared by both import paths.
        /// </summary>
        static bool WriteFrameSet(Color32[][] frames, string[] source, string characterId,
                                  int outW, int outH, float ppu, float heightStat,
                                  string flavour, StringBuilder log, out string report)
        {
            string[] names = CharacterSprites.FrameNames;
            ResolveMissing(frames, source);

            Directory.CreateDirectory(AbsPath(CustomDir));

            Color[] jerseys = { PlayerColors.Human, PlayerColors.Mate,
                                PlayerColors.Opp1, PlayerColors.Opp2 };
            var written = new List<string>();

            try
            {
                for (int i = 0; i < FrameCount; i++)
                {
                    EditorUtility.DisplayProgressBar("Importing character",
                        $"{names[i]} ({i + 1}/{FrameCount})", i / (float)FrameCount);

                    foreach (Color jersey in jerseys)
                    {
                        string asset = $"{CustomDir}/{CharacterSprites.FrameName(jersey, characterId, names[i])}.png";
                        if (frames[i] == null) { DeleteIfPresent(asset); continue; }
                        WriteSprite(asset, Recolor(frames[i], jersey), outW, outH);
                        written.Add(asset);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();
            foreach (string asset in written) ConfigureImporter(asset, ppu);
            AssetDatabase.SaveAssets();

            log.AppendLine();
            log.AppendLine($"Imported '{characterId}' from {flavour}: {written.Count} sprites at " +
                           $"{outW}x{outH}, {ppu:0.#} px/unit, height {heightStat:0.####}.");
            log.AppendLine();
            for (int i = 0; i < FrameCount; i++)
                log.AppendLine($"  {names[i],-9} {source[i] ?? "omitted"}");
            log.AppendLine();

            int drawn = 0, copied = 0;
            for (int i = 0; i < FrameCount; i++)
            {
                if (source[i] == "drawn") drawn++;
                else if (source[i] != null) copied++;
            }
            if (copied > 0)
                log.AppendLine($"{drawn} poses are real art; {copied} are stand-ins copied from " +
                               "another pose. Re-import after those are drawn to replace them.");
            if (frames[9] == null || frames[10] == null)
                log.AppendLine("The depth-wise dive poses are unset, so dives toward and away from " +
                               "the camera roll the sideways dive flat instead. That's the engine's " +
                               "own fallback and looks fine.");

            if (System.Array.Exists(CharacterRoster.All, c => c.id == characterId))
                log.AppendLine($"\nId '{characterId}' is already on the roster — this art now " +
                               "overrides its procedural look. Re-run \"Build World Tour " +
                               "(Everything)\" so the scenes pick it up.");
            else
                log.AppendLine("\nAdd the character to CharacterRoster.All to make it playable:\n\n" +
                               RosterSnippet(characterId, heightStat));

            report = log.ToString();
            Debug.Log("[Volleyball] " + report);
            return true;
        }

        /// <summary>
        /// Where each frame looks for a stand-in, best first. Chains resolve (run1 to run0 to
        /// idle), and the optional depth-dive frames deliberately have none — the animator's own
        /// fallback beats a wrong pose.
        /// </summary>
        static readonly int[][] FrameFallbacks =
        {
            new int[0],           // idle     — required, the root everything falls back to
            new[] { 0 },          // run0     <- idle
            new[] { 1, 0 },       // run1     <- run0
            new[] { 4, 0 },       // jump     <- swing
            new[] { 3, 0 },       // swing    <- jump
            new[] { 0 },          // bump     <- idle (both stand with arms low)
            new[] { 4, 3, 0 },    // set      <- swing (hands up)
            new[] { 4, 3, 0 },    // block    <- swing (arms overhead)
            new[] { 3, 4, 0 },    // dive     <- jump (arms lead; the game rolls it flat)
            new int[0],           // diveUp   — optional
            new int[0],           // diveDown — optional
        };

        static void ResolveMissing(Color32[][] frames, string[] source)
        {
            string[] names = CharacterSprites.FrameNames;
            for (int pass = 0; pass < FrameCount; pass++)
            {
                bool changed = false;
                for (int i = 0; i < FrameCount; i++)
                {
                    if (frames[i] != null || CharacterSprites.FrameIsOptional(i)) continue;
                    foreach (int src in FrameFallbacks[i])
                        if (frames[src] != null)
                        {
                            frames[i] = frames[src];
                            source[i] = $"copied from {names[src]}";
                            changed = true;
                            break;
                        }
                }
                if (!changed) break;
            }
        }

        /// <summary>Frame index for a filename like "Rhino_DiveUp", or -1. Longest names first so
        /// "diveup" doesn't get claimed by "dive".</summary>
        static int MatchFrame(string fileName)
        {
            var norm = new StringBuilder();
            foreach (char c in fileName.ToLowerInvariant())
                if (char.IsLetterOrDigit(c)) norm.Append(c);
            string s = norm.ToString();

            string[] names = CharacterSprites.FrameNames;
            var order = new List<int>();
            for (int i = 0; i < names.Length; i++) order.Add(i);
            order.Sort((a, b) => names[b].Length.CompareTo(names[a].Length));

            foreach (int i in order)
                if (s.EndsWith(names[i].ToLowerInvariant())) return i;
            foreach (int i in order)
                if (s.Contains(names[i].ToLowerInvariant())) return i;
            return -1;
        }

        static void DeleteIfPresent(string assetPath)
        {
            if (File.Exists(AbsPath(assetPath))) AssetDatabase.DeleteAsset(assetPath);
        }

        static bool ValidateId(ref string characterId, out string error)
        {
            characterId = (characterId ?? "").Trim().ToLowerInvariant();
            if (characterId.Length == 0)
            {
                error = "Give the character an id (lowercase letters/digits, e.g. \"rhino\").";
                return false;
            }
            foreach (char ch in characterId)
                if (!(ch >= 'a' && ch <= 'z') && !(ch >= '0' && ch <= '9'))
                {
                    error = $"Id '{characterId}' must be lowercase letters and digits only — it " +
                            "becomes part of the sprite filenames.";
                    return false;
                }
            error = null;
            return true;
        }

        /// <summary>Recover the detail factor and base cell height from a sheet's dimensions. The
        /// width pins the factor exactly, so there's no ambiguity with the height stat.</summary>
        static bool TryGeometry(int sw, int sh, out int detail, out int cellH)
        {
            detail = 0; cellH = 0;
            if (sw <= 0 || sw % SheetWBase != 0) return false;

            detail = sw / SheetWBase;
            if (detail < 1 || detail > MaxDetail || sh % detail != 0) return false;

            int baseH = sh / detail;
            cellH = CellHeightFor(baseH);
            return cellH >= 20 && SheetHBase(cellH) == baseH;
        }

        /// <summary>
        /// Pull one drawing cell out of the sheet at full detail, dropping the guide colours and
        /// anything effectively invisible. Artist alpha is otherwise preserved. Reports how many
        /// pixels survived and how far the art sits from the cell's base/top, in output pixels.
        /// </summary>
        static Color32[] ExtractCell(Color32[] px, int sw, int sh, RectInt baseCell, int detail,
                                     out int kept, out int lowest, out int highest)
        {
            int outW = baseCell.width * detail, outH = baseCell.height * detail;
            int x0 = baseCell.x * detail, yTop0 = baseCell.y * detail;
            var cell = new Color32[outW * outH];
            kept = 0;
            lowest = outH;
            highest = -1;

            for (int oy = 0; oy < outH; oy++)          // oy: 0 = bottom of the sprite
            {
                int srcTop = yTop0 + (outH - 1 - oy);
                int srcRow = sh - 1 - srcTop;          // GetPixels32 is bottom-up
                if (srcRow < 0 || srcRow >= sh) continue;

                for (int ox = 0; ox < outW; ox++)
                {
                    int sx = x0 + ox;
                    if (sx < 0 || sx >= sw) continue;

                    Color32 s = px[srcRow * sw + sx];
                    if (IsGuide(s)) continue;

                    cell[oy * outW + ox] = s;
                    kept++;
                    if (oy < lowest) lowest = oy;
                    if (oy > highest) highest = oy;
                }
            }

            if (kept == 0) lowest = 0;
            return cell;
        }

        /// <summary>True for template guide pixels and for anything too faint to see. Exact match
        /// catches an untouched guide; the loose match catches one a colour-managed save nudged.</summary>
        static bool IsGuide(Color32 c)
        {
            if (c.a < 16) return true;
            foreach (Color32 g in GuideKeys)
            {
                if (c.r == g.r && c.g == g.g && c.b == g.b && c.a == g.a) return true;
                if (c.a < 128
                    && Mathf.Abs(c.r - g.r) <= 24
                    && Mathf.Abs(c.g - g.g) <= 24
                    && Mathf.Abs(c.b - g.b) <= 24) return true;
            }
            return false;
        }

        /// <summary>
        /// Map the magenta shirt key onto one jersey colour, keeping the artist's shading: the
        /// magenta's brightness scales the jersey colour and any green in it lifts the pixel
        /// toward white (so highlights stay highlights). Non-magenta pixels pass through.
        /// </summary>
        static Color32[] Recolor(Color32[] cell, Color jersey)
        {
            var outPx = new Color32[cell.Length];
            for (int i = 0; i < cell.Length; i++)
            {
                Color32 c = cell[i];
                if (c.a == 0) continue;

                float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f;
                if (!IsJerseyKey(r, g, b)) { outPx[i] = c; continue; }

                float v = Mathf.Max(r, b);                            // how bright the magenta is
                float white = Mathf.Clamp01(g / Mathf.Max(v, 1e-4f)); // how washed-out it is
                outPx[i] = new Color32(
                    Byte(Mathf.Lerp(jersey.r * v, v, white)),
                    Byte(Mathf.Lerp(jersey.g * v, v, white)),
                    Byte(Mathf.Lerp(jersey.b * v, v, white)), c.a);
            }
            return outPx;
        }

        /// <summary>Magenta-family test: red and blue both strong and roughly equal, green low.
        /// Tuned to catch magenta/purple shading while leaving skin, sand and warm tones alone.</summary>
        static bool IsJerseyKey(float r, float g, float b)
        {
            float lo = Mathf.Min(r, b), hi = Mathf.Max(r, b);
            return hi > 0.25f && lo > 0.20f && g < 0.75f * lo && (hi - lo) < 0.5f * hi;
        }

        static byte Byte(float v) => (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);

        static void WriteSprite(string assetPath, Color32[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(AbsPath(assetPath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        /// <summary>Same importer settings the procedural bake uses, except pixels-per-unit, which
        /// scales with the detail factor so the character keeps its world size.</summary>
        static void ConfigureImporter(string assetPath, float ppu)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            if (imp == null) return;

            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.spritePixelsPerUnit = ppu;
            imp.SaveAndReimport();
        }

        static string RosterSnippet(string id, float heightStat)
        {
            string title = char.ToUpperInvariant(id[0]) + id.Substring(1);
            return
$@"new CharacterDef
{{
    id = ""{id}"", displayName = ""{title}"", region = """",
    powerUp = PowerUpType.GoldenTouch,
    blurb = ""..."",
    height = {heightStat.ToString("0.####")}f, speed = 1.00f, power = 1.00f, control = 1.00f, jump = 1.00f,
    // fur/furAccent/art only drive the procedural bake — unused while custom art exists
    fur = new Color(0.70f, 0.70f, 0.70f), furAccent = new Color(0.90f, 0.90f, 0.90f),
}},";
        }

        static string AbsPath(string assetPath) =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);

        // ---------------------------------------------------------------- 3x5 pixel font
        // Labels have to be legible at 1x inside a 48px cell, which rules out anything Unity can
        // render for us. Rows are top-down, '#' = ink.

        const string FontSrc =
            "A .#. #.# ### #.# #.#|B ##. #.# ##. #.# ##.|C .## #.. #.. #.. .##|" +
            "D ##. #.# #.# #.# ##.|E ### #.. ##. #.. ###|F ### #.. ##. #.. #..|" +
            "G .## #.. #.# #.# .##|H #.# #.# ### #.# #.#|I ### .#. .#. .#. ###|" +
            "J ..# ..# ..# #.# .#.|K #.# #.# ##. #.# #.#|L #.. #.. #.. #.. ###|" +
            // N keeps a solid middle row on purpose: the open-middle variant (#.#/##./#.#/.##/#.#)
            // reads as an S at 3px wide, turning RUN0 into RUS0.
            "M #.# ### ### #.# #.#|N #.# ##. ### .## #.#|O .#. #.# #.# #.# .#.|" +
            "P ##. #.# ##. #.. #..|Q .#. #.# #.# ##. .##|R ##. #.# ##. #.# #.#|" +
            "S .## #.. .#. ..# ##.|T ### .#. .#. .#. .#.|U #.# #.# #.# #.# ###|" +
            "V #.# #.# #.# #.# .#.|W #.# #.# ### ### #.#|X #.# #.# .#. #.# #.#|" +
            "Y #.# #.# .#. .#. .#.|Z ### ..# .#. #.. ###|0 ### #.# #.# #.# ###|" +
            "1 .#. ##. .#. .#. ###|2 ##. ..# .#. #.. ###|3 ### ..# .## ..# ###|" +
            "4 #.# #.# ### ..# ..#|5 ### #.. ##. ..# ##.|6 .## #.. ### #.# ###|" +
            "7 ### ..# .#. .#. .#.|8 ### #.# ### #.# ###|9 ### #.# ### ..# ##.|" +
            "- ... ... ### ... ...|. ... ... ... ... .#.|, ... ... ... .#. #..|" +
            ": ... .#. ... .#. ...|/ ..# ..# .#. #.. #..|! .#. .#. .#. ... .#.|" +
            "( .#. #.. #.. #.. .#.|) .#. ..# ..# ..# .#.|+ ... .#. ### .#. ...";

        static Dictionary<char, string[]> _font;

        static Dictionary<char, string[]> Font
        {
            get
            {
                if (_font != null) return _font;
                _font = new Dictionary<char, string[]>();
                foreach (string glyph in FontSrc.Split('|'))
                    _font[glyph[0]] = glyph.Substring(2).Split(' ');
                return _font;
            }
        }

        /// <summary>Draw <paramref name="s"/> with its top-left at (x, yTop), top-down coords.</summary>
        static void Text(Color32[] px, int w, int h, int x, int yTop, string s, Color32 c)
        {
            foreach (char raw in s.ToUpperInvariant())
            {
                if (Font.TryGetValue(raw, out string[] rows))
                    for (int gy = 0; gy < rows.Length; gy++)
                        for (int gx = 0; gx < rows[gy].Length; gx++)
                            if (rows[gy][gx] == '#') Put(px, w, h, x + gx, yTop + gy, c);
                x += 4;
            }
        }

        /// <summary>Stacked lines from yTop down, stopping before <paramref name="maxY"/> so a
        /// short cell loses whole lines instead of clipping one through the middle.</summary>
        static void TextBlock(Color32[] px, int w, int h, int x, int yTop, int maxY, string[] lines)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                int y = yTop + i * 6;
                if (y + 5 > maxY) return;
                Text(px, w, h, x, y, lines[i], LabelCol);
            }
        }

        static void Fill(Color32[] px, int w, int h, RectInt r, Color32 c)
        {
            for (int y = 0; y < r.height; y++)
                for (int x = 0; x < r.width; x++)
                    Put(px, w, h, r.x + x, r.y + y, c);
        }

        static void Border(Color32[] px, int w, int h, int x, int yTop, int rw, int rh, Color32 c)
        {
            for (int i = 0; i < rw; i++) { Put(px, w, h, x + i, yTop, c); Put(px, w, h, x + i, yTop + rh - 1, c); }
            for (int i = 0; i < rh; i++) { Put(px, w, h, x, yTop + i, c); Put(px, w, h, x + rw - 1, yTop + i, c); }
        }

        static void Put(Color32[] px, int w, int h, int x, int yTop, Color32 c)
        {
            if (x < 0 || x >= w || yTop < 0 || yTop >= h) return;
            px[(h - 1 - yTop) * w + x] = c;
        }
    }

    /// <summary>Artist-facing front end for <see cref="CustomCharacterSheet"/>.</summary>
    public class CustomCharacterWindow : EditorWindow
    {
        static readonly int[] Details = { 1, 2, 4, 6, 8 };
        static readonly string[] DetailLabels =
            { "1x (48x64 - native)", "2x (96x128)", "4x (192x256)", "6x (288x384)", "8x (384x512)" };

        float _height = 1f;
        int _detailIndex = 2;
        string _sheetPath = "";
        string _folder = "";
        float _looseHeight;
        string _id = "";
        string _result = "";
        bool _ok;
        Vector2 _scroll;

        [MenuItem("Volleyball/Custom Characters/Import Sprite Sheet...", priority = 41)]
        static void Open() =>
            GetWindow<CustomCharacterWindow>(true, "Custom Character", true).minSize = new Vector2(440, 520);

        void OnGUI()
        {
            EditorGUILayout.LabelField("1. Export a template", EditorStyles.boldLabel);
            _height = EditorGUILayout.Slider("Height stat", _height, 0.6f, 1.6f);
            _detailIndex = EditorGUILayout.Popup("Detail", _detailIndex, DetailLabels);

            int detail = Details[_detailIndex];
            int cellH = Mathf.RoundToInt(CharacterSprites.BaseCanvasHeight * _height);
            EditorGUILayout.LabelField(" ", $"cell = {CharacterSprites.BaseCanvasWidth * detail} x " +
                $"{cellH * detail} px  ({CharacterSprites.PixelsPerUnit * detail:0} px/unit)");
            EditorGUILayout.HelpBox(
                "Detail only changes how many pixels the artist gets — the character occupies the " +
                "same space on court either way. 4x is a comfortable size to draw at.",
                MessageType.None);

            if (GUILayout.Button("Save Template Sheet"))
            {
                string path = CustomCharacterSheet.SaveTemplate(_height, detail);
                _ok = true;
                _result = "Template saved to\n" + path + "\n\nSend that PNG to your artist.";
                EditorUtility.RevealInFinder(path);
            }

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("2. Import finished art", EditorStyles.boldLabel);

            // Shared by BOTH import buttons below — it used to sit inside the sheet section,
            // where it read as sheet-only and silently disabled the loose-file import.
            _id = EditorGUILayout.TextField("Character id", _id);
            bool noId = string.IsNullOrWhiteSpace(_id);
            if (noId)
                EditorGUILayout.HelpBox("Both imports need a character id — lowercase letters and " +
                                        "digits, e.g. \"rhino\".", MessageType.Warning);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("From a filled-in template sheet", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _sheetPath = EditorGUILayout.TextField("Sheet PNG", _sheetPath);
                if (GUILayout.Button("...", GUILayout.Width(28)))
                {
                    string picked = EditorUtility.OpenFilePanel("Filled-in character sheet", "", "png");
                    if (!string.IsNullOrEmpty(picked)) _sheetPath = picked;
                }
            }
            using (new EditorGUI.DisabledScope(noId || string.IsNullOrWhiteSpace(_sheetPath)))
                if (GUILayout.Button("Import Sheet"))
                    _ok = CustomCharacterSheet.Import(_sheetPath, _id, out _result);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Or from loose files — one PNG per pose",
                                       EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "For art that didn't come from the template. Files are matched by name " +
                "(Rhino_Run0.png -> run0); any pose that's missing is copied from the closest one " +
                "that exists. Height 0 = derive from the image.", MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                _folder = EditorGUILayout.TextField("Folder", _folder);
                if (GUILayout.Button("...", GUILayout.Width(28)))
                {
                    string picked = EditorUtility.OpenFolderPanel("Folder of pose PNGs", "", "");
                    if (!string.IsNullOrEmpty(picked)) _folder = picked;
                }
            }
            _looseHeight = EditorGUILayout.FloatField("Height stat (0 = auto)", _looseHeight);

            using (new EditorGUI.DisabledScope(noId || string.IsNullOrWhiteSpace(_folder)))
                if (GUILayout.Button("Import Loose Files"))
                    _ok = CustomCharacterSheet.ImportLooseFolder(_folder, _id, _looseHeight, out _result);

            if (string.IsNullOrEmpty(_result)) return;

            EditorGUILayout.Space(8);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.HelpBox(_result, _ok ? MessageType.Info : MessageType.Error);
            if (_ok && GUILayout.Button("Copy to clipboard")) EditorGUIUtility.systemCopyBuffer = _result;
            EditorGUILayout.EndScrollView();
        }
    }
}
