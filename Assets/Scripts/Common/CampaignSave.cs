using System;

namespace Volleyball
{
    /// <summary>
    /// Persistent state for the world-tour campaign: where you are on the ladder
    /// (<see cref="regionIndex"/> into <see cref="RegionRoster.All"/>, <see cref="matchIndex"/>
    /// into that region's tournament) plus lifetime tallies. Laid out so progression fields can
    /// be added without breaking older save files (bump <see cref="saveVersion"/> and migrate
    /// in <see cref="SaveSystem.Load"/>).
    ///
    /// Serialized to JSON by <see cref="SaveSystem"/> via <c>JsonUtility</c>, so every field must
    /// be public and of a serializable type.
    /// </summary>
    [Serializable]
    public class CampaignSave
    {
        public const int CurrentVersion = 2;

        public int saveVersion = CurrentVersion;
        public string createdIso = "";    // ISO-8601 UTC timestamp of New Game
        public string lastPlayedIso = ""; // ISO-8601 UTC timestamp of the most recent session
        public int matchesWon = 0;
        public int matchesLost = 0;

        /// <summary>v1 ladder index. Superseded by <see cref="regionIndex"/>/<see cref="matchIndex"/>;
        /// kept so v1 JSON still parses.</summary>
        public int stage = 0;

        // ---- v2: world tour ----
        public int regionIndex = 0;        // current stop in RegionRoster.All
        public int matchIndex = 0;         // current match within that region's tournament
        public int attemptsThisMatch = 0;  // losses on the current match (flavour)
        public bool tourComplete = false;  // won the grand final
    }
}
