using NUnit.Framework;
using UnityEngine;

namespace Volleyball.EditorTests
{
    /// <summary>
    /// Guards what the two gameplay callouts promise the AI (see <see cref="ChatDirector"/>):
    /// "I got it" pulls teammates off the ball and nobody else, "You got it" hands it to exactly
    /// one teammate — the closest — and the newest call from a team is the one that counts.
    /// These are the rules an AI teammate's positioning depends on, so they are worth pinning.
    ///
    /// The bound match here has no ball, which is deliberate: it isolates the routing rules from
    /// the "a call is spent once the team plays the ball" check, which needs a live rally.
    /// </summary>
    public class ChatCalloutTests
    {
        class Dummy : Volleyball.VolleyPlayer
        {
            public override Volleyball.InputCommand GetCommand(int tick)
                => Volleyball.InputCommand.Empty(tick);
        }

        MatchManager _match;
        Dummy _speaker, _near, _far, _opponent;

        Dummy Make(string name, TeamSide team, Vector3 pos)
        {
            var p = new GameObject(name).AddComponent<Dummy>();
            p.team = team;
            p.ApplySimState(new PlayerSimState { position = pos });
            _match.players.Add(p);
            return p;
        }

        [SetUp]
        public void SetUp()
        {
            _match = new GameObject("TestMatch").AddComponent<MatchManager>();
            _match.players = new System.Collections.Generic.List<VolleyPlayer>();

            _speaker = Make("Speaker", TeamSide.A, new Vector3(-3f, 0f, -4f));
            _near = Make("NearMate", TeamSide.A, new Vector3(2f, 0f, -4f));
            _far = Make("FarMate", TeamSide.A, new Vector3(-4f, 0f, -8f));
            _opponent = Make("Opponent", TeamSide.B, new Vector3(0f, 0f, 4f));

            // bind THIS roster, so an arena scene that happens to be open can't sway the result
            ChatDirector.Bind(_match);
        }

        [TearDown]
        public void TearDown()
        {
            ChatDirector.Bind(null);
            foreach (var p in _match.players)
                if (p != null) Object.DestroyImmediate(p.gameObject);
            Object.DestroyImmediate(_match.gameObject);
        }

        /// <summary>A ball dropping right next to <see cref="_near"/>.</summary>
        static readonly Vector3 Landing = new Vector3(2.2f, 0f, -4.2f);

        [Test]
        public void IGotIt_YieldsTeammates_ButNotTheCaller()
        {
            ChatDirector.Say(_speaker, ChatCall.IGotIt);

            Assert.IsTrue(ChatDirector.TeammateClaimed(_near), "a teammate must back off the call");
            Assert.IsTrue(ChatDirector.TeammateClaimed(_far), "every teammate hears it");
            Assert.IsFalse(ChatDirector.TeammateClaimed(_speaker), "the caller plays the ball");
            Assert.IsFalse(ChatDirector.TeammateClaimed(_opponent), "calls are team-only");
        }

        [Test]
        public void YouGotIt_InvitesOnlyTheClosestTeammate()
        {
            ChatDirector.Say(_speaker, ChatCall.YouGotIt);

            Assert.IsTrue(ChatDirector.InvitedToTake(_near, Landing),
                          "the teammate nearest the ball takes it");
            Assert.IsFalse(ChatDirector.InvitedToTake(_far, Landing),
                           "a farther teammate must not converge on the same ball");
            Assert.IsFalse(ChatDirector.InvitedToTake(_speaker, Landing),
                           "the caller just gave the ball away");
            Assert.IsFalse(ChatDirector.InvitedToTake(_opponent, Landing), "calls are team-only");
        }

        [Test]
        public void NewestCall_ReplacesTheTeamsPreviousOne()
        {
            ChatDirector.Say(_speaker, ChatCall.IGotIt);
            ChatDirector.Say(_far, ChatCall.YouGotIt); // a beat later, someone else defers

            Assert.IsFalse(ChatDirector.TeammateClaimed(_near),
                           "the older claim must not still be pinning teammates down");
            Assert.IsTrue(ChatDirector.InvitedToTake(_near, Landing), "the newest call stands");
        }

        [Test]
        public void Emotes_HaveNoGameplayEffect()
        {
            ChatDirector.Say(_speaker, ChatCall.Nice);

            Assert.IsFalse(ChatDirector.TeammateClaimed(_near));
            Assert.IsFalse(ChatDirector.InvitedToTake(_near, Landing));
        }

        [Test]
        public void ClearCalls_SilencesTheCourt()
        {
            ChatDirector.Say(_speaker, ChatCall.IGotIt);
            ChatDirector.ClearCalls();

            Assert.IsFalse(ChatDirector.TeammateClaimed(_near));
            Assert.IsFalse(ChatDirector.InvitedToTake(_near, Landing));
        }

        [Test]
        public void Callout_RidesTheCommandStream_OnlyOnTheAuthority()
        {
            var cmd = InputCommand.Empty(0);
            cmd.chat = ChatCall.IGotIt;

            _speaker.Simulate(in cmd, 0.02f, SimRole.Predict);
            Assert.IsFalse(ChatDirector.TeammateClaimed(_near),
                           "a predicting client must not publish callouts");

            _speaker.Simulate(in cmd, 0.02f, SimRole.Replay);
            Assert.IsFalse(ChatDirector.TeammateClaimed(_near),
                           "a reconciliation replay must not re-say anything");

            _speaker.Simulate(in cmd, 0.02f, SimRole.Authority);
            Assert.IsTrue(ChatDirector.TeammateClaimed(_near),
                          "the authority is what publishes a callout");
        }
    }
}
