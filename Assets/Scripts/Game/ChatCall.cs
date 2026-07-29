using UnityEngine;
using UnityEngine.InputSystem;

namespace Volleyball
{
    /// <summary>
    /// The fixed vocabulary a player can say to their team. Being a closed set (one byte) is
    /// what lets a callout ride along in the tick command stream instead of needing a chat
    /// channel of its own — and what lets the AI understand it.
    ///
    /// <see cref="IGotIt"/> and <see cref="YouGotIt"/> are the two that MEAN something: the AI
    /// listens to them (see <see cref="ChatDirector"/>). Everything after them is pure
    /// expression — shown, heard, and otherwise ignored by the simulation.
    /// </summary>
    public enum ChatCall : byte
    {
        None = 0,
        IGotIt,     // "mine!" — I'm taking this ball, stay out
        YouGotIt,   // "yours!" — I'm leaving it, you take it
        Nice,
        MyBad,
        Wow,
        LetsGo,
        Laugh,
        GoodGame,
    }

    /// <summary>Which procedurally-drawn emoji face an emote shows (see <see cref="ChatArt"/>).</summary>
    public enum ChatFace : byte { None, Smile, Sad, Shocked, Cool, Laugh, Wink }

    /// <summary>How one <see cref="ChatCall"/> looks, sounds and is triggered.</summary>
    public struct ChatCallDef
    {
        public ChatCall call;
        /// <summary>Bubble caption. Empty = show <see cref="face"/> instead of words.</summary>
        public string bubbleText;
        /// <summary>On-screen button caption. Empty = a face-only (emoji) button.</summary>
        public string buttonLabel;
        public ChatFace face;
        /// <summary>Keyboard hotkey, or <see cref="Key.None"/> for button-only.</summary>
        public Key hotkey;
        /// <summary>True for the two callouts the AI reacts to (bubble is drawn louder).</summary>
        public bool isTeamCall;
        public Color color;
    }

    /// <summary>The chat vocabulary itself — the single place labels, colours and hotkeys live,
    /// so the input layer, the on-screen bar and the bubbles never disagree.</summary>
    public static class ChatCalls
    {
        static readonly Color Claim = new Color(0.20f, 0.78f, 0.45f);
        static readonly Color Cede = new Color(1f, 0.70f, 0.20f);
        static readonly Color Emote = new Color(0.55f, 0.72f, 1f);

        /// <summary>Every call, in menu/button order (None excluded).</summary>
        public static readonly ChatCallDef[] All =
        {
            new ChatCallDef { call = ChatCall.IGotIt,   bubbleText = "I GOT IT!",  buttonLabel = "I GOT IT",
                              hotkey = Key.Z, isTeamCall = true, color = Claim },
            new ChatCallDef { call = ChatCall.YouGotIt, bubbleText = "YOU GOT IT!", buttonLabel = "YOU GOT IT",
                              hotkey = Key.X, isTeamCall = true, color = Cede },

            // emotes: a face, no words — they never touch gameplay
            new ChatCallDef { call = ChatCall.Nice,     face = ChatFace.Smile,   hotkey = Key.Digit1, color = Emote },
            new ChatCallDef { call = ChatCall.MyBad,    face = ChatFace.Sad,     hotkey = Key.Digit2, color = Emote },
            new ChatCallDef { call = ChatCall.Wow,      face = ChatFace.Shocked, hotkey = Key.Digit3, color = Emote },
            new ChatCallDef { call = ChatCall.LetsGo,   face = ChatFace.Cool,    hotkey = Key.Digit4, color = Emote },
            new ChatCallDef { call = ChatCall.Laugh,    face = ChatFace.Laugh,   hotkey = Key.Digit5, color = Emote },
            new ChatCallDef { call = ChatCall.GoodGame, face = ChatFace.Wink,    hotkey = Key.Digit6, color = Emote },
        };

        public static ChatCallDef Get(ChatCall call)
        {
            foreach (var d in All)
                if (d.call == call) return d;
            return new ChatCallDef { call = ChatCall.None, color = Color.white };
        }

        /// <summary>True for the two callouts the AI acts on.</summary>
        public static bool IsTeamCall(ChatCall call)
            => call == ChatCall.IGotIt || call == ChatCall.YouGotIt;

        /// <summary>Short printable key name for a hint label ("Z", "1"); "" when unbound.</summary>
        public static string KeyHint(ChatCall call)
        {
            Key k = Get(call).hotkey;
            if (k == Key.None) return "";
            string s = k.ToString();
            return s.StartsWith("Digit") ? s.Substring(5) : s;
        }
    }
}
