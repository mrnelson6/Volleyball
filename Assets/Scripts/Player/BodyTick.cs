using UnityEngine;

namespace Volleyball
{
    /// <summary>Snapshots every body before any player steps this tick (players run at order 0).</summary>
    [DefaultExecutionOrder(-200)]
    public class BodyTick : MonoBehaviour
    {
        public MatchManager match;

        void FixedUpdate()
        {
            if (match != null) BodySet.Capture(match.players);
        }

        void OnDestroy() => BodySet.Clear();
    }
}
