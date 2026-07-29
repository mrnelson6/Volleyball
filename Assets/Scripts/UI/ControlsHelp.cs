using UnityEngine;
using UnityEngine.InputSystem;

namespace Volleyball
{
    /// <summary>
    /// The control list the pause overlay shows, in one place. Keybindings belong to the client
    /// (like the serve hints in <see cref="ScoreHUD"/>), and the chat rows read their keys from
    /// <see cref="ChatCalls"/> so the help can never drift from what the keys actually do.
    /// Touch devices get the on-screen button names instead of keys.
    /// </summary>
    public static class ControlsHelp
    {
        static bool Touch => Touchscreen.current != null || Application.isMobilePlatform;

        /// <summary>Movement and the four contacts.</summary>
        public static string[] PlayingLines()
        {
            if (Touch)
                return new[]
                {
                    "Move — joystick (bottom left)",
                    "Jump — JUMP",
                    "Bump — BUMP",
                    "Set — SET",
                    "Spike — SPIKE",
                    "Dive — DIVE",
                    "Power-up — POWER",
                };

            return new[]
            {
                "Move — WASD / Arrow keys",
                "Jump — Space",
                "Bump — J  or  Left-click",
                "Set — K",
                "Spike — L  or  Right-click",
                "Dive — ;  or  Left Shift",
                "Power-up — E",
            };
        }

        /// <summary>Serving, talking to your partner, and getting back here.</summary>
        public static string[] TeamLines()
        {
            string claim = ChatCalls.KeyHint(ChatCall.IGotIt);
            string cede = ChatCalls.KeyHint(ChatCall.YouGotIt);

            if (Touch)
                return new[]
                {
                    "\"I got it!\" — I GOT IT",
                    "\"You got it!\" — YOU GOT IT",
                    "Emotes — the :) button",
                    "",
                    "Serve — BUMP underhand",
                    "Jump serve — SET to toss,",
                    "    then JUMP + SPIKE at the peak",
                };

            return new[]
            {
                $"\"I got it!\" — {claim}",
                $"\"You got it!\" — {cede}",
                "Emotes — 1 to 6",
                "Pause — Esc",
                "Serve — J underhand",
                "Jump serve — K to toss,",
                "    then Space + L at the peak",
            };
        }

        /// <summary>The one-line rules reminder under the two columns.</summary>
        public const string Footer =
            "Three touches a side, and never twice in a row — bump, set, spike.  " +
            "Aim by holding a direction as you hit.";
    }
}
