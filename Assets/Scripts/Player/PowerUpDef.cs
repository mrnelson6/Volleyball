using UnityEngine;

namespace Volleyball
{
    /// <summary>The pool of signature power-ups. Every roster animal is assigned one
    /// (see <see cref="CharacterDef.powerUp"/>); several animals can share a type.</summary>
    public enum PowerUpType
    {
        ThunderSpike, DeepFreeze, GiantGrowth, CycloneServe, GoldenTouch,
        WildfireSprint, MoonBall, SkyJump, LongReach, Sandstorm,
    }

    /// <summary>When an AI player with a full meter should fire its power-up, so offensive
    /// buffs land right before an attack and defensive ones when the opponents are attacking.</summary>
    public enum PowerAiCue { OwnAttack, OwnServe, OwnPossession, OpponentAttack, Anytime }

    /// <summary>
    /// One power-up: an arcade-crazy timed effect a player fires once their meter is full.
    /// Effects are expressed as multipliers consumed by the existing gameplay hooks — the
    /// player stat getters, the contact-error model, the driven-shot pace in
    /// <see cref="BallController.LaunchTo"/>, and the global wind/gravity channel in
    /// <see cref="PowerUpDirector"/>. Every multiplier defaults to 1 (no effect), so each
    /// entry only states what it changes. All effects end early when the rally ends.
    /// </summary>
    public class PowerUpDef
    {
        public PowerUpType type;
        public string displayName;
        public string blurb;       // one-line description, shown on the select screen
        public string bannerText;  // the on-activation shout, e.g. "THUNDER SPIKE!"
        public float duration = 6f;
        public Color color;        // identity colour: sprite glow + HUD meter tint
        public PowerAiCue aiCue = PowerAiCue.Anytime;

        // ---- self buffs (applied to the caster while active) ----
        public float moveMult = 1f;        // run + dive speed
        public float jumpMult = 1f;        // jump take-off speed
        public float reachMult = 1f;       // horizontal hit reach
        public float reachHeightMult = 1f; // vertical hit window
        public float blockReachMult = 1f;  // block engage radius
        public float attackPaceMult = 1f;  // driven spike/block pace
        public float selfErrorMult = 1f;   // own contact error (<1 = tighter)
        public float spriteScale = 1f;     // visual growth (Giant Growth)

        // ---- opponent debuffs (inflicted on both opposing players while active) ----
        public float oppMoveMult = 1f;
        public float oppErrorMult = 1f;

        // ---- global effects (the whole court, reverted when the effect ends) ----
        public float gravityMult = 1f;
        public Vector3 extraWind = Vector3.zero;
    }

    /// <summary>The ten power-ups. Look up by type; the mapping from animal to power-up
    /// lives on each <see cref="CharacterDef"/> roster entry.</summary>
    public static class PowerUpRoster
    {
        public static readonly PowerUpDef[] All =
        {
            new PowerUpDef
            {
                type = PowerUpType.ThunderSpike, displayName = "Thunder Spike",
                blurb = "Attacks crash down with frightening pace and dead aim.",
                bannerText = "THUNDER SPIKE!",
                duration = 8f, color = new Color(1.00f, 0.92f, 0.25f),
                aiCue = PowerAiCue.OwnAttack,
                attackPaceMult = 1.5f, selfErrorMult = 0.6f,
            },
            new PowerUpDef
            {
                type = PowerUpType.DeepFreeze, displayName = "Deep Freeze",
                blurb = "Freezes the other team down to a crawl.",
                bannerText = "DEEP FREEZE!",
                duration = 3.5f, color = new Color(0.50f, 0.85f, 1.00f),
                aiCue = PowerAiCue.OwnAttack,
                oppMoveMult = 0.25f,
            },
            new PowerUpDef
            {
                type = PowerUpType.GiantGrowth, displayName = "Giant Growth",
                blurb = "Grow enormous — the net simply stops being an argument.",
                bannerText = "GIANT GROWTH!",
                duration = 6f, color = new Color(0.40f, 0.90f, 0.35f),
                aiCue = PowerAiCue.OpponentAttack,
                spriteScale = 1.45f, reachMult = 1.45f, reachHeightMult = 1.3f, blockReachMult = 1.6f,
            },
            new PowerUpDef
            {
                type = PowerUpType.CycloneServe, displayName = "Cyclone",
                blurb = "Whips up a crosswind that bends the ball in flight.",
                bannerText = "CYCLONE!",
                duration = 6f, color = new Color(0.30f, 0.95f, 0.85f),
                aiCue = PowerAiCue.OwnServe,
                extraWind = new Vector3(3.0f, 0f, 0f),
            },
            new PowerUpDef
            {
                type = PowerUpType.GoldenTouch, displayName = "Golden Touch",
                blurb = "Every touch lands exactly where it was aimed.",
                bannerText = "GOLDEN TOUCH!",
                duration = 8f, color = new Color(1.00f, 0.72f, 0.10f),
                aiCue = PowerAiCue.OwnPossession,
                selfErrorMult = 0.05f,
            },
            new PowerUpDef
            {
                type = PowerUpType.WildfireSprint, displayName = "Wildfire Sprint",
                blurb = "Blazing speed — cover the whole court at a sprint.",
                bannerText = "WILDFIRE SPRINT!",
                duration = 6f, color = new Color(1.00f, 0.45f, 0.15f),
                aiCue = PowerAiCue.OpponentAttack,
                moveMult = 1.7f,
            },
            new PowerUpDef
            {
                type = PowerUpType.MoonBall, displayName = "Moon Ball",
                blurb = "Gravity lets go — the ball floats and everyone soars.",
                bannerText = "MOON BALL!",
                duration = 5f, color = new Color(0.75f, 0.65f, 1.00f),
                aiCue = PowerAiCue.Anytime,
                gravityMult = 0.55f,
            },
            new PowerUpDef
            {
                type = PowerUpType.SkyJump, displayName = "Sky Jump",
                blurb = "Leap far above the net and hammer it down.",
                bannerText = "SKY JUMP!",
                duration = 6f, color = new Color(0.45f, 0.70f, 1.00f),
                aiCue = PowerAiCue.OwnAttack,
                jumpMult = 1.30f, selfErrorMult = 0.7f,
            },
            new PowerUpDef
            {
                type = PowerUpType.LongReach, displayName = "Long Reach",
                blurb = "Stretch out and reach balls nobody should ever get.",
                bannerText = "LONG REACH!",
                duration = 6f, color = new Color(0.85f, 0.45f, 0.95f),
                aiCue = PowerAiCue.OpponentAttack,
                reachMult = 1.8f, blockReachMult = 1.5f,
            },
            new PowerUpDef
            {
                type = PowerUpType.Sandstorm, displayName = "Sandstorm",
                blurb = "Kicks up stinging sand — the other team can't place a ball.",
                bannerText = "SANDSTORM!",
                duration = 6f, color = new Color(0.85f, 0.65f, 0.35f),
                aiCue = PowerAiCue.OwnAttack,
                oppErrorMult = 2.5f,
            },
        };

        public static PowerUpDef Get(PowerUpType type)
        {
            foreach (var p in All)
                if (p.type == type) return p;
            return All[0];
        }
    }
}
