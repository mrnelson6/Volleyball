using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Authority-side (offline, or the server) contact pass after every player has stepped:
    /// a player still in the sliding part of a dive who overlaps someone knocks them down.
    /// Like the contact-error rolls, this is a match-level judgement kept OUT of Simulate; the
    /// result lands in the victim's sim state and replicates with the next snapshot. Runs
    /// before SnapshotSync (order 300) so the knockdown ships the same tick.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class BodyReferee : MonoBehaviour
    {
        public MatchManager match;

        void FixedUpdate()
        {
            if (match == null || !NetworkSession.IsAuthority) return;
            Resolve(match.players);
        }

        /// <summary>One knockdown pass over <paramref name="players"/> (all already stepped this
        /// tick). Returns how many were bowled over. Static so it can be tested without a match.</summary>
        public static int Resolve(System.Collections.Generic.IReadOnlyList<VolleyPlayer> players)
        {
            int knocked = 0;
            for (int a = 0; a < players.Count; a++)
            {
                VolleyPlayer diver = players[a];
                if (diver == null || !diver.IsDiveSliding) continue;
                for (int b = 0; b < players.Count; b++)
                {
                    VolleyPlayer target = players[b];
                    if (b == a || target == null || !target.CanBeKnockedDown) continue;
                    Vector3 d = target.SimPosition - diver.SimPosition;
                    if (Mathf.Abs(d.y) > Mathf.Min(diver.bodyHeight, target.bodyHeight) * 0.75f) continue;
                    float reach = diver.bodyRadius + target.bodyRadius;
                    if (d.x * d.x + d.z * d.z > reach * reach) continue;
                    target.KnockDown(diver.DiveDir);
                    knocked++;
                    VBLog.Event($"KNOCKDOWN '{target.name}' by diving '{diver.name}' at {VBLog.V(target.SimPosition)}");
                }
            }
            return knocked;
        }
    }
}
