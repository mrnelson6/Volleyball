using System.Collections.Generic;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// The court's callout channel. Two responsibilities, deliberately split:
    ///
    ///  - <see cref="Say"/> is the AUTHORITY side. A callout reaches it from the tick command
    ///    stream (<see cref="VolleyPlayer.Simulate"/>, exactly like a power-up activation), it
    ///    rate-limits, records what the two gameplay calls mean for the team, and relays the
    ///    result to every client through <see cref="Relay"/>.
    ///  - <see cref="Show"/> is the VIEW side, run on every machine (the authority right after
    ///    Say, clients from the relayed RPC): a bubble over the speaker and a sound. It records
    ///    nothing, so no client can drift from the server's idea of who called the ball.
    ///
    /// The AI asks <see cref="TeammateClaimed"/> / <see cref="InvitedToTake"/> each tick, which
    /// is the whole gameplay integration: "I got it" makes teammates back off, "You got it"
    /// puts the ball on them. A call is spent the moment the calling team actually plays the
    /// ball, and expires on its own after <c>chatCallWindow</c> either way — a call is never
    /// allowed to leave an AI standing around.
    /// </summary>
    public static class ChatDirector
    {
        /// <summary>
        /// How a server-side callout reaches clients. Set by the network layer while a session
        /// is live (see NetworkMatchState); null offline, where <see cref="Show"/> alone is the
        /// whole presentation.
        /// </summary>
        public static System.Action<VolleyPlayer, ChatCall> Relay;

        struct Call
        {
            public VolleyPlayer speaker;
            public float until;
            /// <summary>Own-team toucher when the call was made — the ball moving on past it
            /// is what spends the call.</summary>
            public VolleyPlayer touchMark;
            public bool live;
        }

        // one live claim ("I got it") and one live cede ("You got it") per team; the newer
        // call always wins, because a player changing their mind is the common case
        static readonly Call[] _claims = new Call[2];
        static readonly Call[] _cedes = new Call[2];

        // per-speaker cooldown, keyed by instance id so a swapped-out controller can't leak
        static readonly Dictionary<int, float> _nextAllowed = new Dictionary<int, float>();

        static MatchManager _match;

        /// <summary>
        /// Adopt the match this court is playing: its roster and its ball are what callouts get
        /// judged against (who is nearest, whether a call has been spent). Called by
        /// <see cref="MatchManager"/> as it starts, so nothing here has to go hunting for scene
        /// objects. Unbound, calls still work — they just expire on time alone.
        /// </summary>
        public static void Bind(MatchManager match)
        {
            _match = match;
            ClearCalls();
        }

        /// <summary>Release a match as its scene goes away — a no-op if another one has already
        /// taken over, so an old court's teardown can't unbind the new one.</summary>
        public static void Unbind(MatchManager match)
        {
            if (_match == match) Bind(null);
        }

        static BallController Ball => _match != null ? _match.ball : null;

        static int Idx(TeamSide t) => t == TeamSide.B ? 1 : 0;

        // ------------------------------------------------------------------ authority

        /// <summary>
        /// A player said something (authority only — the command stream carried it here).
        /// Applies the team meaning, tells the clients, and shows it locally.
        /// </summary>
        public static void Say(VolleyPlayer speaker, ChatCall call)
        {
            if (speaker == null || call == ChatCall.None) return;

            float now = Time.time;
            int id = speaker.GetInstanceID();
            if (_nextAllowed.TryGetValue(id, out float ready) && now < ready) return;
            _nextAllowed[id] = now + Mathf.Max(0.1f, GameConfig.Instance.chatCooldown);

            if (ChatCalls.IsTeamCall(call)) Record(speaker, call, now);

            VBLog.Event($"CHAT {call} by '{speaker.name}' team={speaker.team}");
            if (NetworkSession.IsOnline) Relay?.Invoke(speaker, call); // never a stale relay offline
            Show(speaker, call);
        }

        static void Record(VolleyPlayer speaker, ChatCall call, float now)
        {
            var c = new Call
            {
                speaker = speaker,
                until = now + Mathf.Max(0.2f, GameConfig.Instance.chatCallWindow),
                touchMark = OwnTeamToucher(speaker.team),
                live = true,
            };
            int i = Idx(speaker.team);
            if (call == ChatCall.IGotIt) { _claims[i] = c; _cedes[i] = default; }
            else { _cedes[i] = c; _claims[i] = default; }
        }

        static VolleyPlayer OwnTeamToucher(TeamSide team)
        {
            BallController b = Ball;
            return b != null && b.LastTouchTeam == team ? b.LastTouchPlayer : null;
        }

        /// <summary>Drop every live call — the serve boundary wipes the slate.</summary>
        public static void ClearCalls()
        {
            _claims[0] = _claims[1] = default;
            _cedes[0] = _cedes[1] = default;
            _nextAllowed.Clear();
        }

        // ------------------------------------------------------------------ AI queries

        /// <summary>True while a teammate has called this ball for themselves: back off it.</summary>
        public static bool TeammateClaimed(VolleyPlayer p)
        {
            if (p == null) return false;
            Call c = _claims[Idx(p.team)];
            return Live(in c, p.team) && (Object)c.speaker != (Object)p;
        }

        /// <summary>
        /// True while a teammate has handed this ball over and <paramref name="p"/> is the one
        /// being handed it — the teammate of the speaker closest to <paramref name="point"/>
        /// (the predicted landing spot). Everyone else on the team stays out of it.
        /// </summary>
        public static bool InvitedToTake(VolleyPlayer p, Vector3 point)
        {
            if (p == null) return false;
            Call c = _cedes[Idx(p.team)];
            if (!Live(in c, p.team) || (Object)c.speaker == (Object)p) return false;

            // An invitation must never override the no-consecutive-contacts rule — being told
            // "yours" right after you touched it would otherwise walk you into a double contact.
            BallController b = Ball;
            if (b != null && (Object)b.LastTouchPlayer == (Object)p) return false;

            return IsNearestTeammate(c.speaker, p, point);
        }

        /// <summary>A call is live until it times out, the calling team plays the ball, or a
        /// newer call from that team replaces it.</summary>
        static bool Live(in Call c, TeamSide team)
        {
            if (!c.live || c.speaker == null) return false;
            if (Time.time > c.until) return false;

            BallController b = Ball;
            if (b != null && b.LastTouchTeam == team
                && (Object)b.LastTouchPlayer != (Object)c.touchMark) return false; // spent
            return true;
        }

        /// <summary>Is <paramref name="candidate"/> the speaker's closest teammate to a point?
        /// (Exact ties break by instance id, matching the AI's own tie-break.)</summary>
        static bool IsNearestTeammate(VolleyPlayer speaker, VolleyPlayer candidate, Vector3 point)
        {
            MatchManager m = _match;
            if (m == null || m.players == null) return true; // no roster to compare against

            var q = new Vector2(point.x, point.z);
            float mine = Vector2.Distance(
                new Vector2(candidate.SimPosition.x, candidate.SimPosition.z), q);
            foreach (var other in m.players)
            {
                if (other == null || other == candidate || other == speaker) continue;
                if (other.team != candidate.team) continue;
                float d = Vector2.Distance(new Vector2(other.SimPosition.x, other.SimPosition.z), q);
                if (d < mine) return false;
                if (Mathf.Approximately(d, mine)
                    && other.GetInstanceID() < candidate.GetInstanceID()) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ view

        /// <summary>
        /// Present a callout on this machine: a bubble over the speaker's head plus its sound.
        /// The bubble component is added on demand (like the power-up glow), so no baked scene
        /// needs rebuilding for chat.
        /// </summary>
        public static void Show(VolleyPlayer speaker, ChatCall call)
        {
            if (speaker == null || call == ChatCall.None) return;
            // a dedicated server has nothing to show it to (and no graphics device to build the
            // bubble's texture with); the clients it relayed to still see it
            if (!ChatArt.CanRender) return;

            var bubble = speaker.GetComponent<ChatBubble>();
            if (bubble == null) bubble = speaker.gameObject.AddComponent<ChatBubble>();
            bubble.Say(call);

            GameAudio.PlayChat(call, speaker.transform.position);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Relay = null;
            Bind(null);
        }
    }
}
