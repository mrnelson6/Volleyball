using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Makes the scene's four baked players match a <see cref="MatchConfig"/>'s occupants,
    /// swapping <see cref="PlayerController"/>/<see cref="AIController"/> components at
    /// runtime where they disagree. Runs IDENTICALLY on server and every client (plain
    /// MonoBehaviours don't replicate, so each machine performs the same swap), which is
    /// exactly why the controllers must stay non-networked components. Ownership — who may
    /// stream commands for a slot — is layered on afterwards by NetworkMatchState.
    /// </summary>
    public static class NetSlotBinder
    {
        public static void BindAll(MatchManager match, MatchConfig cfg)
        {
            if (cfg?.slots == null) return;

            var players = Object.FindObjectsByType<VolleyPlayer>(FindObjectsSortMode.None);
            foreach (var slot in cfg.slots)
            {
                VolleyPlayer p = FindBySlot(players, slot);
                if (p == null) continue;
                bool wantHuman = slot.occupant != SlotOccupant.AI;
                if (p is PlayerController == wantHuman) continue;
                Swap(match, p, wantHuman);
            }
        }

        static VolleyPlayer FindBySlot(VolleyPlayer[] players, in MatchConfig.Slot slot)
        {
            foreach (var p in players)
                if (p != null && p.team == slot.team && (p.halfSign < 0f) == (slot.halfSign < 0f))
                    return p;
            return null;
        }

        static VolleyPlayer Swap(MatchManager match, VolleyPlayer old, bool human)
        {
            GameObject go = old.gameObject;
            TeamSide team = old.team;
            float halfSign = old.halfSign;
            string characterId = old.characterId;
            Color jersey = old.jerseyColor;
            int rosterIndex = match != null ? match.players.IndexOf(old) : -1;

            // The glow caches its player in Awake — drop it and let the fresh controller's
            // Start re-add a correctly-bound one. The animator keeps its baked sprites, so
            // it gets rebound in place instead.
            var glow = go.GetComponentInChildren<PowerUpGlow>();
            if (glow != null) Object.DestroyImmediate(glow);

            Object.DestroyImmediate(old);
            VolleyPlayer fresh = human ? (VolleyPlayer)go.AddComponent<PlayerController>()
                                       : go.AddComponent<AIController>();
            fresh.team = team;
            fresh.halfSign = halfSign;
            fresh.characterId = characterId;
            fresh.jerseyColor = jersey;

            if (rosterIndex >= 0) match.players[rosterIndex] = fresh;
            match?.OnPlayerReplaced(old, fresh);
            go.GetComponentInChildren<CharacterAnimator>()?.Rebind(fresh);
            go.GetComponent<NetworkPlayer>()?.Reconfigure();

            VBLog.Event($"SLOT BIND {go.name} -> {(human ? "human" : "AI")} ({team}/{halfSign})");
            return fresh;
        }
    }
}
