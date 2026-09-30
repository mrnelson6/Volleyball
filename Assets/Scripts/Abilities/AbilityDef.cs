using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Every character's signature ability (the per-animal replacement for the old stat-multiplier
    /// power-ups, <see cref="PowerUpType"/>). A character with <see cref="AbilityId.None"/> still
    /// uses its legacy power-up — the roster migrates region by region.
    /// </summary>
    public enum AbilityId : byte
    {
        None,
        // Beach
        FoxTrick, BearSlam,
        // Savanna
        Burrow, Stampede, MudWallow, TallOrder, Roar, Charge,
        // Amazon
        HotSpring, BananaBall, SlowMo, Pounce,
        // Outback
        CubeDrop, PackHunt, BigStride, Trampoline,
        // Himalaya
        Balance, Avalanche, CliffHop, PhantomStrike,
        // Forest
        BlinkHop, TunnelTrap, Rampage, AntlerParry,
        // Sahara
        DoubleJump, SoundBlast, SandstormDevil, Oasis,
        // Rockies
        StickyPaws, WideLoad, Earthquake, NetWalker,
        // Arctic
        IceRink, Glide, Iceberg, Blizzard,
    }

    /// <summary>
    /// What an ability decided when it fired, replicated to every client so they build the
    /// same thing (the stampede's lane, the mud's spot, the charge's direction...). Only the
    /// authority plans; the meaning of each field is the ability's own business.
    /// </summary>
    public struct AbilityParams : Unity.Netcode.INetworkSerializable
    {
        public Vector3 a;
        public Vector3 b;
        public float f;
        public int seed;
        /// <summary>Simulation tick the ability fired on — the time base for anything the
        /// player simulation reads (zones), identical on every machine.</summary>
        public int startTick;

        public void NetworkSerialize<T>(Unity.Netcode.BufferSerializer<T> serializer)
            where T : Unity.Netcode.IReaderWriter
        {
            serializer.SerializeValue(ref a);
            serializer.SerializeValue(ref b);
            serializer.SerializeValue(ref f);
            serializer.SerializeValue(ref seed);
            serializer.SerializeValue(ref startTick);
        }
    }

    /// <summary>One ability's identity and tuning, plus the factory for its behaviour.</summary>
    public class AbilityDef
    {
        public AbilityId id;
        public string displayName;
        public string blurb;        // one line for the select screen
        public string bannerText;   // the on-activation shout
        public Color color;
        /// <summary>How long the HUD shows it running (armed abilities: how long they stay armed).</summary>
        public float duration = 6f;
        public System.Func<Ability> create;
    }

    /// <summary>All abilities, looked up by id. The animal → ability mapping lives on each
    /// <see cref="CharacterDef"/> roster entry.</summary>
    public static class AbilityRoster
    {
        public static readonly AbilityDef[] All =
        {
            new AbilityDef
            {
                id = AbilityId.FoxTrick, displayName = "Fox Trick",
                blurb = "Your next shot over splits in two — only one ball is real.",
                bannerText = "FOX TRICK!", color = new Color(1.00f, 0.55f, 0.15f), duration = 10f,
                create = () => new FoxTrickAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.BearSlam, displayName = "Bear Slam",
                blurb = "Land from a jump with a shockwave that flattens anyone near the net.",
                bannerText = "BEAR SLAM!", color = new Color(0.80f, 0.45f, 0.20f), duration = 8f,
                create = () => new BearSlamAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.Burrow, displayName = "Burrow",
                blurb = "Dive underground and pop up right where the ball is coming down.",
                bannerText = "BURROW!", color = new Color(0.85f, 0.70f, 0.40f), duration = 0.6f,
                create = () => new BurrowAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.Stampede, displayName = "Stampede",
                blurb = "A zebra herd thunders across their court — get out of the lane!",
                bannerText = "STAMPEDE!", color = new Color(0.95f, 0.95f, 0.95f), duration = 2.4f,
                create = () => new StampedeAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.MudWallow, displayName = "Mud Wallow",
                blurb = "Turn part of their court into a sticky mud pit.",
                bannerText = "MUD WALLOW!", color = new Color(0.55f, 0.38f, 0.22f), duration = 7f,
                create = () => new MudWallowAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.TallOrder, displayName = "Tall Order",
                blurb = "The net shoots up a metre — good luck spiking over that.",
                bannerText = "TALL ORDER!", color = new Color(1.00f, 0.80f, 0.30f), duration = 6f,
                create = () => new TallOrderAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.Roar, displayName = "Roar",
                blurb = "A mighty roar stuns everyone in front of you and rattles their next touch.",
                bannerText = "ROAR!", color = new Color(0.95f, 0.65f, 0.20f), duration = 3f,
                create = () => new RoarAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.Charge, displayName = "Charge",
                blurb = "Bulldoze across your court — hit the ball on the way for a free power shot.",
                bannerText = "CHARGE!", color = new Color(0.70f, 0.72f, 0.78f), duration = 0.75f,
                create = () => new ChargeAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.HotSpring, displayName = "Hot Spring",
                blurb = "A steaming pool on your side: touches in it are near-perfect and balls float down over it.",
                bannerText = "HOT SPRING!", color = new Color(0.35f, 0.85f, 0.85f), duration = 7f,
                create = () => new HotSpringAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.BananaBall, displayName = "Banana Ball",
                blurb = "Your next shot over swerves hard late in its flight.",
                bannerText = "BANANA BALL!", color = new Color(1.00f, 0.85f, 0.20f), duration = 10f,
                create = () => new BananaBallAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.SlowMo, displayName = "Slow-Mo",
                blurb = "The ball alone drops to 40% speed for a few seconds.",
                bannerText = "SLOW-MO...", color = new Color(0.55f, 0.85f, 0.55f), duration = 3f,
                create = () => new SlowMoAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.Pounce, displayName = "Pounce",
                blurb = "Leap from anywhere on your side straight to the ball and play it.",
                bannerText = "POUNCE!", color = new Color(1.00f, 0.75f, 0.25f), duration = 1.2f,
                create = () => new PounceAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.CubeDrop, displayName = "Cube Drop",
                blurb = "Three sturdy cubes thump down on their court. Wombats really do this.",
                bannerText = "CUBE DROP!", color = new Color(0.55f, 0.38f, 0.22f), duration = 7f,
                create = () => new CubeDropAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.PackHunt, displayName = "Pack Hunt",
                blurb = "A ghost dingo joins your side and plays one ball for you.",
                bannerText = "PACK HUNT!", color = new Color(0.55f, 0.85f, 1.00f), duration = 8f,
                create = () => new PackHuntAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.BigStride, displayName = "Big Stride",
                blurb = "Faster runs, and every jump becomes a huge bound.",
                bannerText = "BIG STRIDE!", color = new Color(0.60f, 0.50f, 0.40f), duration = 8f,
                create = () => new BigStrideAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.Trampoline, displayName = "Trampoline",
                blurb = "A bounce pad under you: your team jumps much higher off it.",
                bannerText = "TRAMPOLINE!", color = new Color(0.30f, 0.50f, 1.00f), duration = 8f,
                create = () => new TrampolineAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.Balance, displayName = "Balance",
                blurb = "Your next pass hangs frozen at its peak for a second.",
                bannerText = "BALANCE!", color = new Color(0.55f, 0.85f, 0.35f), duration = 10f,
                create = () => new BalanceAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.Avalanche, displayName = "Avalanche",
                blurb = "Giant snowballs roll through their back court.",
                bannerText = "AVALANCHE!", color = new Color(0.85f, 0.93f, 1.00f), duration = 2.8f,
                create = () => new AvalancheAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.CliffHop, displayName = "Cliff Hop",
                blurb = "A rock ledge bursts up under you: +1.25m for spikes and blocks.",
                bannerText = "CLIFF HOP!", color = new Color(0.60f, 0.56f, 0.50f), duration = 7f,
                create = () => new CliffHopAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.PhantomStrike, displayName = "Phantom Strike",
                blurb = "Your next spike goes nearly invisible: no shadow, no trail.",
                bannerText = "PHANTOM STRIKE!", color = new Color(0.80f, 0.85f, 0.95f), duration = 10f,
                create = () => new PhantomStrikeAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.BlinkHop, displayName = "Blink Hop",
                blurb = "Three instant hops: press the button again to blink along the stick.",
                bannerText = "BLINK HOP!", color = new Color(0.80f, 0.70f, 1.00f), duration = 6f,
                create = () => new BlinkHopAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.TunnelTrap, displayName = "Tunnel Trap",
                blurb = "Holes open in their court: step in one and you are stuck.",
                bannerText = "TUNNEL TRAP!", color = new Color(0.45f, 0.32f, 0.20f), duration = 8f,
                create = () => new TunnelTrapAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.Rampage, displayName = "Rampage",
                blurb = "Whoever digs your next spike gets flattened straight after.",
                bannerText = "RAMPAGE!", color = new Color(0.70f, 0.40f, 0.25f), duration = 10f,
                create = () => new RampageAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.AntlerParry, displayName = "Antler Parry",
                blurb = "Balls crossing the net near you get swatted straight back. Wildly.",
                bannerText = "ANTLER PARRY!", color = new Color(0.65f, 0.50f, 0.30f), duration = 5f,
                create = () => new AntlerParryAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.DoubleJump, displayName = "Double Jump",
                blurb = "Jump again in mid-air for the highest reach on the tour.",
                bannerText = "DOUBLE JUMP!", color = new Color(1.00f, 0.85f, 0.50f), duration = 8f,
                create = () => new DoubleJumpAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.SoundBlast, displayName = "Sound Blast",
                blurb = "A shockwave from those ears shoves the ball in flight.",
                bannerText = "SOUND BLAST!", color = new Color(1.00f, 0.80f, 0.45f), duration = 0.6f,
                create = () => new SoundBlastAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.SandstormDevil, displayName = "Sandstorm Devil",
                blurb = "A whirlwind wanders their court, flinging players and the ball.",
                bannerText = "SANDSTORM DEVIL!", color = new Color(0.90f, 0.75f, 0.45f), duration = 7f,
                create = () => new SandstormDevilAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.Oasis, displayName = "Oasis",
                blurb = "A pool on your side: the first ball to land in it splashes back up.",
                bannerText = "OASIS!", color = new Color(0.30f, 0.65f, 0.95f), duration = 8f,
                create = () => new OasisAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.StickyPaws, displayName = "Sticky Paws",
                blurb = "Catch the next ball that comes to you, then throw it anywhere.",
                bannerText = "STICKY PAWS!", color = new Color(0.55f, 0.55f, 0.60f), duration = 8f,
                create = () => new StickyPawsAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.WideLoad, displayName = "Wide Load",
                blurb = "Enormous solid antlers: balls glance off and your block covers half the net.",
                bannerText = "WIDE LOAD!", color = new Color(0.60f, 0.45f, 0.28f), duration = 6f,
                create = () => new WideLoadAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.Earthquake, displayName = "Earthquake",
                blurb = "Their court shakes: nobody over there can jump.",
                bannerText = "EARTHQUAKE!", color = new Color(0.55f, 0.40f, 0.25f), duration = 3.5f,
                create = () => new EarthquakeAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.NetWalker, displayName = "Net Walker",
                blurb = "Prowl along the top of the net and play any ball near it.",
                bannerText = "NET WALKER!", color = new Color(0.95f, 0.75f, 0.45f), duration = 5f,
                create = () => new NetWalkerAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.IceRink, displayName = "Ice Rink",
                blurb = "Their court freezes over: no grip, no stopping.",
                bannerText = "ICE RINK!", color = new Color(0.70f, 0.90f, 1.00f), duration = 6f,
                create = () => new IceRinkAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.Glide, displayName = "Glide",
                blurb = "Hold jump in the air to float down slowly.",
                bannerText = "GLIDE!", color = new Color(0.95f, 0.95f, 1.00f), duration = 8f,
                create = () => new GlideAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.Iceberg, displayName = "Iceberg",
                blurb = "A solid wall of ice erupts at your net: attacks bounce off it.",
                bannerText = "ICEBERG!", color = new Color(0.70f, 0.90f, 1.00f), duration = 6f,
                create = () => new IcebergAbility(),
            },
            new AbilityDef
            {
                id = AbilityId.Blizzard, displayName = "Blizzard",
                blurb = "A gale toward their baseline: your shots carry, theirs fall short.",
                bannerText = "BLIZZARD!", color = new Color(0.90f, 0.95f, 1.00f), duration = 6f,
                create = () => new BlizzardAbility(),
            },
        };

        public static AbilityDef Get(AbilityId id)
        {
            if (id == AbilityId.None) return null;
            foreach (var d in All)
                if (d.id == id) return d;
            return null;
        }
    }
}
