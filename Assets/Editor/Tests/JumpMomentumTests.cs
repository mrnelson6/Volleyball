using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Volleyball.EditorTests
{
    /// <summary>
    /// Jumps carry momentum: the horizontal speed you take off with keeps you flying that way,
    /// and the stick only bends it a little in the air (GameConfig.airControl). Ground control
    /// stays instant.
    /// </summary>
    public class JumpMomentumTests
    {
        class ScriptedPlayer : VolleyPlayer
        {
            public override InputCommand GetCommand(int tick) => InputCommand.Empty(tick);
        }

        const float Dt = 0.02f;
        ScriptedPlayer _p;
        Vector3 _savedGravity;

        [SetUp]
        public void SetUp()
        {
            _savedGravity = Physics.gravity;
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            _p = new GameObject("Jumper").AddComponent<ScriptedPlayer>();
            _p.team = TeamSide.A;
            _p.ApplySimState(new PlayerSimState { position = new Vector3(-3f, 0f, -5f) });
        }

        [TearDown]
        public void TearDown()
        {
            Physics.gravity = _savedGravity;
            Object.DestroyImmediate(_p.gameObject);
        }

        void Step(int tick, Vector2 move, bool jump = false)
        {
            var c = InputCommand.Empty(tick);
            c.moveWorld = move;
            c.jump = jump;
            var frame = new BodyFrame(new BodyEntry[0], 0);
            _p.Simulate(in c, Dt, SimRole.Authority, frame);
        }

        /// <summary>Run right for a bit, jump, then do <paramref name="airMove"/> until landing.
        /// Returns how far right the player travelled while airborne.</summary>
        float RunJumpThen(Vector2 airMove)
        {
            int t = 0;
            for (; t < 20; t++) Step(t, Vector2.right);
            Step(t++, Vector2.right, jump: true);
            float takeoffX = _p.SimPosition.x;
            Assert.IsFalse(_p.IsGrounded, "should be airborne after the jump");
            while (!_p.IsGrounded && t < 400) Step(t++, airMove);
            return _p.SimPosition.x - takeoffX;
        }

        [Test]
        public void RunningJump_KeepsGoingWithoutInput()
        {
            float drift = RunJumpThen(Vector2.zero);
            Assert.Greater(drift, 3f, "letting go of the stick mid-air should not stop the jump dead");
        }

        [Test]
        public void ReversingInAir_OnlySlowsYou_DoesNotTurnYouAround()
        {
            float forward = RunJumpThen(Vector2.zero);
            TearDown(); SetUp();
            float reversed = RunJumpThen(Vector2.left);
            Assert.Greater(reversed, 0f, "momentum should still carry you the way you were running");
            Assert.Less(reversed, forward, "...but pulling back does bleed some of it off");
        }

        [Test]
        public void StandingJump_OnlySmallAirSteer()
        {
            int t = 0;
            Step(t++, Vector2.zero, jump: true);
            Vector3 start = _p.SimPosition;
            while (!_p.IsGrounded && t < 400) Step(t++, Vector2.right);
            float drift = _p.SimPosition.x - start.x;
            float fullControl = 6f * 1.2f; // what the old instant air control would have covered
            Assert.Greater(drift, 0f, "the stick still nudges you");
            Assert.Less(drift, fullControl * 0.4f, "but nowhere near full control");
        }

        [Test]
        public void GroundControl_StaysInstant()
        {
            Step(0, Vector2.right);
            Step(1, Vector2.left);
            Vector3 a = _p.SimPosition;
            Step(2, Vector2.left);
            Assert.Less(_p.SimPosition.x, a.x, "on the ground a reversal takes effect immediately");
        }
    }
}
