using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Volleyball.EditorTests
{
    /// <summary>
    /// Player-vs-player bodies (Overcooked-style bumping) and dive knockdowns. Players are stepped
    /// the way the live game does it: a tick-start <see cref="BodyFrame"/> captured BEFORE anyone
    /// moves, then each player's Simulate against that frame, then the <see cref="BodyReferee"/>.
    /// </summary>
    public class PlayerBodyTests
    {
        class ScriptedPlayer : VolleyPlayer
        {
            public override InputCommand GetCommand(int tick) => InputCommand.Empty(tick);
        }

        const float Dt = 0.02f;
        const float MinSeparation = 0.35f; // bodies may squash a little while shoving, never pass through

        ScriptedPlayer _a, _b;
        Vector3 _savedGravity;
        readonly BodyEntry[] _frame = new BodyEntry[2];

        [SetUp]
        public void SetUp()
        {
            _savedGravity = Physics.gravity;
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            _a = Make("A", -1f);
            _b = Make("B", 1f);
        }

        [TearDown]
        public void TearDown()
        {
            Physics.gravity = _savedGravity;
            Object.DestroyImmediate(_a.gameObject);
            Object.DestroyImmediate(_b.gameObject);
        }

        static ScriptedPlayer Make(string name, float halfSign)
        {
            var p = new GameObject(name).AddComponent<ScriptedPlayer>();
            p.team = TeamSide.A;
            p.halfSign = halfSign;
            return p;
        }

        static void Place(VolleyPlayer p, Vector3 pos) => p.ApplySimState(new PlayerSimState { position = pos });

        static InputCommand Move(int tick, Vector2 dir)
        {
            var c = InputCommand.Empty(tick);
            c.moveWorld = dir;
            return c;
        }

        BodyFrame Capture()
        {
            VolleyPlayer[] ps = { _a, _b };
            for (int i = 0; i < ps.Length; i++)
                _frame[i] = new BodyEntry
                {
                    player = ps[i], position = ps[i].SimPosition, radius = ps[i].bodyRadius,
                    height = ps[i].bodyHeight, order = ps[i].BodyOrder,
                };
            return new BodyFrame(_frame, ps.Length);
        }

        /// <summary>One live tick: frame → both step (in the given order) → referee.</summary>
        void Tick(int t, InputCommand ca, InputCommand cb, bool bFirst = false)
        {
            BodyFrame f = Capture();
            if (bFirst) { _b.Simulate(in cb, Dt, SimRole.Authority, f); _a.Simulate(in ca, Dt, SimRole.Authority, f); }
            else { _a.Simulate(in ca, Dt, SimRole.Authority, f); _b.Simulate(in cb, Dt, SimRole.Authority, f); }
            BodyReferee.Resolve(new List<VolleyPlayer> { _a, _b });
        }

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        [Test]
        public void WalkingIntoAStandingPlayer_SlidesYouRoundTheSide_NeverThrough()
        {
            Place(_a, new Vector3(-1.2f, 0f, -5f));
            Place(_b, new Vector3(0f, 0f, -5f));
            float closest = float.MaxValue;
            for (int t = 0; t < 100; t++) // two seconds of walking straight at them
            {
                Tick(t, Move(t, Vector2.right), InputCommand.Empty(t));
                closest = Mathf.Min(closest, Flat(_a.SimPosition, _b.SimPosition));
            }
            Assert.Greater(_a.SimPosition.x, _b.SimPosition.x + 0.3f,
                "walking head-on into someone should slide you round them and on your way");
            Assert.Less(Flat(_b.SimPosition, new Vector3(0f, 0f, -5f)), 0.4f,
                "the player walked into only gets nudged, not bulldozed");
            Assert.Greater(closest, MinSeparation, "bodies overlapped too deeply");
        }

        [Test]
        public void WalkingHeadOn_BothSlipAroundEachOther()
        {
            Place(_a, new Vector3(-3f, 0f, -5f));
            Place(_b, new Vector3(3f, 0f, -5f));
            float closest = float.MaxValue;
            for (int t = 0; t < 200; t++) // four seconds
            {
                Tick(t, Move(t, Vector2.right), Move(t, Vector2.left));
                closest = Mathf.Min(closest, Flat(_a.SimPosition, _b.SimPosition));
            }
            Assert.Greater(_a.SimPosition.x, 1.5f, "A should have slipped past B");
            Assert.Less(_b.SimPosition.x, -1.5f, "B should have slipped past A");
            Assert.Greater(closest, MinSeparation, "bodies overlapped too deeply while passing");
        }

        [Test]
        public void Pushing_DoesNotDependOnStepOrder()
        {
            PlayerSimState[] Run(bool bFirst)
            {
                Place(_a, new Vector3(-1.2f, 0f, -5f));
                Place(_b, new Vector3(0f, 0.0f, -5.05f));
                var trace = new PlayerSimState[80];
                for (int t = 0; t < trace.Length; t++)
                {
                    Tick(t, Move(t, new Vector2(1f, 0.2f)), Move(t, new Vector2(-0.3f, 0f)), bFirst);
                    trace[t] = _b.CaptureSimState();
                }
                return trace;
            }

            var ab = Run(false);
            var ba = Run(true);
            for (int t = 0; t < ab.Length; t++)
                Assert.AreEqual(ab[t].position, ba[t].position, $"tick {t}: step order changed the result");
        }

        [Test]
        public void DivingIntoAPlayer_KnocksThemDown_DiverCarriesOn()
        {
            Place(_a, new Vector3(-2.5f, 0f, -5f));
            Place(_b, new Vector3(0f, 0f, -5f));
            var dive = Move(0, Vector2.right);
            dive.dive = true;
            Tick(0, dive, InputCommand.Empty(0));
            bool knocked = false;
            for (int t = 1; t < 25 && !knocked; t++)
            {
                Tick(t, InputCommand.Empty(t), InputCommand.Empty(t));
                knocked = _b.IsKnockedDown;
            }
            Assert.IsTrue(knocked, "a dive through a player should bowl them over");
            Assert.IsTrue(_a.IsDiving, "the diver's own dive carries on untouched");
            Assert.Greater(_b.KnockDir.x, 0.9f, "knocked the way the diver was going");
        }

        [Test]
        public void KnockedDown_NoControlForASecond_ThenImmuneWhileGettingUp()
        {
            Place(_b, new Vector3(0f, 0f, -5f));
            Place(_a, new Vector3(-6f, 0f, -5f)); // well away
            _b.KnockDown(Vector3.right);

            // tumble settles; after that, steering/jumping/diving do nothing while down
            for (int t = 0; t < 15; t++) Tick(t, InputCommand.Empty(t), InputCommand.Empty(t));
            Vector3 settled = _b.SimPosition;
            for (int t = 15; t < 45; t++)
            {
                var c = Move(t, Vector2.left);
                c.jump = true;
                c.dive = true;
                Tick(t, InputCommand.Empty(t), c);
            }
            Assert.IsTrue(_b.IsKnockedDown, "still down before a second has passed");
            Assert.Less(Flat(_b.SimPosition, settled), 0.001f, "no movement while knocked down");
            Assert.AreEqual(0f, _b.VerticalVelocity, 1e-4f, "no jumping while knocked down");
            Assert.IsFalse(_b.IsDiving, "no diving while knocked down");

            // up after knockdownTime, but immune for the grace window
            for (int t = 45; t < 55; t++) Tick(t, InputCommand.Empty(t), InputCommand.Empty(t));
            Assert.IsFalse(_b.IsKnockedDown, "back on their feet after ~1s");
            Assert.IsFalse(_b.CanBeKnockedDown, "immune for a moment after getting up");
            Vector3 before = _b.SimPosition;
            for (int t = 55; t < 60; t++) Tick(t, InputCommand.Empty(t), Move(t, Vector2.left));
            Assert.Less(_b.SimPosition.x, before.x - 0.2f, "can move again once up");
        }
    }
}
