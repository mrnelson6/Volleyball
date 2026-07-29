using UnityEngine;

namespace Volleyball
{
    /// <summary>Who drives a court slot. Offline there is exactly one LocalHuman; online any
    /// mix — AI fills whatever humans don't claim, and versus/co-op/4-human are all just
    /// different occupant patterns over the same four slots.</summary>
    public enum SlotOccupant : byte { AI, LocalHuman, RemoteHuman }

    /// <summary>
    /// Everything needed to cast and tune one match, as plain serializable data. Built by the
    /// menus (<see cref="SceneFlow"/>) — including drawing any random characters, so every id
    /// in here is concrete — and later by the online lobby, where the host builds it once and
    /// replicates it so all clients dress the same match. Carried across the scene load in
    /// <see cref="MatchSetup.Current"/>.
    /// </summary>
    [System.Serializable]
    public class MatchConfig
    {
        /// <summary>A human slot no connected client has claimed yet. (0 can't be the
        /// sentinel — it is the server/host's own client id.)</summary>
        public const ulong UnassignedClient = ulong.MaxValue;

        [System.Serializable]
        public struct Slot
        {
            public TeamSide team;
            public float halfSign;        // -1 = left half (x<0), +1 = right
            public SlotOccupant occupant;
            public string characterId;    // concrete roster id (randoms already drawn)
            public ulong clientId;        // owning network client (online); 0 offline
        }

        /// <summary>The four court slots: A-left, A-right, B-left, B-right.</summary>
        public Slot[] slots = new Slot[4];

        /// <summary>True while a world-tour match is running: results write to the save and
        /// the Hit key routes to the next match instead of an in-place restart.</summary>
        public bool isCampaign;

        /// <summary>Shown on the HUD: "Sunny Savanna — Match 2/3 vs Stripe Sprinters".</summary>
        public string matchLabel;

        /// <summary>Per-match AI contact-error multiplier; &lt;= 0 = use the GameConfig value.</summary>
        public float aiErrorMult = -1f;

        /// <summary>Per-match scale on the AI reaction window; &lt;= 0 = unscaled.</summary>
        public float aiReactionScale = -1f;

        /// <summary>Find the slot a scene player occupies, matched by team + court half.</summary>
        public bool TryGetSlot(TeamSide team, float halfSign, out Slot slot)
        {
            foreach (var s in slots)
                if (s.team == team && (s.halfSign < 0f) == (halfSign < 0f))
                {
                    slot = s;
                    return true;
                }
            slot = default;
            return false;
        }

        /// <summary>
        /// The standard solo cast: the local human on team A's left half beside an AI
        /// teammate, versus two AI opponents. Every menu path funnels through this until the
        /// online lobby introduces other occupant patterns.
        /// </summary>
        public static MatchConfig Solo(string humanId, string teammateId, string opp1Id, string opp2Id)
            => new MatchConfig
            {
                slots = new[]
                {
                    new Slot { team = TeamSide.A, halfSign = -1f, occupant = SlotOccupant.LocalHuman, characterId = humanId },
                    new Slot { team = TeamSide.A, halfSign = 1f, occupant = SlotOccupant.AI, characterId = teammateId },
                    new Slot { team = TeamSide.B, halfSign = -1f, occupant = SlotOccupant.AI, characterId = opp1Id },
                    new Slot { team = TeamSide.B, halfSign = 1f, occupant = SlotOccupant.AI, characterId = opp2Id },
                },
            };
    }
}
