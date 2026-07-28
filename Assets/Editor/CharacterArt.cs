using System.IO;
using UnityEditor;
using UnityEngine;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// Procedurally bakes a tiny humanoid sprite sheet (idle / run0 / run1 / jump / swing) for a
    /// player, in the same code-generated spirit as the placeholder circle/ball art. One PNG is
    /// baked per frame per team colour and cached under <c>Assets/Resources/Characters/</c>, so
    /// re-running a scene builder is cheap and idempotent. <see cref="AttachCharacter"/> wires the
    /// frames onto a sprite child and adds a <see cref="CharacterAnimator"/> to play them.
    ///
    /// The figure is a flat front-facing pixel character (skin head/limbs, team-coloured tank,
    /// dark shorts) drawn with a 1px outline for readability against the beach backdrop. Limbs are
    /// drawn as thick segments whose joints differ per frame to give a readable run/jump/spike.
    ///
    /// Frames are baked per <see cref="CharacterDef"/>: the roster character's height stat
    /// stretches the whole figure vertically (a taller canvas, longer legs/torso, same-size head)
    /// and its skin/hair colours are drawn in, so each character is recognisable at a glance.
    /// </summary>
    public static class CharacterArt
    {
        // Baked into Resources so runtime code (CharacterSprites) can load any character in any
        // jersey colour — that's what lets the menu's character select re-dress a built arena.
        public const string Dir = "Assets/Resources/" + CharacterSprites.ResourceFolder;
        const int W = CharacterSprites.BaseCanvasWidth;
        const int H = CharacterSprites.BaseCanvasHeight; // scaled by the height stat
        const float PixelsPerUnit = CharacterSprites.PixelsPerUnit;

        /// <summary>Sprite child localPosition that plants this character's feet on the ground.</summary>
        public static Vector3 SpriteLocalPosFor(CharacterDef ch) => CharacterSprites.SpriteLocalPosFor(ch);

        /// <summary>
        /// Bake every roster character in every player jersey colour, so a runtime character
        /// swap (menu pick / random AI draw) always finds its frames. Cached per PNG, so
        /// re-running is cheap. Called by the scene builders.
        /// </summary>
        public static void BakeRoster()
        {
            foreach (var color in new[] { PlayerColors.Human, PlayerColors.Mate,
                                          PlayerColors.Opp1, PlayerColors.Opp2 })
                foreach (var ch in CharacterRoster.All)
                    GetCharacterFrames(color, ch);
        }

        /// <summary>
        /// Replace whatever sprite is on <paramref name="sr"/> with a baked frame set for
        /// <paramref name="character"/> in team colour <paramref name="color"/> and add a
        /// <see cref="CharacterAnimator"/> to drive it.
        /// </summary>
        public static void AttachCharacter(GameObject spriteChild, SpriteRenderer sr, Color color,
                                           CharacterDef character)
        {
            Sprite[] f = GetCharacterFrames(color, character);
            sr.sprite = f[0];
            sr.color = Color.white;          // colour is baked into the frames

            var anim = spriteChild.GetComponent<CharacterAnimator>() ?? spriteChild.AddComponent<CharacterAnimator>();
            anim.idle = f[0];
            anim.run0 = f[1];
            anim.run1 = f[2];
            anim.jump = f[3];
            anim.swing = f[4];
            anim.bumpPose = f[5];
            anim.setPose = f[6];
            anim.blockPose = f[7];
            anim.divePose = f[8];
            anim.diveUpPose = f[9];
            anim.diveDownPose = f[10];
        }

        /// <summary>Bake any missing frames for the whole roster without rebuilding a scene —
        /// handy after adding a new frame name (existing PNGs are cached and untouched).</summary>
        [MenuItem("Volleyball/Bake Character Sprites", priority = 25)]
        static void BakeRosterMenu()
        {
            BakeRoster();
            Debug.Log("[Volleyball] Character sprites baked/updated in " + Dir);
        }

        /// <summary>The full frame set, baked (and cached) per team colour + character. Delete
        /// <c>Assets/Resources/Characters/</c> to force a re-bake after tweaking the roster.</summary>
        public static Sprite[] GetCharacterFrames(Color color, CharacterDef character)
        {
            Directory.CreateDirectory(AbsPath(Dir));

            string[] names = CharacterSprites.FrameNames;
            var frames = new Sprite[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                // named via CharacterSprites so runtime Resources.Load finds the same files
                string assetPath = $"{Dir}/{CharacterSprites.FrameName(color, character.id, names[i])}.png";
                if (!File.Exists(AbsPath(assetPath)))
                {
                    var tex = BakeFrame(i, color, character);
                    File.WriteAllBytes(AbsPath(assetPath), tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                }
                ConfigureImporter(assetPath);
                frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            }
            return frames;
        }

        static void ConfigureImporter(string assetPath)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            if (imp == null) return;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.spritePixelsPerUnit = PixelsPerUnit;
            imp.SaveAndReimport();
        }

        // ---------------------------------------------------------------- baking

        struct Pose
        {
            public Vector2 kneeL, footL, kneeR, footR;
            public Vector2 elbowL, handL, elbowR, handR;
            public float bob;        // whole-figure vertical bob
            public float headDX;     // upper-body lean
            public bool armsInFront; // draw BOTH arms over the torso (arms presented to camera)
        }

        // shoulders/hips are fixed; only the joints below the shoulder/hip vary per frame
        static readonly Vector2 HipL = new Vector2(20, 26), HipR = new Vector2(28, 26);
        static readonly Vector2 ShoulderL = new Vector2(18, 47), ShoulderR = new Vector2(30, 47);

        static Pose GetPose(int frame)
        {
            switch (frame)
            {
                case 1: // run0 — right leg drives up/forward, left planted; right arm forward/up
                    return new Pose
                    {
                        kneeL = new Vector2(20, 14), footL = new Vector2(21, 2),
                        kneeR = new Vector2(29, 21), footR = new Vector2(31, 15),
                        elbowL = new Vector2(16, 39), handL = new Vector2(15, 31),
                        elbowR = new Vector2(31, 42), handR = new Vector2(33, 46),
                        bob = 1f,
                    };
                case 2: // run1 — mirror of run0
                    return new Pose
                    {
                        kneeL = new Vector2(19, 21), footL = new Vector2(17, 15),
                        kneeR = new Vector2(28, 14), footR = new Vector2(27, 2),
                        elbowL = new Vector2(17, 42), handL = new Vector2(15, 46),
                        elbowR = new Vector2(32, 39), handR = new Vector2(33, 31),
                        bob = 0f,
                    };
                case 3: // jump — legs tucked apart, both arms up
                    return new Pose
                    {
                        kneeL = new Vector2(18, 18), footL = new Vector2(16, 9),
                        kneeR = new Vector2(30, 18), footR = new Vector2(32, 9),
                        elbowL = new Vector2(16, 50), handL = new Vector2(13, 58),
                        elbowR = new Vector2(32, 50), handR = new Vector2(35, 58),
                        bob = 0f,
                    };
                case 4: // swing — hitting (right) arm straight overhead, left arm out for balance
                    return new Pose
                    {
                        kneeL = new Vector2(20, 16), footL = new Vector2(18, 6),
                        kneeR = new Vector2(28, 16), footR = new Vector2(30, 6),
                        elbowL = new Vector2(15, 41), handL = new Vector2(12, 34),
                        elbowR = new Vector2(31, 53), handR = new Vector2(34, 63),
                        bob = 1f, headDX = 1f,
                    };
                case 5: // bump — knees bent; both arms come down the sides and meet in a platform
                        // in front of the body, both arms drawn toward the camera
                    return new Pose
                    {
                        kneeL = new Vector2(19, 13), footL = new Vector2(18, 2),
                        kneeR = new Vector2(29, 13), footR = new Vector2(30, 2),
                        elbowL = new Vector2(16, 34), handL = new Vector2(23, 25),
                        elbowR = new Vector2(32, 34), handR = new Vector2(25, 25),
                        bob = -1f, armsInFront = true,
                    };
                case 6: // set — both hands raised in front of the forehead (overhead set),
                        // both arms drawn toward the camera
                    return new Pose
                    {
                        kneeL = new Vector2(20, 15), footL = new Vector2(19, 4),
                        kneeR = new Vector2(28, 15), footR = new Vector2(29, 4),
                        elbowL = new Vector2(16, 43), handL = new Vector2(21, 53),
                        elbowR = new Vector2(32, 43), handR = new Vector2(27, 53),
                        armsInFront = true,
                    };
                case 7: // block — both arms straight up above the head
                    return new Pose
                    {
                        kneeL = new Vector2(20, 16), footL = new Vector2(19, 6),
                        kneeR = new Vector2(28, 16), footR = new Vector2(29, 6),
                        elbowL = new Vector2(18, 50), handL = new Vector2(18, 62),
                        elbowR = new Vector2(30, 50), handR = new Vector2(30, 62),
                        bob = 1f,
                    };
                case 8: // dive — superman layout: both arms stretched together past the head,
                        // legs trailing nearly straight. The animator rolls the whole sprite
                        // horizontal during the slide, so "up" here becomes the dive direction.
                    return new Pose
                    {
                        kneeL = new Vector2(19, 14), footL = new Vector2(16, 3),
                        kneeR = new Vector2(28, 14), footR = new Vector2(31, 3),
                        elbowL = new Vector2(17, 52), handL = new Vector2(21, 63),
                        elbowR = new Vector2(31, 52), handR = new Vector2(27, 63),
                        armsInFront = true,
                    };
                default: // idle — stand, arms at sides
                    return new Pose
                    {
                        kneeL = new Vector2(20, 14), footL = new Vector2(20, 2),
                        kneeR = new Vector2(28, 14), footR = new Vector2(28, 2),
                        elbowL = new Vector2(16, 38), handL = new Vector2(16, 30),
                        elbowR = new Vector2(32, 38), handR = new Vector2(32, 30),
                    };
            }
        }

        static Texture2D BakeFrame(int frame, Color jersey, CharacterDef ch)
        {
            int h = Mathf.RoundToInt(H * ch.height);
            var tex = new Texture2D(W, h, TextureFormat.RGBA32, false);
            var clear = new Color[W * h]; // default is (0,0,0,0)
            tex.SetPixels(clear);

            // frames 9/10 (diveUp/diveDown) are foreshortened lying poses with their own
            // painter's-order draw — the standing-figure Pose rig can't reposition the torso
            if (frame >= 9)
            {
                bool away = frame == 9;
                DrawDiveDepth(tex, away, true, jersey, ch);
                DrawDiveDepth(tex, away, false, jersey, ch);
                tex.Apply();
                return tex;
            }

            Pose p = Stretch(GetPose(frame), ch.height);
            // outline pass first (everything grown by 1px in dark), then the colour fill on top
            DrawFigure(tex, p, true, jersey, ch);
            DrawFigure(tex, p, false, jersey, ch);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// A dive along the camera's depth axis, drawn as a foreshortened body lying on the
        /// ground (the animator shows it upright and unrolled, feet region at the baseline).
        /// <paramref name="away"/>: head-first away from the camera (we see the back and the
        /// soles); otherwise head-first toward the camera (face and hands nearest).
        /// </summary>
        static void DrawDiveDepth(Texture2D tex, bool away, bool outline, Color jersey, CharacterDef ch)
        {
            float s = ch.height;
            Color shortsCol = jersey * 0.45f; shortsCol.a = 1f;
            Color line = new Color(0.06f, 0.06f, 0.09f);
            int g = outline ? 1 : 0;
            int Y(float y) => Mathf.RoundToInt(y * s);
            Color Skin() => outline ? line : ch.skin;
            Color JerseyC() => outline ? line : jersey;
            Color ShortsC() => outline ? line : shortsCol;
            Color HairC() => outline ? line : ch.hair;

            if (away)
            {
                // farthest first: arms stretched past the head onto the far sand, then the
                // (all-hair) back of the head, torso, shorts, and finally the near legs/soles
                Limb(tex, SY(new Vector2(19, 27), s), SY(new Vector2(15, 34), s), SY(new Vector2(20, 42), s), 2, g, Skin(), 0f);
                Limb(tex, SY(new Vector2(29, 27), s), SY(new Vector2(33, 34), s), SY(new Vector2(28, 42), s), 2, g, Skin(), 0f);
                Box(tex, 19, Y(28), 29, Y(35), g, Skin(), 0f);   // head
                Box(tex, 18, Y(30), 30, Y(36), g, HairC(), 0f);  // seen from behind: all hair
                Box(tex, 17, Y(17), 31, Y(28), g, JerseyC(), 0f); // foreshortened torso
                Box(tex, 16, Y(12), 32, Y(18), g, ShortsC(), 0f);
                Limb(tex, SY(new Vector2(20, 14), s), SY(new Vector2(18, 8), s), SY(new Vector2(16, 2), s), 3, g, Skin(), 0f);
                Limb(tex, SY(new Vector2(28, 14), s), SY(new Vector2(30, 8), s), SY(new Vector2(32, 2), s), 3, g, Skin(), 0f);
            }
            else
            {
                // farthest first: trailing legs small at the top, then shorts and torso coming
                // forward, the face just above the sand, and the reaching arms nearest of all
                Limb(tex, SY(new Vector2(20, 32), s), SY(new Vector2(19, 38), s), SY(new Vector2(18, 43), s), 2, g, Skin(), 0f);
                Limb(tex, SY(new Vector2(28, 32), s), SY(new Vector2(29, 38), s), SY(new Vector2(30, 43), s), 2, g, Skin(), 0f);
                Box(tex, 16, Y(28), 32, Y(34), g, ShortsC(), 0f);
                Box(tex, 17, Y(19), 31, Y(29), g, JerseyC(), 0f);
                Box(tex, 18, Y(10), 30, Y(20), g, Skin(), 0f);   // face toward the camera
                Box(tex, 17, Y(17), 31, Y(21), g, HairC(), 0f);  // hair above the face
                Limb(tex, SY(new Vector2(19, 26), s), SY(new Vector2(14, 16), s), SY(new Vector2(20, 5), s), 2, g, Skin(), 0f);
                Limb(tex, SY(new Vector2(29, 26), s), SY(new Vector2(34, 16), s), SY(new Vector2(28, 5), s), 2, g, Skin(), 0f);
            }
        }

        // The height stat stretches the figure vertically: every joint and torso row is scaled,
        // so a tall character gets longer legs, arms and torso on a taller canvas.
        static Vector2 SY(Vector2 v, float s) => new Vector2(v.x, v.y * s);

        static Pose Stretch(Pose p, float s)
        {
            p.kneeL = SY(p.kneeL, s); p.footL = SY(p.footL, s);
            p.kneeR = SY(p.kneeR, s); p.footR = SY(p.footR, s);
            p.elbowL = SY(p.elbowL, s); p.handL = SY(p.handL, s);
            p.elbowR = SY(p.elbowR, s); p.handR = SY(p.handR, s);
            return p;
        }

        static void DrawFigure(Texture2D tex, Pose p, bool outline, Color jersey, CharacterDef ch)
        {
            float s = ch.height;
            Color shorts = jersey * 0.45f; shorts.a = 1f;
            Color line = new Color(0.06f, 0.06f, 0.09f);
            int g = outline ? 1 : 0;
            float oy = p.bob;
            int Y(float y) => Mathf.RoundToInt(y * s);

            Color Skin() => outline ? line : ch.skin;
            Color Jersey() => outline ? line : jersey;
            Color Shorts() => outline ? line : shorts;
            Color Hair() => outline ? line : ch.hair;

            // right leg behind the torso; right arm behind too, unless the pose presents both
            // arms toward the camera (bump/set), in which case both arms are drawn in front.
            if (!p.armsInFront)
                Limb(tex, SY(ShoulderR, s), p.elbowR, p.handR, 2, g, Skin(), oy);
            Limb(tex, SY(HipR, s), p.kneeR, p.footR, 3, g, Skin(), oy);
            Limb(tex, SY(HipL, s), p.kneeL, p.footL, 3, g, Skin(), oy);

            // head + hair anchor to the canvas top so the head stays near its baseline size —
            // the stretch goes into the legs and torso, which is what reads as "tall"
            int top = tex.height - 1;
            Box(tex, 16, Y(22), 32, Y(31), g, Shorts(), oy);            // shorts
            Box(tex, 16, Y(30), 32, Y(49), g, Jersey(), oy);            // tank top / torso
            Box(tex, 18, Y(49), 30, top - 2, g, Skin(), oy, p.headDX);  // head
            Box(tex, 17, top - 5, 31, top, g, Hair(), oy, p.headDX);    // hair cap

            if (p.armsInFront)                                          // both arms over the torso
                Limb(tex, SY(ShoulderR, s), p.elbowR, p.handR, 2, g, Skin(), oy);
            Limb(tex, SY(ShoulderL, s), p.elbowL, p.handL, 2, g, Skin(), oy); // front (left) arm
        }

        // ---------------------------------------------------------------- raster primitives

        static void Limb(Texture2D tex, Vector2 a, Vector2 b, Vector2 c, int r, int grow, Color col, float oy)
        {
            Seg(tex, a, b, r + grow, col, oy);
            Seg(tex, b, c, r + grow, col, oy);
        }

        static void Seg(Texture2D tex, Vector2 a, Vector2 b, int r, Color col, float oy)
        {
            int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y))) + 1;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Stamp(tex, Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, t)),
                          Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, t) + oy), r, col);
            }
        }

        static void Stamp(Texture2D tex, int cx, int cy, int r, Color col)
        {
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                    SetPx(tex, x, y, col);
        }

        static void Box(Texture2D tex, int x0, int y0, int x1, int y1, int grow, Color col, float oy, float dx = 0f)
        {
            int ox = Mathf.RoundToInt(dx);
            int oyi = Mathf.RoundToInt(oy);
            for (int y = y0 - grow; y <= y1 + grow; y++)
                for (int x = x0 - grow; x <= x1 + grow; x++)
                    SetPx(tex, x + ox, y + oyi, col);
        }

        static void SetPx(Texture2D tex, int x, int y, Color col)
        {
            if (x < 0 || x >= tex.width || y < 0 || y >= tex.height) return;
            tex.SetPixel(x, y, col);
        }

        static string AbsPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
