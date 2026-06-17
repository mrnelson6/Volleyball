namespace Volleyball
{
    /// <summary>Which side of the net a team / player belongs to.</summary>
    public enum TeamSide
    {
        None,
        A, // near side (negative Z) — the human player's team
        B  // far side (positive Z) — the opponents
    }

    public static class TeamSideExtensions
    {
        public static TeamSide Other(this TeamSide s)
        {
            if (s == TeamSide.A) return TeamSide.B;
            if (s == TeamSide.B) return TeamSide.A;
            return TeamSide.None;
        }
    }
}
