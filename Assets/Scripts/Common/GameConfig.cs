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

        [Header("Skill / Contact Error")]
        [Tooltip("Execution error (metres of spray) is built up from these factors, then sprayed " +
                 "onto the aim point WITHOUT clamping to the court — so a bad contact can go out " +
                 "or into the net. Good contacts (in position, ideal set, well-timed spike off a " +
                 "real set) stay near zero; pressure situations build it up.")]
        public float setBaseError = 0.25f;
        public float bumpBaseError = 0.7f;
        public float spikeBaseError = 0.5f;
        public float serveBaseError = 0.4f;
        public float blockBaseError = 0.6f;

        [Tooltip("Incoming ball speed (m/s) below which no speed penalty applies — a soft ball is " +
                 "easy to control.")]
        public float softBallSpeed = 10f;
        [Tooltip("Extra error per m/s of incoming speed above softBallSpeed, per contact type. " +
                 "Set >> Bump: setting a hard-driven ball is far worse than passing it.")]
        public float setSpeedPenalty = 0.12f;
        public float bumpSpeedPenalty = 0.05f;
        public float spikeSpeedPenalty = 0.02f;

        [Tooltip("Spiking a ball that wasn't set to you (own-team Set) adds this much error.")]
        public float spikeNoSetPenalty = 1.2f;
        [Tooltip("Error added per m/s of the spiker's vertical velocity at contact — zero at the " +
                 "apex of the jump, large when hit while still rising or already falling.")]
        public float jumpTimingPenalty = 0.06f;
        [Tooltip("Penalty for spiking while grounded (mistimed jump / not airborne).")]
        public float groundedSpikePenalty = 1.5f;

        [Tooltip("Error added when reaching for the ball — scales from 0 (ball at your feet) to " +
                 "this value at the very edge of your reach. Rewards good positioning.")]
        public float reachErrorPenalty = 0.5f;

        [Tooltip("Hard cap on total contact error (metres), so nothing flies absurdly far.")]
        public float maxContactError = 4f;

        [Header("Blocking")]
        public float blockNetDistance = 1.6f; // how close to the net the player must be
        public float blockMinHeight = 1.6f;   // minimum ball height to block
        public float blockReach = 1.6f;       // radius around the player to engage a block
        public float blockBallBand = 0.9f;    // ball must be within this of the net plane

        [Header("AI")]
        public float aiAimError = 1.2f;
        public float aiSpikeHeightThreshold = 1.8f;
        [Tooltip("Human-like reaction latency (seconds): after an opponent sends the ball at the " +
                 "AI, it can't pursue, jump, or contact until a delay in this range elapses — so a " +
                 "ball blocked straight back can't be dug instantly.")]
        public float aiReactionMin = 0.18f;
        public float aiReactionMax = 0.38f;
        [Tooltip("Multiplier on the AI's contact error (1 = same skill as the player; higher = " +
                 "the AI mishits more often).")]
        public float aiErrorMult = 1f;

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
