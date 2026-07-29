using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// A local human's device input, decoupled from the <see cref="GameInput"/> singleton so
    /// gameplay never reads a global. Presses are LATCHED between simulation ticks: the render
    /// loop can run faster or slower than the 50Hz tick, so an edge seen on any frame must
    /// stick until the next tick consumes it (<see cref="ConsumeTick"/>), or a fast frame
    /// rate would drop presses and a slow one would double them.
    /// </summary>
    public interface IInputSource
    {
        /// <summary>Latch this render frame's edges. Call once per Update, before any tick.</summary>
        void PollFrame();

        /// <summary>Current screen-space steering (not latched — held values just sample).</summary>
        Vector2 Move { get; }

        /// <summary>Take and clear the presses latched since the last tick.</summary>
        void ConsumeTick(out bool jump, out bool dive, out bool power,
                         out bool hitPressed, out HitType hitType);

        /// <summary>Take and clear the callout latched since the last tick
        /// (<see cref="ChatCall.None"/> when nothing was said).</summary>
        ChatCall ConsumeChat();

        /// <summary>Key hint for the power-up meter label ("E" on desktop, "" on touch).</summary>
        string PowerHintLabel { get; }
    }
}
