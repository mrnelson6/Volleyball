using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// A playable animal: gameplay stats plus the appearance that identifies them on court.
    /// Every animal's stats mirror the real creature so they read at a glance — the giraffe
    /// is tall, the cougar is fast, the buffalo is strong. Stats are multipliers around 1
    /// (the baseline athlete, roster range 0.70–1.35) and trade off against each other:
    /// <list type="bullet">
    /// <item><b>height</b> — how tall the animal is. Scales the baked sprite, the vertical
    /// hit window and the block reach, and tightens spike/block contacts. Short animals give
    /// all of that up.</item>
    /// <item><b>speed</b> — scales run speed and the dive lunge.</item>
    /// <item><b>power</b> — raw strength: scales the pace of driven spikes and blocks, so a
    /// buffalo hits heavier balls than a giraffe of the same contact height.</item>
    /// <item><b>control</b> — ball handling: divides the contact error on bumps, sets,
    /// serves and dive digs, so a high-control animal places touches where it aimed.</item>
    /// <item><b>jump</b> — jump height multiplier (applied as sqrt to the take-off speed so
    /// apex height scales linearly — the kangaroo jumps 35% higher, not 82%).</item>
    /// </list>
    /// Players reference a character by <see cref="id"/> (see <see cref="CharacterRoster"/>);
    /// the stats themselves live here in code so tuning one animal updates every scene.
    /// </summary>
    public class CharacterDef
    {
        public string id;
        public string displayName;
        public string blurb; // one-line real-animal trait, shown on the select screen

        /// <summary>Home region id (see RegionRoster), or "" for the protagonist duo.</summary>
        public string region = "";

        // ---- stats (1 = baseline) ----
        public float height = 1f;
        public float speed = 1f;
        public float power = 1f;
        public float control = 1f;
        public float jump = 1f;

        // ---- appearance (jersey colour stays per-team; these identify the animal) ----
        public Color fur;       // main fur/feather colour (head, limbs, tail)
        public Color furAccent; // muzzle/snout/beak/inner-ear accent
        public SpeciesArt art = new SpeciesArt();

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

    /// <summary>
    /// The built-in cast: the protagonist duo plus every regional animal on the world tour.
    /// Look up by id; unknown ids (including pre-animal legacy ids like "ace") fall back to
    /// the default protagonist.
    /// </summary>
    public static class CharacterRoster
    {
        public const string DefaultId = "fox";

        /// <summary>The player's fixed campaign character.</summary>
        public const string ProtagonistId = "fox";
        /// <summary>The player's fixed campaign teammate.</summary>
        public const string TeammateId = "bear";

        public static readonly CharacterDef[] All =
        {
            // ---------------------------------------------------------------- protagonists
            new CharacterDef
            {
                id = "fox", displayName = "Finn the Fox",
                blurb = "Quick and clever — a sharp first touch and sharper instincts.",
                height = 0.95f, speed = 1.15f, power = 0.95f, control = 1.10f, jump = 1.00f,
                fur = new Color(0.87f, 0.45f, 0.15f),
                furAccent = new Color(0.96f, 0.90f, 0.78f),
                art = new SpeciesArt { head = HeadShape.Muzzle, ears = EarStyle.Pointed, tail = 1.3f },
            },
            new CharacterDef
            {
                id = "bear", displayName = "Bruno the Bear",
                blurb = "Big paws, bigger spikes — owns the net, slow to the ball.",
                height = 1.18f, speed = 0.85f, power = 1.25f, control = 0.90f, jump = 0.85f,
                fur = new Color(0.45f, 0.30f, 0.18f),
                furAccent = new Color(0.70f, 0.55f, 0.38f),
                art = new SpeciesArt { head = HeadShape.Muzzle, ears = EarStyle.Round, tail = 0.3f },
            },

            // ---------------------------------------------------------------- sunny savanna
            new CharacterDef
            {
                id = "meerkat", displayName = "Pip the Meerkat", region = "savanna",
                blurb = "The tiny lookout — digs everything, but the net is far away up there.",
                height = 0.75f, speed = 1.20f, power = 0.75f, control = 1.10f, jump = 0.95f,
                fur = new Color(0.80f, 0.68f, 0.48f),
                furAccent = new Color(0.92f, 0.85f, 0.70f),
                art = new SpeciesArt { head = HeadShape.Muzzle, ears = EarStyle.Round, tail = 1.0f,
                                       markings = MarkingStyle.MaskPatch,
                                       markingColor = new Color(0.25f, 0.18f, 0.12f) },
            },
            new CharacterDef
            {
                id = "zebra", displayName = "Zuri the Zebra", region = "savanna",
                blurb = "Born sprinter — covers the court in a blur of stripes.",
                height = 1.05f, speed = 1.15f, power = 0.95f, control = 0.90f, jump = 1.00f,
                fur = new Color(0.92f, 0.92f, 0.92f),
                furAccent = new Color(0.35f, 0.33f, 0.33f),
                art = new SpeciesArt { head = HeadShape.LongMuzzle, ears = EarStyle.Pointed, tail = 1.0f,
                                       markings = MarkingStyle.Stripes,
                                       markingColor = new Color(0.12f, 0.12f, 0.13f) },
            },
            new CharacterDef
            {
                id = "warthog", displayName = "Waldo the Warthog", region = "savanna",
                blurb = "Charges straight through the ball — power first, aim later.",
                height = 0.90f, speed = 0.95f, power = 1.15f, control = 0.85f, jump = 0.90f,
                fur = new Color(0.55f, 0.45f, 0.38f),
                furAccent = new Color(0.70f, 0.60f, 0.52f),
                art = new SpeciesArt { head = HeadShape.LongMuzzle, ears = EarStyle.Pointed,
                                       horns = HornStyle.Tusks, tail = 1.0f },
            },
            new CharacterDef
            {
                id = "giraffe", displayName = "Gigi the Giraffe", region = "savanna",
                blurb = "Tallest animal on the tour — the net simply belongs to her.",
                height = 1.30f, speed = 0.80f, power = 1.00f, control = 0.85f, jump = 0.80f,
                fur = new Color(0.90f, 0.75f, 0.40f),
                furAccent = new Color(0.96f, 0.88f, 0.65f),
                art = new SpeciesArt { head = HeadShape.LongMuzzle, ears = EarStyle.Round,
                                       horns = HornStyle.Horns, neck = 1f, tail = 1.0f,
                                       markings = MarkingStyle.Spots,
                                       markingColor = new Color(0.55f, 0.35f, 0.15f) },
            },
            new CharacterDef
            {
                id = "lion", displayName = "Leo the Lion", region = "savanna",
                blurb = "King of the court — strong everywhere, weak nowhere.",
                height = 1.10f, speed = 1.05f, power = 1.15f, control = 0.95f, jump = 1.00f,
                fur = new Color(0.80f, 0.60f, 0.30f),
                furAccent = new Color(0.93f, 0.85f, 0.68f),
                art = new SpeciesArt { head = HeadShape.Muzzle, ears = EarStyle.Round, tail = 1.1f },
            },

            // ---------------------------------------------------------------- amazon rainforest
            new CharacterDef
            {
                id = "capybara", displayName = "Cabo the Capybara", region = "amazon",
                blurb = "The chillest animal alive — never rushed, never rattled, never misplaces a touch.",
                height = 0.95f, speed = 0.85f, power = 0.90f, control = 1.30f, jump = 0.85f,
                fur = new Color(0.60f, 0.45f, 0.28f),
                furAccent = new Color(0.72f, 0.58f, 0.40f),
                art = new SpeciesArt { head = HeadShape.Round, ears = EarStyle.Round, tail = 0f },
            },
            new CharacterDef
            {
                id = "toucan", displayName = "Tiko the Toucan", region = "amazon",
                blurb = "That famous beak sets the ball like a spoon — light, precise, airborne.",
                height = 0.85f, speed = 1.10f, power = 0.80f, control = 1.15f, jump = 1.10f,
                fur = new Color(0.15f, 0.15f, 0.18f),
                furAccent = new Color(0.95f, 0.55f, 0.10f), // the beak
                art = new SpeciesArt { head = HeadShape.Beak, ears = EarStyle.None, tail = 0.8f },
            },
            new CharacterDef
            {
                id = "sloth", displayName = "Susu the Sloth", region = "amazon",
                blurb = "Slowest player in the world — but give her time and the touch is perfect.",
                height = 0.90f, speed = 0.70f, power = 1.10f, control = 1.25f, jump = 0.75f,
                fur = new Color(0.55f, 0.50f, 0.40f),
                furAccent = new Color(0.80f, 0.74f, 0.60f),
                art = new SpeciesArt { head = HeadShape.Round, ears = EarStyle.None, tail = 0f,
                                       markings = MarkingStyle.MaskPatch,
                                       markingColor = new Color(0.30f, 0.25f, 0.18f) },
            },
            new CharacterDef
            {
                id = "jaguar", displayName = "Jax the Jaguar", region = "amazon",
                blurb = "The rainforest's apex sprinter — explodes to any ball.",
                height = 1.00f, speed = 1.30f, power = 1.10f, control = 0.90f, jump = 1.05f,
                fur = new Color(0.85f, 0.65f, 0.25f),
                furAccent = new Color(0.95f, 0.85f, 0.60f),
                art = new SpeciesArt { head = HeadShape.Muzzle, ears = EarStyle.Round, tail = 1.2f,
                                       markings = MarkingStyle.Spots,
                                       markingColor = new Color(0.20f, 0.14f, 0.08f) },
            },

            // ---------------------------------------------------------------- australian outback
            new CharacterDef
            {
                id = "wombat", displayName = "Wanda the Wombat", region = "outback",
                blurb = "A furry brick — low to the ground and built entirely of muscle.",
                height = 0.85f, speed = 0.90f, power = 1.20f, control = 1.00f, jump = 0.80f,
                fur = new Color(0.50f, 0.42f, 0.36f),
                furAccent = new Color(0.65f, 0.56f, 0.48f),
                art = new SpeciesArt { head = HeadShape.Round, ears = EarStyle.Round, tail = 0f },
            },
            new CharacterDef
            {
                id = "dingo", displayName = "Digger the Dingo", region = "outback",
                blurb = "Tireless desert runner — always exactly where the ball comes down.",
                height = 0.95f, speed = 1.15f, power = 0.95f, control = 1.00f, jump = 1.00f,
                fur = new Color(0.80f, 0.60f, 0.35f),
                furAccent = new Color(0.92f, 0.82f, 0.62f),
                art = new SpeciesArt { head = HeadShape.Muzzle, ears = EarStyle.Pointed, tail = 1.0f },
            },
            new CharacterDef
            {
                id = "emu", displayName = "Ezra the Emu", region = "outback",
                blurb = "Two metres of legs and feathers at a full sprint — can't be outrun.",
                height = 1.15f, speed = 1.25f, power = 0.85f, control = 0.80f, jump = 0.95f,
                fur = new Color(0.45f, 0.40f, 0.33f),
                furAccent = new Color(0.60f, 0.54f, 0.44f),
                art = new SpeciesArt { head = HeadShape.Beak, ears = EarStyle.None, neck = 0.6f, tail = 0.5f },
            },
            new CharacterDef
            {
                id = "kangaroo", displayName = "Kip the Kangaroo", region = "outback",
                blurb = "The highest jumper on Earth — spikes come down from the clouds.",
                height = 1.05f, speed = 1.10f, power = 1.05f, control = 0.85f, jump = 1.35f,
                fur = new Color(0.70f, 0.50f, 0.35f),
                furAccent = new Color(0.88f, 0.76f, 0.60f),
                art = new SpeciesArt { head = HeadShape.Muzzle, ears = EarStyle.Tall, tail = 1.4f },
            },

            // ---------------------------------------------------------------- himalayan peaks
            new CharacterDef
            {
                id = "redpanda", displayName = "Rumi the Red Panda", region = "himalaya",
                blurb = "Gentle paws and perfect balance from a life in the treetops.",
                height = 0.85f, speed = 1.05f, power = 0.80f, control = 1.25f, jump = 1.00f,
                fur = new Color(0.75f, 0.30f, 0.12f),
                furAccent = new Color(0.95f, 0.88f, 0.78f),
                art = new SpeciesArt { head = HeadShape.Round, ears = EarStyle.Round, tail = 1.3f,
                                       markings = MarkingStyle.MaskPatch,
                                       markingColor = new Color(0.95f, 0.88f, 0.78f) },
            },
            new CharacterDef
            {
                id = "yak", displayName = "Yara the Yak", region = "himalaya",
                blurb = "A shaggy mountain of muscle — every spike lands like an avalanche.",
                height = 1.15f, speed = 0.80f, power = 1.30f, control = 0.85f, jump = 0.75f,
                fur = new Color(0.30f, 0.22f, 0.16f),
                furAccent = new Color(0.55f, 0.48f, 0.42f),
                art = new SpeciesArt { head = HeadShape.LongMuzzle, ears = EarStyle.Droopy,
                                       horns = HornStyle.Horns, tail = 1.2f },
            },
            new CharacterDef
            {
                id = "markhor", displayName = "Mako the Markhor", region = "himalaya",
                blurb = "The cliff-hopping mountain goat — springs off nothing at all.",
                height = 1.00f, speed = 1.00f, power = 1.00f, control = 1.00f, jump = 1.20f,
                fur = new Color(0.65f, 0.58f, 0.48f),
                furAccent = new Color(0.82f, 0.76f, 0.66f),
                art = new SpeciesArt { head = HeadShape.LongMuzzle, ears = EarStyle.Droopy,
                                       horns = HornStyle.Horns, tail = 0.6f },
            },
            new CharacterDef
            {
                id = "snowleopard", displayName = "Sasha the Snow Leopard", region = "himalaya",
                blurb = "The ghost of the peaks — you won't see her reach the ball, but she will.",
                height = 1.00f, speed = 1.25f, power = 1.05f, control = 1.05f, jump = 1.15f,
                fur = new Color(0.80f, 0.80f, 0.82f),
                furAccent = new Color(0.94f, 0.94f, 0.95f),
                art = new SpeciesArt { head = HeadShape.Muzzle, ears = EarStyle.Round, tail = 1.4f,
                                       markings = MarkingStyle.Spots,
                                       markingColor = new Color(0.30f, 0.30f, 0.33f) },
            },

            // ---------------------------------------------------------------- black forest
            new CharacterDef
            {
                id = "hare", displayName = "Hazel the Hare", region = "forest",
                blurb = "Fastest feet in the forest — nothing drops on her side of the court.",
                height = 0.80f, speed = 1.35f, power = 0.75f, control = 1.00f, jump = 1.15f,
                fur = new Color(0.62f, 0.52f, 0.40f),
                furAccent = new Color(0.85f, 0.78f, 0.66f),
                art = new SpeciesArt { head = HeadShape.Muzzle, ears = EarStyle.Tall, tail = 0.3f },
            },
            new CharacterDef
            {
                id = "badger", displayName = "Bram the Badger", region = "forest",
                blurb = "Stocky, stubborn and immovable — digs like he was born for it. He was.",
                height = 0.85f, speed = 0.95f, power = 1.15f, control = 1.05f, jump = 0.85f,
                fur = new Color(0.45f, 0.45f, 0.48f),
                furAccent = new Color(0.90f, 0.90f, 0.88f),
                art = new SpeciesArt { head = HeadShape.Muzzle, ears = EarStyle.Round, tail = 0.4f,
                                       markings = MarkingStyle.MaskPatch,
                                       markingColor = new Color(0.12f, 0.12f, 0.14f) },
            },
            new CharacterDef
            {
                id = "boar", displayName = "Iggy the Boar", region = "forest",
                blurb = "Hits every ball like he's ramming an oak tree. Sometimes at the target.",
                height = 0.95f, speed = 1.00f, power = 1.25f, control = 0.80f, jump = 0.90f,
                fur = new Color(0.40f, 0.32f, 0.26f),
                furAccent = new Color(0.58f, 0.48f, 0.40f),
                art = new SpeciesArt { head = HeadShape.LongMuzzle, ears = EarStyle.Pointed,
                                       horns = HornStyle.Tusks, tail = 0.6f },
            },
            new CharacterDef
            {
                id = "stag", displayName = "Stellan the Stag", region = "forest",
                blurb = "Crowned in antlers — a wall at the net that the forest bows to.",
                height = 1.20f, speed = 1.05f, power = 1.10f, control = 0.95f, jump = 1.05f,
                fur = new Color(0.55f, 0.42f, 0.28f),
                furAccent = new Color(0.78f, 0.68f, 0.52f),
                art = new SpeciesArt { head = HeadShape.LongMuzzle, ears = EarStyle.Pointed,
                                       horns = HornStyle.Antlers, tail = 0.3f },
            },

            // ---------------------------------------------------------------- sahara dunes
            new CharacterDef
            {
                id = "jerboa", displayName = "Juju the Jerboa", region = "sahara",
                blurb = "A palm-sized desert spring — jumps ten times her own height.",
                height = 0.75f, speed = 1.25f, power = 0.70f, control = 1.05f, jump = 1.30f,
                fur = new Color(0.85f, 0.70f, 0.45f),
                furAccent = new Color(0.95f, 0.88f, 0.72f),
                art = new SpeciesArt { head = HeadShape.Round, ears = EarStyle.Tall, tail = 1.5f },
            },
            new CharacterDef
            {
                id = "fennec", displayName = "Fifi the Fennec", region = "sahara",
                blurb = "Those enormous ears hear exactly where the ball wants to land.",
                height = 0.80f, speed = 1.20f, power = 0.80f, control = 1.20f, jump = 1.00f,
                fur = new Color(0.90f, 0.78f, 0.55f),
                furAccent = new Color(0.97f, 0.92f, 0.80f),
                art = new SpeciesArt { head = HeadShape.Muzzle, ears = EarStyle.Tall, tail = 1.1f },
            },
            new CharacterDef
            {
                id = "oryx", displayName = "Orin the Oryx", region = "sahara",
                blurb = "Desert-forged and spear-horned — steady through any sandstorm.",
                height = 1.10f, speed = 1.00f, power = 1.10f, control = 0.95f, jump = 0.95f,
                fur = new Color(0.85f, 0.80f, 0.70f),
                furAccent = new Color(0.95f, 0.92f, 0.85f),
                art = new SpeciesArt { head = HeadShape.LongMuzzle, ears = EarStyle.Pointed,
                                       horns = HornStyle.Horns, tail = 0.8f,
                                       markings = MarkingStyle.MaskPatch,
                                       markingColor = new Color(0.25f, 0.20f, 0.16f) },
            },
            new CharacterDef
            {
                id = "camel", displayName = "Cleo the Camel", region = "sahara",
                blurb = "Tall, patient, untiring — the desert's original endurance athlete.",
                height = 1.25f, speed = 0.90f, power = 1.15f, control = 1.00f, jump = 0.75f,
                fur = new Color(0.78f, 0.62f, 0.40f),
                furAccent = new Color(0.90f, 0.78f, 0.58f),
                art = new SpeciesArt { head = HeadShape.LongMuzzle, ears = EarStyle.Round,
                                       neck = 0.5f, tail = 0.8f },
            },

            // ---------------------------------------------------------------- rocky mountains
            new CharacterDef
            {
                id = "raccoon", displayName = "Rocky the Raccoon", region = "rockies",
                blurb = "The cleverest paws in the mountains — nothing slips through them.",
                height = 0.85f, speed = 1.05f, power = 0.85f, control = 1.30f, jump = 1.00f,
                fur = new Color(0.50f, 0.50f, 0.53f),
                furAccent = new Color(0.78f, 0.78f, 0.80f),
                art = new SpeciesArt { head = HeadShape.Muzzle, ears = EarStyle.Round, tail = 1.2f,
                                       markings = MarkingStyle.MaskPatch,
                                       markingColor = new Color(0.10f, 0.10f, 0.12f) },
            },
            new CharacterDef
            {
                id = "moose", displayName = "Moe the Moose", region = "rockies",
                blurb = "Antlers wider than the net is high. Ducking is the ball's problem.",
                height = 1.28f, speed = 0.80f, power = 1.20f, control = 0.80f, jump = 0.75f,
                fur = new Color(0.38f, 0.28f, 0.20f),
                furAccent = new Color(0.55f, 0.44f, 0.34f),
                art = new SpeciesArt { head = HeadShape.LongMuzzle, ears = EarStyle.Droopy,
                                       horns = HornStyle.Antlers, tail = 0.2f },
            },
            new CharacterDef
            {
                id = "buffalo", displayName = "Butch the Buffalo", region = "rockies",
                blurb = "The strongest animal on tour — his spikes leave craters.",
                height = 1.10f, speed = 0.90f, power = 1.35f, control = 0.85f, jump = 0.80f,
                fur = new Color(0.35f, 0.25f, 0.18f),
                furAccent = new Color(0.22f, 0.16f, 0.12f),
                art = new SpeciesArt { head = HeadShape.LongMuzzle, ears = EarStyle.Round,
                                       horns = HornStyle.Horns, tail = 0.9f },
            },
            new CharacterDef
            {
                id = "cougar", displayName = "Cora the Cougar", region = "rockies",
                blurb = "The fastest cat in the mountains — everywhere at once.",
                height = 1.00f, speed = 1.35f, power = 1.05f, control = 0.95f, jump = 1.20f,
                fur = new Color(0.75f, 0.58f, 0.38f),
                furAccent = new Color(0.90f, 0.80f, 0.65f),
                art = new SpeciesArt { head = HeadShape.Muzzle, ears = EarStyle.Round, tail = 1.3f },
            },

            // ---------------------------------------------------------------- polar ice
            new CharacterDef
            {
                id = "penguin", displayName = "Pingo the Penguin", region = "arctic",
                blurb = "Can't jump, won't run — but the cleanest flippers in volleyball.",
                height = 0.80f, speed = 0.90f, power = 0.90f, control = 1.35f, jump = 0.80f,
                fur = new Color(0.10f, 0.12f, 0.16f),
                furAccent = new Color(0.95f, 0.75f, 0.20f), // the beak
                art = new SpeciesArt { head = HeadShape.Beak, ears = EarStyle.None, tail = 0.2f },
            },
            new CharacterDef
            {
                id = "snowyowl", displayName = "Ola the Snowy Owl", region = "arctic",
                blurb = "Silent wings and eyes that miss nothing on the whole court.",
                height = 0.85f, speed = 1.15f, power = 0.85f, control = 1.15f, jump = 1.10f,
                fur = new Color(0.92f, 0.92f, 0.95f),
                furAccent = new Color(0.30f, 0.28f, 0.25f), // the beak
                art = new SpeciesArt { head = HeadShape.Beak, ears = EarStyle.None, tail = 0.4f,
                                       markings = MarkingStyle.Spots,
                                       markingColor = new Color(0.55f, 0.55f, 0.58f) },
            },
            new CharacterDef
            {
                id = "walrus", displayName = "Wally the Walrus", region = "arctic",
                blurb = "A tonne of blubber behind every hit — just don't ask him to chase.",
                height = 1.10f, speed = 0.75f, power = 1.30f, control = 1.00f, jump = 0.70f,
                fur = new Color(0.60f, 0.42f, 0.35f),
                furAccent = new Color(0.75f, 0.60f, 0.52f),
                art = new SpeciesArt { head = HeadShape.Round, ears = EarStyle.None,
                                       horns = HornStyle.Tusks, tail = 0.3f },
            },
            new CharacterDef
            {
                id = "polarbear", displayName = "Boris the Polar Bear", region = "arctic",
                blurb = "The Arctic's undisputed heavyweight — tall, strong and very patient.",
                height = 1.20f, speed = 0.95f, power = 1.30f, control = 0.90f, jump = 0.90f,
                fur = new Color(0.92f, 0.90f, 0.85f),
                furAccent = new Color(0.98f, 0.97f, 0.94f),
                art = new SpeciesArt { head = HeadShape.Muzzle, ears = EarStyle.Round, tail = 0.2f },
            },

            // ---------------------------------------------------------------- world finals all-stars
            // (the finals reuse regional champions — no extra species needed)
        };

        public static CharacterDef Get(string id)
        {
            foreach (var c in All)
                if (c.id == id) return c;
            return All[0]; // unknown/legacy id → the default protagonist
        }
    }
}
