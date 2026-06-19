using System;

namespace Volleyball
{
    /// <summary>
    /// Persistent state for the single-player campaign. Intentionally minimal for now — the
    /// campaign mode itself is a stub — but laid out so progression fields can be added without
    /// breaking older save files (bump <see cref="saveVersion"/> and migrate on load if needed).
    ///
    /// Serialized to JSON by <see cref="SaveSystem"/> via <c>JsonUtility</c>, so every field must
    /// be public and of a serializable type.
    /// </summary>
    [Serializable]
    public class CampaignSave
    {
        public const int CurrentVersion = 1;

        public int saveVersion = CurrentVersion;
        public string createdIso = "";   // ISO-8601 UTC timestamp of New Game
        public string lastPlayedIso = ""; // ISO-8601 UTC timestamp of the most recent session
        public int matchesWon = 0;
        public int matchesLost = 0;
        public int stage = 0;             // index into the (future) campaign ladder
    }
}
