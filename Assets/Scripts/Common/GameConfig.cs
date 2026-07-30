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

        [Header("Body / world collision")]
        [Tooltip("Radius of the collision capsule the simulation sweeps against the world — how " +
                 "close a player can press to the net, a wall, or a prop.")]
        public float bodyRadius = 0.35f;
        [Tooltip("Height of the collision capsule for a character of height 1; scales with the " +
                 "character's height stat.")]
        public float bodyHeight = 1.7f;
        [Tooltip("Ledges up to this tall are walked straight over (bleacher treads, kerbs); " +
                 "anything higher has to be jumped onto. Keep below WorldCollision's 0.5m ground " +
                 "probe, or a step you can walk over won't be found underfoot afterwards.")]
        public float stepHeight = 0.4f;

        [Header("Hitting")]
        public float reach = 2.6f;          // horizontal hit tolerance
        public float hitReachHeight = 2.4f; // vertical window (stacks on jump height)
        public float hitBufferTime = 0.35f; // how long a hit press is remembered

        [Header("Serving")]
        [Tooltip("Where the ball sits before a serve, as a height above the SERVER'S OWN " +
                 "position — their hands. Measured from them, not from the floor, so the ball " +
                 "rides up with a jump and with whatever they are standing on. Scales with the " +
                 "character's height stat.")]
        public float serveHoldHeight = 1.5f;

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

        [Header("Diving")]
        [Tooltip("A dive is a committed grounded lunge that covers ground fast. If a low ball " +
                 "comes into reach mid-slide it's dug up chaotically — high, uncontrolled, with a " +
                 "big error — then the diver is stuck getting up for a moment.")]
        public float diveSpeed = 10f;        // lunge speed while sliding (vs moveSpeed 6)
        public float diveDuration = 0.4f;    // seconds the slide lasts
        public float diveRecoverTime = 0.6f; // seconds stuck on the ground afterwards
        public float diveReach = 1.5f;       // contact radius while laid out
        public float diveMaxBallHeight = 1.5f; // a dive only digs balls below this height
        public float diveBaseError = 3f;     // huge spray — the dig squirts off in a random direction
        public float divePopApex = 3.4f;     // nominal pop height; each dig rolls 0.6–1.5x this

        [Header("Blocking")]
        [Tooltip("A block is a TIMED PRESS, never automatic: jump at the net, then hit the hit " +
                 "key as the attack reaches your hands. No press, no block.")]
        public float blockNetDistance = 1.6f; // how close to the net the player must be
        public float blockMinHeight = 1.6f;   // minimum ball height to block
        public float blockReach = 1.6f;       // radius around the player to engage a block
        public float blockBallBand = 0.9f;    // ball must be within this of the net plane

        [Tooltip("How long a block press keeps your hands up waiting for the attack. Press " +
                 "earlier than this before contact and there's no block at all. Too short and " +
                 "the block is unhittable — the ball crosses the net band in under 0.1s.")]
        public float blockPressWindow = 0.4f;
        [Tooltip("Press this long before the ball arrives — or any later — and the timing is " +
                 "perfect. You are MEANT to press slightly early, because that is what 'hit it " +
                 "as the ball reaches your hands' actually feels like; quality then falls from " +
                 "here to zero at the far end of blockPressWindow.")]
        public float blockPerfectLead = 0.18f;
        [Tooltip("Timing at or above which a block STUFFS — driven straight down at the " +
                 "attackers' feet. Below it you only deflect: the ball arcs up and drifts deep.")]
        public float blockStuffTiming = 0.6f;
        [Tooltip("Extra contact error on the worst legal timing — a press right at the edge of " +
                 "reach — falling to zero on a perfectly timed one. A badly mistimed block is a " +
                 "deflection that can spray back onto your own side or out.")]
        public float blockMistimePenalty = 2.2f;

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
        [Tooltip("Quality of the AI's blocks, 0..1. It presses the instant the window opens, so " +
                 "without this it would stuff every attack perfectly; this is the handicap that " +
                 "stands in for a human's hesitation. 1 = unbeatable at the net.")]
        public float aiBlockTiming = 0.55f;

        [Header("Chat / Callouts")]
        [Tooltip("How long a teammate's \"I got it\" / \"You got it\" steers the AI (seconds). " +
                 "A call also dies the moment the calling team plays the ball, so this is only " +
                 "the ceiling on a call nobody acted on.")]
        public float chatCallWindow = 1.4f;
        [Tooltip("Minimum gap between one player's callouts (seconds) — stops chat spam.")]
        public float chatCooldown = 0.6f;

        [Header("Power-Ups")]
        [Tooltip("Master switch for the whole power-up system (meters, activation, AI usage).")]
        public bool powerUpsEnabled = true;
        [Tooltip("Meter gained by the toucher on every ball contact (serves included). " +
                 "1 = a full meter.")]
        public float powerChargePerTouch = 0.15f;
        [Tooltip("Meter every player on court gains when a rally ends, win or lose.")]
        public float powerChargePerRally = 0.25f;

        [Header("Match")]
        public int pointsToWin = 7;
        public int maxTouches = 3;
        public float pointPauseSeconds = 1.5f;
        public float aiServeDelay = 1f;
        [Tooltip("Watchdog: a rally where nobody has touched the ball for this long is dead — the " +
                 "ball is stuck in the scenery or otherwise never coming down. The point goes " +
                 "against whoever touched it last. Must stay well above any real flight time.")]
        public float rallyStallSeconds = 8f;

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
