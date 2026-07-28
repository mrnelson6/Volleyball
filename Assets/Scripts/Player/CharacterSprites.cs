using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// The four on-court jersey colours (one per player slot). Shared by the editor scene
    /// builders (which bake sprites in these colours) and runtime code (which loads them back).
    /// </summary>
    public static class PlayerColors
    {
        public static readonly Color Human = new Color(0.20f, 0.50f, 0.95f); // you (blue)
        public static readonly Color Mate = new Color(0.45f, 0.80f, 1.00f);  // teammate (cyan)
        public static readonly Color Opp1 = new Color(0.95f, 0.30f, 0.25f);  // opponent (red)
        public static readonly Color Opp2 = new Color(0.98f, 0.60f, 0.20f);  // opponent (orange)
    }

    /// <summary>
    /// Runtime access to the baked character frame sets. The editor's CharacterArt bakes one
    /// PNG per (jersey colour × roster character × frame) into <c>Assets/Resources/Characters/</c>
    /// using the names defined here, so a player's look can be swapped at runtime — which is what
    /// lets you pick a character on the menu and have the already-built arena re-dress itself.
    /// </summary>
    public static class CharacterSprites
    {
        /// <summary>Folder under Resources/ the frames are baked into.</summary>
        public const string ResourceFolder = "Characters";

        public const int BaseCanvasWidth = 48;
        public const int BaseCanvasHeight = 64;      // scaled by the character's height stat
        public const float PixelsPerUnit = 34f;      // ~1.8 world units tall at height 1

        /// <summary>Frame names, in the order CharacterAnimator consumes them.</summary>
        public static readonly string[] FrameNames =
            { "idle", "run0", "run1", "jump", "swing", "bump", "set", "block", "dive",
              "diveUp", "diveDown" };

        /// <summary>Asset base name for one baked frame (no folder, no extension).</summary>
        public static string FrameName(Color jersey, string characterId, string frame)
            => $"char_{ColorUtility.ToHtmlStringRGB(jersey)}_{characterId}_{frame}";

        /// <summary>Sprite child localPosition that plants this character's feet on the ground.</summary>
        public static Vector3 SpriteLocalPosFor(CharacterDef ch)
        {
            float worldHeight = Mathf.RoundToInt(BaseCanvasHeight * ch.height) / PixelsPerUnit;
            return new Vector3(0f, worldHeight * 0.5f - 0.04f, 0f);
        }

        /// <summary>Load the full frame set for one jersey colour + character, or null if the
        /// sprites were never baked (run a scene builder to bake them).</summary>
        public static Sprite[] LoadFrames(Color jersey, CharacterDef ch)
        {
            var frames = new Sprite[FrameNames.Length];
            for (int i = 0; i < FrameNames.Length; i++)
            {
                frames[i] = Resources.Load<Sprite>($"{ResourceFolder}/{FrameName(jersey, ch.id, FrameNames[i])}");
                if (frames[i] == null)
                {
                    Debug.LogWarning($"[Volleyball] Missing baked character sprite " +
                                     $"'{FrameName(jersey, ch.id, FrameNames[i])}' — re-run a scene " +
                                     "builder to bake the roster. Keeping the current look.");
                    return null;
                }
            }
            return frames;
        }

        /// <summary>
        /// Turn <paramref name="player"/> into <paramref name="ch"/>: stats (via characterId),
        /// baked sprites in the player's jersey colour, feet-on-ground sprite offset, and shadow
        /// size. Safe no-op on the visuals if the frames aren't baked; stats always apply.
        /// </summary>
        public static void Apply(VolleyPlayer player, CharacterDef ch)
        {
            player.characterId = ch.id;

            var anim = player.GetComponentInChildren<CharacterAnimator>();
            Sprite[] f = anim != null ? LoadFrames(player.jerseyColor, ch) : null;
            if (f != null)
            {
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

                var sr = anim.GetComponent<SpriteRenderer>();
                if (sr != null) sr.sprite = f[0];

                anim.transform.localPosition = SpriteLocalPosFor(ch);
                anim.CaptureBaseLocalY();
            }

            // the blob shadow lives on its own object, targeting the player
            foreach (var ds in Object.FindObjectsByType<DropShadow>(FindObjectsSortMode.None))
                if (ds.target == player.transform)
                    ds.baseSize = 1.0f * ch.height;

            VBLog.Event($"CHARACTER '{player.name}' -> {ch.displayName} " +
                        $"(h={ch.height:F2} s={ch.speed:F2} p={ch.power:F2} " +
                        $"c={ch.control:F2} j={ch.jump:F2})");
        }
    }
}
