using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// A playable character archetype: gameplay stats plus the appearance that identifies them
    /// on court. Stats are multipliers around 1 (the baseline athlete) and trade off against
    /// each other — nobody is best at everything:
    /// <list type="bullet">
    /// <item><b>height</b> — how tall the character is. Scales the baked sprite, the vertical
    /// hit window and the block reach, tightens spike/block contacts, and directly scales the
    /// pace of driven spikes/blocks (a big wingspan makes work at the net easier and heavier).
    /// Short characters give all of that up.</item>
    /// <item><b>speed</b> — scales run speed and the dive lunge.</item>
    /// <item><b>control</b> — ball handling: divides the contact error on bumps, sets, serves
    /// and dive digs, so a high-control character places touches where they aimed.</item>
    /// </list>
    /// Players reference a character by <see cref="id"/> (see <see cref="CharacterRoster"/>);
    /// the stats themselves live here in code so tuning one character updates every scene.
    /// </summary>
    public class CharacterDef
    {
        public string id;
        public string displayName;
        public string blurb; // one-line archetype description, shown on the select screen

        // ---- stats (1 = baseline) ----
        public float height = 1f;
        public float speed = 1f;
        public float control = 1f;

        // ---- appearance (jersey colour stays per-team; these identify the person) ----
        public Color skin;
        public Color hair;

        /// <summary>
        /// The character's multiplier on the contact-error model for one hit type: work at the
        /// net (spike/block) is governed by height, everything else (bump/set/serve/dive) by
        /// control. &lt;1 = tighter than the baseline athlete.
        /// </summary>
        public float ErrorMult(HitType type)
        {
            switch (type)
            {
                case HitType.Spike:
                case HitType.Block: return 1f / Mathf.Max(height, 0.01f);
                default: return 1f / Mathf.Max(control, 0.01f);
            }
        }
    }

    /// <summary>The built-in cast. Look up by id; unknown ids fall back to the default.</summary>
    public static class CharacterRoster
    {
        public const string DefaultId = "ace";

        public static readonly CharacterDef[] All =
        {
            new CharacterDef
            {
                id = "ace", displayName = "Ace",
                blurb = "Balanced all-rounder — no weaknesses, no edge.",
                height = 1.00f, speed = 1.00f, control = 1.00f,
                skin = new Color(0.93f, 0.74f, 0.55f),
                hair = new Color(0.18f, 0.13f, 0.10f), // dark brown
            },
            new CharacterDef
            {
                id = "tower", displayName = "Tower",
                blurb = "Owns the net — spikes and blocks — but slow to the ball.",
                height = 1.16f, speed = 0.85f, control = 0.90f,
                skin = new Color(0.55f, 0.36f, 0.22f),
                hair = new Color(0.06f, 0.06f, 0.08f), // black
            },
            new CharacterDef
            {
                id = "bolt", displayName = "Bolt",
                blurb = "Covers the whole court, but small at the net.",
                height = 0.88f, speed = 1.25f, control = 0.95f,
                skin = new Color(0.87f, 0.62f, 0.41f),
                hair = new Color(0.95f, 0.83f, 0.45f), // blond
            },
            new CharacterDef
            {
                id = "sage", displayName = "Sage",
                blurb = "Surgical passes and sets from a modest athlete.",
                height = 0.95f, speed = 0.90f, control = 1.35f,
                skin = new Color(0.98f, 0.84f, 0.70f),
                hair = new Color(0.62f, 0.28f, 0.12f), // auburn
            },
            new CharacterDef
            {
                id = "rex", displayName = "Rex",
                blurb = "A skyscraper. Terrifying at the net, clumsy everywhere else.",
                height = 1.22f, speed = 0.80f, control = 0.85f,
                skin = new Color(0.80f, 0.58f, 0.38f),
                hair = new Color(0.75f, 0.76f, 0.78f), // silver
            },
            new CharacterDef
            {
                id = "dot", displayName = "Dot",
                blurb = "Tiny libero — quick feet and clean digs, no blocking game.",
                height = 0.82f, speed = 1.20f, control = 1.15f,
                skin = new Color(0.96f, 0.78f, 0.62f),
                hair = new Color(0.93f, 0.45f, 0.65f), // pink
            },
            new CharacterDef
            {
                id = "viper", displayName = "Viper",
                blurb = "Big, fast and reckless — wild on the easy touches.",
                height = 1.05f, speed = 1.15f, control = 0.80f,
                skin = new Color(0.72f, 0.55f, 0.36f),
                hair = new Color(0.20f, 0.60f, 0.35f), // green
            },
            new CharacterDef
            {
                id = "pearl", displayName = "Pearl",
                blurb = "Tall, unhurried setter — steady hands, heavy feet.",
                height = 1.08f, speed = 0.85f, control = 1.15f,
                skin = new Color(0.45f, 0.29f, 0.18f),
                hair = new Color(0.93f, 0.91f, 0.85f), // platinum
            },
        };

        public static CharacterDef Get(string id)
        {
            foreach (var c in All)
                if (c.id == id) return c;
            return All[0]; // unknown/legacy id → the default all-rounder
        }
    }
}
