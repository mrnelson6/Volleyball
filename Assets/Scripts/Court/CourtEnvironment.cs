using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Applies a region's <see cref="EnvironmentProfile"/> to the live match: gravity, ball
    /// drag, ambience, and the wind sampled each physics step by <see cref="BallController"/>.
    /// Called by <see cref="MatchManager"/> on Start for every playable scene — including
    /// non-regional ones, which is what resets the global state (gravity persists across
    /// scene loads) back to stock after a themed court.
    /// </summary>
    public static class CourtEnvironment
    {
        public const float BaseGravity = 9.81f;

        /// <summary>The profile in force for the current scene (Default when not regional).</summary>
        public static EnvironmentProfile Active { get; private set; } = EnvironmentProfile.Default;

        public static void ApplyFor(string sceneName, BallController ball)
        {
            RegionDef region = RegionRoster.BySceneName(sceneName);
            Active = region != null ? region.env : EnvironmentProfile.Default;

            Physics.gravity = new Vector3(0f, -BaseGravity * Active.gravityScale, 0f);
            if (ball != null) ball.Body.linearDamping = Active.ballDrag;
            GameAudio.SetAmbience(Active.ambience);

            VBLog.Event($"ENVIRONMENT '{sceneName}' region={(region != null ? region.id : "-")} " +
                        $"gravity={Active.gravityScale:F2} wind={VBLog.V(Active.wind)} " +
                        $"gust={Active.gustAmp:F2} drag={Active.ballDrag:F2}");
        }

        /// <summary>
        /// The wind acceleration right now: the profile's constant wind, swelling and fading
        /// with slow Perlin gusts. The AI compensates for the constant part only, so gusts read
        /// as honest misjudgement — keep gustAmp modest (≤ 0.4).
        /// </summary>
        public static Vector3 WindNow(float time)
        {
            if (Active.wind == Vector3.zero) return Vector3.zero;
            float gust = 1f + Active.gustAmp * (Mathf.PerlinNoise(time * 0.35f, 0.37f) * 2f - 1f);
            return Active.wind * gust;
        }
    }
}
