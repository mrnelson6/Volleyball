using UnityEngine;

namespace Volleyball
{
    /// <summary>Which head template the sprite baker draws for a species.</summary>
    public enum HeadShape
    {
        Round,      // blunt round head, no snout (capybara, sloth, jerboa)
        Muzzle,     // round head + short forward snout (fox, bear, big cats)
        LongMuzzle, // wider/longer snout (zebra, moose, camel, boar)
        Beak,       // tapering beak wedge instead of a snout (birds)
    }

    public enum EarStyle { None, Round, Pointed, Tall, Droopy }

    public enum HornStyle { None, Horns, Antlers, Tusks }

    public enum MarkingStyle { None, Stripes, Spots, MaskPatch }

    /// <summary>
    /// Per-species body-feature parameters consumed by the editor sprite baker
    /// (CharacterArt). One shared bipedal rig + these parameters is what makes every
    /// animal recognisable without a bespoke draw function per species. Lives in the
    /// runtime assembly next to <see cref="CharacterDef"/> so both the editor baker and
    /// runtime UI can read it.
    /// </summary>
    public class SpeciesArt
    {
        public HeadShape head = HeadShape.Muzzle;
        public EarStyle ears = EarStyle.Pointed;
        public HornStyle horns = HornStyle.None;

        /// <summary>0 = head sits on the shoulders; 1 = a full giraffe neck column. Tall
        /// species already get extra canvas rows from their height stat — the neck spends
        /// those rows on neck instead of an oversized head.</summary>
        public float neck = 0f;

        /// <summary>Tail length multiplier: 0 = none, ~0.3 = stub, 1 = average, 1.4 = kangaroo.</summary>
        public float tail = 1f;

        public MarkingStyle markings = MarkingStyle.None;

        /// <summary>Colour of stripes/spots/mask patches (drawn over fur on head and limbs
        /// only — the torso wears the jersey).</summary>
        public Color markingColor = new Color(0.15f, 0.12f, 0.10f);

        public Color noseColor = new Color(0.10f, 0.08f, 0.08f);
    }
}
