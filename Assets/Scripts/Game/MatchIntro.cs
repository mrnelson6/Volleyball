namespace Volleyball
{
    /// <summary>
    /// The pre-match cinematic's timeline, shared by the authority (which holds the match in
    /// <see cref="MatchState.Intro"/> for exactly <see cref="Length"/> seconds, then blows the
    /// whistle) and every viewer (<see cref="MatchIntroDirector"/> renders the beat that the
    /// shared clock says is playing). Pure data: the length depends only on the number of
    /// players on court, so server and clients agree on it without sending it.
    ///
    /// <code>
    /// | fly-over | eyes × shots | versus | 3 · 2 · 1 | settle | whistle
    /// </code>
    /// </summary>
    public static class MatchIntro
    {
        /// <summary>Master switch. Off, a match goes straight to the first whistle (the
        /// art screenshot tour turns it off so its shots stay on gameplay).</summary>
        public static bool Enabled = true;

        /// <summary>The camera's sweep over the court.</summary>
        public const float FlyLength = 4.2f;

        /// <summary>Each player's anime eye close-up.</summary>
        public const float EyeShotLength = 1.05f;

        /// <summary>Everyone at once: a team-vs-team split screen of every player's eyes.</summary>
        public const float VersusLength = 3f;

        /// <summary>One number of the countdown, and how many there are (3, 2, 1).</summary>
        public const float CountBeat = 0.8f;
        public const int CountFrom = 3;

        /// <summary>Back in the normal game view, a breath before the whistle.</summary>
        public const float SettleLength = 0.6f;

        public static float EyesStart => FlyLength;
        public static float VersusStart(int shots) => EyesStart + shots * EyeShotLength;
        public static float CountStart(int shots) => VersusStart(shots) + VersusLength;
        public static float SettleStart(int shots) => CountStart(shots) + CountFrom * CountBeat;
        public static float Length(int shots) => SettleStart(shots) + SettleLength;
    }
}
