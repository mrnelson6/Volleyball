using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Central tuning values shared by every player and the match, so sizes/speeds/rules can
    /// be changed globally in one place. Create the editable asset via
    /// <c>Volleyball → Create Game Config</c> (it lives in a Resources folder so it loads at
    /// runtime); without it, these built-in defaults are used.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Volleyball/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Movement")]
        public float moveSpeed = 6f;
        public float jumpSpeed = 6.5f;

        [Header("Hitting")]
        public float reach = 2.6f;          // horizontal hit tolerance
        public float hitReachHeight = 2.4f; // vertical window (stacks on jump height)
        public float hitBufferTime = 0.35f; // how long a hit press is remembered
        [Tooltip("Random spread (in metres) added to every hit's landing spot. 0 = perfectly " +
                 "aimed; higher = less predictable, you never quite know where it'll go.")]
        public float hitChaos = 1.5f;

        [Header("Blocking")]
        public float blockNetDistance = 1.6f; // how close to the net the player must be
        public float blockMinHeight = 1.6f;   // minimum ball height to block
        public float blockReach = 1.6f;       // radius around the player to engage a block
        public float blockBallBand = 0.9f;    // ball must be within this of the net plane

        [Header("AI")]
        public float aiAimError = 1.2f;
        public float aiSpikeHeightThreshold = 1.8f;

        [Header("Match")]
        public int pointsToWin = 7;
        public int maxTouches = 3;
        public float pointPauseSeconds = 1.5f;
        public float aiServeDelay = 1f;

        [Header("Visuals")]
        [Tooltip("Ball speed (m/s) above which a motion trail appears — hard spikes / jump serves.")]
        public float trailMinSpeed = 13f;

        [Header("Audio (0–1)")]
        [Range(0f, 1f)] public float masterVolume = 1f;     // scales everything
        [Range(0f, 1f)] public float sfxVolume = 1f;        // hits, net, whistle, landings, points
        [Range(0f, 1f)] public float ambientVolume = 0.22f; // beach loop
        [Range(0f, 1f)] public float movementVolume = 0.16f; // sand-shuffle ceiling
        [Range(0f, 1f)] public float crowdVolume = 0.5f;     // cheer/applause on points

        static GameConfig _instance;

        /// <summary>The shared config — the Resources asset if present, else built-in defaults.</summary>
        public static GameConfig Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = Resources.Load<GameConfig>("GameConfig");
                if (_instance == null)
                {
                    _instance = CreateInstance<GameConfig>();
                    Debug.LogWarning("[Volleyball] No GameConfig asset in a Resources folder — using " +
                                     "built-in defaults. Run 'Volleyball → Create Game Config' to make one you can edit.");
                }
                return _instance;
            }
        }
    }
}
