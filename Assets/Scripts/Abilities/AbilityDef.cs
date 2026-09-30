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
