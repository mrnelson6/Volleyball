namespace Volleyball
{
    /// <summary>
    /// Carries the pre-match <see cref="MatchConfig"/> from the menu into the next loaded
    /// arena (statics survive scene loads). <see cref="MatchManager"/> applies it on Start
    /// and the players re-dress themselves. Null = no menu choice (a scene played directly
    /// from the editor): the scene's built-in characters are kept.
    /// </summary>
    public static class MatchSetup
    {
        public static MatchConfig Current;

        public static void Clear() => Current = null;
    }
}
