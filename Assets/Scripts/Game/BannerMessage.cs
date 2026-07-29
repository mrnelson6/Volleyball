namespace Volleyball
{
    /// <summary>What kind of thing the match banner is announcing.</summary>
    public enum BannerKind : byte
    {
        None,
        Raw,        // pre-rendered text (campaign results) — shown verbatim
        PowerShout, // "Tiger: CYCLONE!" — shown verbatim, timed clear
        Point,      // team scored; text = reason ("out", "double contact", …)
        MatchWon,   // team won the match (non-campaign)
        ServeHint,  // a human server holds the ball; text = server's display name
        TossHint,   // the jump-serve toss is up; text = server's display name
        Perfect,    // a perfect jump serve is in flight
        AiServing,  // an AI serves; text = its display name
    }

    /// <summary>
    /// A structured banner: the match reports WHAT happened (kind + team + payload) and each
    /// viewer's HUD renders it from its own perspective — "Point — You!" on the scoring side
    /// is "Point — Opponents" on the other. Keeps every string out of the authoritative match
    /// state, which matters once two humans on opposite teams watch the same match.
    /// </summary>
    [System.Serializable]
    public struct BannerMessage
    {
        public BannerKind kind;
        public TeamSide team;
        public string text;

        public static readonly BannerMessage None =
            new BannerMessage { kind = BannerKind.None, team = TeamSide.None };

        public static BannerMessage Raw(string text)
            => new BannerMessage { kind = BannerKind.Raw, team = TeamSide.None, text = text };

        public static BannerMessage Of(BannerKind kind, TeamSide team = TeamSide.None, string text = null)
            => new BannerMessage { kind = kind, team = team, text = text };

        public bool IsNone => kind == BannerKind.None;
    }
}
