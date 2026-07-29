using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// The court-wide power-up effects: extra wind (Cyclone) and scaled gravity (Moon Ball).
    /// Static so the ball and players can read it without wiring. State is reverted by the
    /// owning effect's expiry, again by MatchManager at every rally end (belt and braces),
    /// and gravity gets a third safety net: CourtEnvironment.ApplyFor resets it on every
    /// scene load.
    /// </summary>
    public static class PowerUpDirector
    {
        /// <summary>Additional wind acceleration on the ball, on top of the regional wind.</summary>
        public static Vector3 ExtraWind { get; private set; }

        public static void SetExtraWind(Vector3 wind) => ExtraWind = wind;

        /// <summary>Raised whenever the gravity multiplier changes (Moon Ball on/off) — the
        /// network layer replicates it so every client's jump physics agrees with the server.</summary>
        public static event System.Action<float> GravityMultChanged;

        /// <summary>Scale gravity relative to the scene's regional profile (1 = back to normal).</summary>
        public static void SetGravityMult(float mult)
        {
            Physics.gravity = new Vector3(
                0f, -CourtEnvironment.BaseGravity * CourtEnvironment.Active.gravityScale * mult, 0f);
            GravityMultChanged?.Invoke(mult);
        }

        public static void RevertAll()
        {
            ExtraWind = Vector3.zero;
            SetGravityMult(1f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => ExtraWind = Vector3.zero;
    }
}
