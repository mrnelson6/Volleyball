using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// The human-controlled player: samples an <see cref="IInputSource"/> into one
    /// <see cref="InputCommand"/> per tick. The camera-relative conversion happens here —
    /// on the machine that has the camera — so the command (and the simulation consuming
    /// it) is already world-space.
    /// </summary>
    public class PlayerController : VolleyPlayer
    {
        public override bool IsHuman => true;

        IInputSource _input;

        /// <summary>Device input for this player. Defaults to the local devices; the network
        /// layer never sets one on remote proxies (their commands arrive over the wire).</summary>
        public IInputSource Input
        {
            get { return _input ?? (_input = new LocalInputSource()); }
            set { _input = value; }
        }

        protected override void Update()
        {
            // latch this render frame's presses so the fixed tick never drops one
            if (IsLocallyControlled) Input.PollFrame();
            base.Update();
        }

        public override InputCommand GetCommand(int tick)
        {
            // Only the machine that actually controls this player may sample devices — a
            // server-side copy of a remote (or dropped) human must never read the host's
            // keyboard.
            if (!IsLocallyControlled) return InputCommand.Empty(tick);

            Input.ConsumeTick(out bool jump, out bool dive, out bool power,
                              out bool hitPressed, out HitType hitType);
            Vector3 w = CamRelativeDir(Input.Move);

            // While holding the serve, the three hit keys mean serve actions instead:
            // Bump = underhand serve, Set = toss, Spike = strike the tossed ball.
            ServeIntent serve = ServeIntent.None;
            if (hitPressed && match != null && match.IsServePhaseFor(this))
                serve = hitType == HitType.Spike ? ServeIntent.JumpStrike
                      : hitType == HitType.Set   ? ServeIntent.Toss
                                                 : ServeIntent.Underhand;

            return new InputCommand
            {
                tick = tick,
                moveWorld = new Vector2(w.x, w.z),
                jump = jump,
                dive = dive,
                power = power,
                hitPressed = hitPressed,
                hitType = hitType,
                aimMode = AimMode.Steer, // humans aim by steering — see VolleyPlayer.SteerAim
                serve = serve,
            };
        }

        /// <summary>Convert screen-relative input into a world XZ direction using the camera.</summary>
        static Vector3 CamRelativeDir(Vector2 input)
        {
            Camera cam = Camera.main;
            if (cam == null) return new Vector3(input.x, 0f, input.y);

            Vector3 f = cam.transform.forward; f.y = 0f; f.Normalize();
            Vector3 r = cam.transform.right; r.y = 0f; r.Normalize();
            return r * input.x + f * input.y;
        }
    }
}
