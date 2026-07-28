using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// The gameplay properties a regional court applies at match start: gravity, wind, ball
    /// drag and ambience. Applied by <see cref="CourtEnvironment"/>; purely-cosmetic theming
    /// stays in the editor's arena decorators.
    /// </summary>
    public class EnvironmentProfile
    {
        /// <summary>Multiplier on Physics.gravity (0.85 = thin Himalayan air).</summary>
        public float gravityScale = 1f;

        /// <summary>Constant acceleration on the ball in flight (m/s²). Keep the magnitude
        /// ≤ ~1 — the AI compensates for this constant part of the wind, but not for gusts.</summary>
        public Vector3 wind = Vector3.zero;

        /// <summary>0..0.4 — how strongly the wind gusts around its constant value. Gusts are
        /// invisible to the AI's landing prediction, so they read as human-like misjudgement;
        /// beyond 0.4 they read as the AI being broken.</summary>
        public float gustAmp = 0f;

        /// <summary>Rigidbody.linearDamping on the ball (humid/heavy air). Keep ≤ 0.3 — the
        /// AI's ballistic landing prediction ignores drag, and above that the error shows.</summary>
        public float ballDrag = 0f;

        /// <summary>GameAudio ambience flavour: surf / wind / jungle / rain / snow.</summary>
        public string ambience = "surf";

        /// <summary>One-line flavour shown on the tour board ("Thin mountain air…").</summary>
        public string bannerNote = "";

        public static readonly EnvironmentProfile Default = new EnvironmentProfile();
    }

    /// <summary>One match of a regional tournament: a named opponent duo plus its AI tuning.</summary>
    public class MatchDef
    {
        public string teamName;
        public string opp1Id, opp2Id; // roster ids from the region's species pool

        /// <summary>AI contact-error multiplier for this match (lower = sharper; the
        /// GameConfig default is 1.6). Overrides the global value via MatchSetup.</summary>
        public float aiErrorMult = 1.5f;

        /// <summary>Scales the AI's reaction latency window (lower = faster reactions).</summary>
        public float aiReactionScale = 1f;
    }

    /// <summary>
    /// One stop on the world tour: a themed court scene, the animals that live there, the
    /// tournament ladder you must clear, and the court's environmental properties.
    /// </summary>
    public class RegionDef
    {
        public string id;
        public string displayName;
        public string blurb;

        /// <summary>Scene name — the join key between this table, the editor scene builders
        /// and Build Settings. Falls back to the beach if the scene isn't built yet.</summary>
        public string sceneName;

        public string[] speciesPool;   // roster ids native to this region (for UI/flavour)
        public MatchDef[] matches;     // the tournament, in order; last = region champions
        public EnvironmentProfile env = EnvironmentProfile.Default;
    }

    /// <summary>
    /// The world tour ladder, in play order. Same "code as data" convention as
    /// <see cref="CharacterRoster"/> — lives in the runtime assembly so the campaign UI,
    /// MatchManager and the editor scene builders all read one table.
    /// </summary>
    public static class RegionRoster
    {
        public static readonly RegionDef[] All =
        {
            new RegionDef
            {
                id = "savanna", displayName = "Sunny Savanna",
                blurb = "Dusty grassland under an acacia sky — where the tour begins.",
                sceneName = "SavannaArena",
                speciesPool = new[] { "meerkat", "zebra", "warthog", "giraffe", "lion" },
                env = new EnvironmentProfile { ambience = "wind" },
                matches = new[]
                {
                    new MatchDef { teamName = "Dust Diggers", opp1Id = "meerkat", opp2Id = "warthog",
                                   aiErrorMult = 1.8f, aiReactionScale = 1.3f },
                    new MatchDef { teamName = "Stripe Sprinters", opp1Id = "zebra", opp2Id = "meerkat",
                                   aiErrorMult = 1.7f, aiReactionScale = 1.25f },
                    new MatchDef { teamName = "Pride of the Plains", opp1Id = "lion", opp2Id = "giraffe",
                                   aiErrorMult = 1.55f, aiReactionScale = 1.2f },
                },
            },
            new RegionDef
            {
                id = "amazon", displayName = "Amazon Rainforest",
                blurb = "A court deep under the canopy, where the air is thick enough to chew.",
                sceneName = "AmazonArena",
                speciesPool = new[] { "capybara", "toucan", "sloth", "jaguar" },
                env = new EnvironmentProfile
                {
                    ballDrag = 0.2f, ambience = "jungle",
                    bannerNote = "Humid jungle air — the ball dies fast.",
                },
                matches = new[]
                {
                    new MatchDef { teamName = "Slow and Steady", opp1Id = "sloth", opp2Id = "capybara",
                                   aiErrorMult = 1.6f, aiReactionScale = 1.2f },
                    new MatchDef { teamName = "Canopy Crew", opp1Id = "toucan", opp2Id = "capybara",
                                   aiErrorMult = 1.5f, aiReactionScale = 1.15f },
                    new MatchDef { teamName = "River Kings", opp1Id = "jaguar", opp2Id = "toucan",
                                   aiErrorMult = 1.4f, aiReactionScale = 1.1f },
                },
            },
            new RegionDef
            {
                id = "outback", displayName = "Australian Outback",
                blurb = "Red rock, big sky, and a crosswind with opinions of its own.",
                sceneName = "OutbackArena",
                speciesPool = new[] { "wombat", "dingo", "emu", "kangaroo" },
                env = new EnvironmentProfile
                {
                    wind = new Vector3(0.8f, 0f, 0f), gustAmp = 0.3f, ambience = "wind",
                    bannerNote = "Gusty crosswind — watch your serves drift.",
                },
                matches = new[]
                {
                    new MatchDef { teamName = "Burrow Brigade", opp1Id = "wombat", opp2Id = "dingo",
                                   aiErrorMult = 1.5f, aiReactionScale = 1.15f },
                    new MatchDef { teamName = "Dust Runners", opp1Id = "emu", opp2Id = "dingo",
                                   aiErrorMult = 1.45f, aiReactionScale = 1.12f },
                    new MatchDef { teamName = "Red Rock Rollers", opp1Id = "wombat", opp2Id = "emu",
                                   aiErrorMult = 1.4f, aiReactionScale = 1.1f },
                    new MatchDef { teamName = "The Boomers", opp1Id = "kangaroo", opp2Id = "emu",
                                   aiErrorMult = 1.3f, aiReactionScale = 1.05f },
                },
            },
            new RegionDef
            {
                id = "himalaya", displayName = "Himalayan Peaks",
                blurb = "The highest court on Earth, strung with prayer flags.",
                sceneName = "HimalayaArena",
                speciesPool = new[] { "redpanda", "yak", "markhor", "snowleopard" },
                env = new EnvironmentProfile
                {
                    gravityScale = 0.85f, ambience = "snow",
                    bannerNote = "Thin mountain air — everything floats.",
                },
                matches = new[]
                {
                    new MatchDef { teamName = "Treeline Twins", opp1Id = "redpanda", opp2Id = "markhor",
                                   aiErrorMult = 1.4f, aiReactionScale = 1.1f },
                    new MatchDef { teamName = "Base Camp Bruisers", opp1Id = "yak", opp2Id = "redpanda",
                                   aiErrorMult = 1.35f, aiReactionScale = 1.08f },
                    new MatchDef { teamName = "Cliff Dancers", opp1Id = "yak", opp2Id = "markhor",
                                   aiErrorMult = 1.3f, aiReactionScale = 1.05f },
                    new MatchDef { teamName = "Ghosts of the Peaks", opp1Id = "snowleopard", opp2Id = "markhor",
                                   aiErrorMult = 1.2f, aiReactionScale = 1f },
                },
            },
            new RegionDef
            {
                id = "forest", displayName = "Black Forest",
                blurb = "A mossy clearing between old pines, kept damp by a permanent drizzle.",
                sceneName = "ForestArena",
                speciesPool = new[] { "hare", "badger", "boar", "stag" },
                env = new EnvironmentProfile
                {
                    ballDrag = 0.15f, ambience = "rain",
                    bannerNote = "Forest drizzle — heavy, damp air.",
                },
                matches = new[]
                {
                    new MatchDef { teamName = "Hedgerow Heroes", opp1Id = "hare", opp2Id = "badger",
                                   aiErrorMult = 1.35f, aiReactionScale = 1.08f },
                    new MatchDef { teamName = "Rooters", opp1Id = "boar", opp2Id = "badger",
                                   aiErrorMult = 1.3f, aiReactionScale = 1.05f },
                    new MatchDef { teamName = "Thicket Flickers", opp1Id = "hare", opp2Id = "boar",
                                   aiErrorMult = 1.25f, aiReactionScale = 1.02f },
                    new MatchDef { teamName = "Crown of the Forest", opp1Id = "stag", opp2Id = "hare",
                                   aiErrorMult = 1.15f, aiReactionScale = 1f },
                },
            },
            new RegionDef
            {
                id = "sahara", displayName = "Sahara Dunes",
                blurb = "An oasis court between the dunes; the sand never quite stays down.",
                sceneName = "SaharaArena",
                speciesPool = new[] { "jerboa", "fennec", "oryx", "camel" },
                env = new EnvironmentProfile
                {
                    wind = new Vector3(1.0f, 0f, 0.4f), gustAmp = 0.4f, ambience = "wind",
                    bannerNote = "Sandstorm gusts — the ball drifts mid-air.",
                },
                matches = new[]
                {
                    new MatchDef { teamName = "Dune Hoppers", opp1Id = "jerboa", opp2Id = "fennec",
                                   aiErrorMult = 1.3f, aiReactionScale = 1.05f },
                    new MatchDef { teamName = "Oasis Guard", opp1Id = "oryx", opp2Id = "fennec",
                                   aiErrorMult = 1.25f, aiReactionScale = 1.02f },
                    new MatchDef { teamName = "Sand Spears", opp1Id = "jerboa", opp2Id = "oryx",
                                   aiErrorMult = 1.2f, aiReactionScale = 1f },
                    new MatchDef { teamName = "Mirage", opp1Id = "camel", opp2Id = "jerboa",
                                   aiErrorMult = 1.1f, aiReactionScale = 0.98f },
                },
            },
            new RegionDef
            {
                id = "rockies", displayName = "Rocky Mountains",
                blurb = "A pine-ringed court below the snowline, with a storm wind down the valley.",
                sceneName = "RockiesArena",
                speciesPool = new[] { "raccoon", "moose", "buffalo", "cougar" },
                env = new EnvironmentProfile
                {
                    wind = new Vector3(0f, 0f, 0.7f), gustAmp = 0.25f, ambience = "wind",
                    bannerNote = "Wind straight down the court — serves sail long or die short.",
                },
                matches = new[]
                {
                    new MatchDef { teamName = "Campsite Bandits", opp1Id = "raccoon", opp2Id = "moose",
                                   aiErrorMult = 1.25f, aiReactionScale = 1.02f },
                    new MatchDef { teamName = "Timberline", opp1Id = "buffalo", opp2Id = "raccoon",
                                   aiErrorMult = 1.2f, aiReactionScale = 1f },
                    new MatchDef { teamName = "The Stampede", opp1Id = "moose", opp2Id = "buffalo",
                                   aiErrorMult = 1.15f, aiReactionScale = 0.98f },
                    new MatchDef { teamName = "Peak Predators", opp1Id = "cougar", opp2Id = "buffalo",
                                   aiErrorMult = 1.05f, aiReactionScale = 0.95f },
                },
            },
            new RegionDef
            {
                id = "arctic", displayName = "Polar Ice",
                blurb = "A court on the floes under the aurora. Bring a scarf.",
                sceneName = "ArcticArena",
                speciesPool = new[] { "penguin", "snowyowl", "walrus", "polarbear" },
                env = new EnvironmentProfile
                {
                    gravityScale = 0.95f, ambience = "snow",
                    bannerNote = "Polar chill — a touch of float on every ball.",
                },
                matches = new[]
                {
                    new MatchDef { teamName = "Floe Flippers", opp1Id = "penguin", opp2Id = "snowyowl",
                                   aiErrorMult = 1.15f, aiReactionScale = 0.98f },
                    new MatchDef { teamName = "Blubber Bros", opp1Id = "walrus", opp2Id = "penguin",
                                   aiErrorMult = 1.1f, aiReactionScale = 0.96f },
                    new MatchDef { teamName = "Night Watch", opp1Id = "snowyowl", opp2Id = "walrus",
                                   aiErrorMult = 1.05f, aiReactionScale = 0.94f },
                    new MatchDef { teamName = "The Ice Kings", opp1Id = "polarbear", opp2Id = "walrus",
                                   aiErrorMult = 1.0f, aiReactionScale = 0.92f },
                },
            },
            new RegionDef
            {
                id = "skyfinals", displayName = "Cloud Kingdom Finals",
                blurb = "The World Finals, held above the weather itself. Champions only.",
                sceneName = SceneFlow.SkyArena, // reuses the existing fantasy arena
                speciesPool = new[] { "snowleopard", "jaguar", "kangaroo", "cougar", "lion", "polarbear" },
                env = new EnvironmentProfile
                {
                    gravityScale = 0.9f, ambience = "wind",
                    bannerNote = "Cloud-high finale — hang time for days.",
                },
                matches = new[]
                {
                    new MatchDef { teamName = "Spot Squad", opp1Id = "snowleopard", opp2Id = "jaguar",
                                   aiErrorMult = 1.0f, aiReactionScale = 0.92f },
                    new MatchDef { teamName = "Spring Loaded", opp1Id = "kangaroo", opp2Id = "cougar",
                                   aiErrorMult = 0.95f, aiReactionScale = 0.9f },
                    new MatchDef { teamName = "Kings of the Wild", opp1Id = "lion", opp2Id = "polarbear",
                                   aiErrorMult = 0.9f, aiReactionScale = 0.85f },
                },
            },
        };

        /// <summary>Region by ladder index, clamped to the valid range.</summary>
        public static RegionDef Get(int index) => All[Mathf.Clamp(index, 0, All.Length - 1)];

        /// <summary>The region whose court is the given scene, or null (menu, fantasy arenas).</summary>
        public static RegionDef BySceneName(string sceneName)
        {
            foreach (var r in All)
                if (r.sceneName == sceneName) return r;
            return null;
        }

        /// <summary>Total number of matches on the whole tour.</summary>
        public static int TotalMatches
        {
            get { int n = 0; foreach (var r in All) n += r.matches.Length; return n; }
        }
    }
}
