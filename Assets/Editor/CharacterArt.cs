using System.IO;
using UnityEditor;
using UnityEngine;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// Procedurally bakes a tiny humanoid sprite sheet (idle / run0 / run1 / jump / swing) for a
    /// player, in the same code-generated spirit as the placeholder circle/ball art. One PNG is
    /// baked per frame per team colour and cached under <c>Assets/Sprites/Characters/</c>, so
    /// re-running a scene builder is cheap and idempotent. <see cref="AttachCharacter"/> wires the
    /// frames onto a sprite child and adds a <see cref="CharacterAnimator"/> to play them.
    ///
    /// The figure is a flat front-facing pixel character (skin head/limbs, team-coloured tank,
    /// dark shorts) drawn with a 1px outline for readability against the beach backdrop. Limbs are
    /// drawn as thick segments whose joints differ per frame to give a readable run/jump/spike.
    /// </summary>
    public static class CharacterArt
    {
        public const string Dir = "Assets/Sprites/Characters";
        const int W = 48, H = 64;          // canvas; figure occupies roughly y:2..63
        const float PixelsPerUnit = 34f;   // ~1.8 world units tall at scale 1

        // names line up with the frame order returned by GetCharacterFrames
        static readonly string[] FrameNames = { "idle", "run0", "run1", "jump", "swing", "bump", "set", "block" };

        /// <summary>Sprite child localPosition that plants the figure's feet on the ground.</summary>
        public static readonly Vector3 SpriteLocalPos = new Vector3(0f, 0.9f, 0f);

        /// <summary>
        /// Replace whatever sprite is on <paramref name="sr"/> with a baked character frame set in
        /// <paramref name="color"/> and add a <see cref="CharacterAnimator"/> to drive it.
        /// </summary>
        public static void AttachCharacter(GameObject spriteChild, SpriteRenderer sr, Color color)
        {
            Sprite[] f = GetCharacterFrames(color);
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
        }

        /// <summary>Idle, Run0, Run1, Jump, Swing — baked (and cached) for the given team colour.</summary>
        public static Sprite[] GetCharacterFrames(Color color)
        {
            Directory.CreateDirectory(AbsPath(Dir));
            string key = ColorUtility.ToHtmlStringRGB(color);

            var frames = new Sprite[FrameNames.Length];
            for (int i = 0; i < FrameNames.Length; i++)
            {
                string assetPath = $"{Dir}/char_{key}_{FrameNames[i]}.png";
                if (!File.Exists(AbsPath(assetPath)))
                {
                    var tex = BakeFrame(i, color);
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

        static Texture2D BakeFrame(int frame, Color jersey)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var clear = new Color[W * H]; // default is (0,0,0,0)
            tex.SetPixels(clear);

            Pose p = GetPose(frame);
            // outline pass first (everything grown by 1px in dark), then the colour fill on top
            DrawFigure(tex, p, true, jersey);
            DrawFigure(tex, p, false, jersey);
            tex.Apply();
            return tex;
        }

        static void DrawFigure(Texture2D tex, Pose p, bool outline, Color jersey)
        {
            Color skin = new Color(0.93f, 0.74f, 0.55f);
            Color shorts = jersey * 0.45f; shorts.a = 1f;
            Color hair = new Color(0.18f, 0.13f, 0.10f);
            Color line = new Color(0.06f, 0.06f, 0.09f);
            int g = outline ? 1 : 0;
            float oy = p.bob;

            Color Skin() => outline ? line : skin;
            Color Jersey() => outline ? line : jersey;
            Color Shorts() => outline ? line : shorts;
            Color Hair() => outline ? line : hair;

            // right leg behind the torso; right arm behind too, unless the pose presents both
            // arms toward the camera (bump/set), in which case both arms are drawn in front.
            if (!p.armsInFront)
                Limb(tex, ShoulderR, p.elbowR, p.handR, 2, g, Skin(), oy);
            Limb(tex, HipR, p.kneeR, p.footR, 3, g, Skin(), oy);
            Limb(tex, HipL, p.kneeL, p.footL, 3, g, Skin(), oy);

            Box(tex, 16, 22, 32, 31, g, Shorts(), oy);                 // shorts
            Box(tex, 16, 30, 32, 49, g, Jersey(), oy);                // tank top / torso
            Box(tex, 18, 49, 30, 61, g, Skin(), oy, p.headDX);        // head
            Box(tex, 17, 58, 31, 63, g, Hair(), oy, p.headDX);        // hair cap

            if (p.armsInFront)                                          // both arms over the torso
                Limb(tex, ShoulderR, p.elbowR, p.handR, 2, g, Skin(), oy);
            Limb(tex, ShoulderL, p.elbowL, p.handL, 2, g, Skin(), oy); // front (left) arm
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
            if (x < 0 || x >= W || y < 0 || y >= H) return;
            tex.SetPixel(x, y, col);
        }

        static string AbsPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
