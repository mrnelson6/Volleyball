using NUnit.Framework;
using UnityEngine;

namespace Volleyball.EditorTests
{
    /// <summary>
    /// Guards the property client-side prediction will stand on: <see cref="VolleyPlayer.Simulate"/>
    /// is a pure function of (state, command, dt). The same command stream from the same start
    /// state must produce a bit-identical state trace — if randomness, wall-clock time, or a
    /// transform read ever sneaks into the simulation, these fail. (The second run deliberately
    /// parks the GameObject somewhere absurd: the sim must not notice, because the transform is
    /// only the rendered view.)
    /// </summary>
    public class PlayerSimDeterminismTests
    {
        class ScriptedPlayer : Volleyball.VolleyPlayer
        {
            public override Volleyball.InputCommand GetCommand(int tick)
                => Volleyball.InputCommand.Empty(tick);
        }

        const float Dt = 0.02f; // the 50Hz simulation tick

        ScriptedPlayer _player;
        Vector3 _savedGravity;

        [SetUp]
        public void SetUp()
        {
            _savedGravity = Physics.gravity;
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            _player = new GameObject("SimTestPlayer").AddComponent<ScriptedPlayer>();
            _player.team = Volleyball.TeamSide.A;
        }

        [TearDown]
        public void TearDown()
        {
            Physics.gravity = _savedGravity;
            Object.DestroyImmediate(_player.gameObject);
        }

        /// <summary>Six seconds of varied play: runs, two jumps, a dive, direction changes.</summary>
        static Volleyball.InputCommand[] ScriptedRun()
        {
            var cmds = new Volleyball.InputCommand[300];
            for (int t = 0; t < cmds.Length; t++)
            {
                var c = Volleyball.InputCommand.Empty(t);
                if (t < 60) c.moveWorld = new Vector2(0.8f, -0.4f);
                if (t == 60) c.jump = true;
                if (t > 90 && t < 130) c.moveWorld = new Vector2(-1f, 0f);
                if (t == 140) c.jump = true;
                if (t == 200) { c.moveWorld = new Vector2(0f, -1f); c.dive = true; }
                if (t > 260) c.moveWorld = new Vector2(0.3f, 0.9f);
                cmds[t] = c;
            }
            return cmds;
        }

        Volleyball.PlayerSimState[] Trace(Volleyball.InputCommand[] cmds,
                                          Volleyball.PlayerSimState start, Vector3 parkTransformAt)
        {
            _player.transform.position = parkTransformAt; // the sim must never look at this
            _player.ApplySimState(start);
            var trace = new Volleyball.PlayerSimState[cmds.Length];
            for (int i = 0; i < cmds.Length; i++)
            {
                _player.Simulate(in cmds[i], Dt);
                trace[i] = _player.CaptureSimState();
            }
            return trace;
        }

        [Test]
        public void SameCommands_ProduceBitIdenticalTrace()
        {
            var start = new Volleyball.PlayerSimState { position = new Vector3(-1.5f, 0f, -6f) };
            var cmds = ScriptedRun();

            var a = Trace(cmds, start, Vector3.zero);
            var b = Trace(cmds, start, new Vector3(99f, 5f, -42f));

            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].position.x, b[i].position.x, $"position.x diverged at tick {i}");
                Assert.AreEqual(a[i].position.y, b[i].position.y, $"position.y diverged at tick {i}");
                Assert.AreEqual(a[i].position.z, b[i].position.z, $"position.z diverged at tick {i}");
                Assert.AreEqual(a[i].vertVel, b[i].vertVel, $"vertVel diverged at tick {i}");
                Assert.AreEqual(a[i].diveTimer, b[i].diveTimer, $"diveTimer diverged at tick {i}");
                Assert.AreEqual(a[i].diveRecover, b[i].diveRecover, $"diveRecover diverged at tick {i}");
            }

            // sanity: the script actually exercised the state machine
            bool everAirborne = false, everDiving = false;
            foreach (var s in a)
            {
                if (s.position.y > 0.1f) everAirborne = true;
                if (s.diveTimer > 0f) everDiving = true;
            }
            Assert.IsTrue(everAirborne, "scripted run should include a jump");
            Assert.IsTrue(everDiving, "scripted run should include a dive");
        }

        [Test]
        public void JumpApex_IsFrameRateIndependent_AndNearClosedForm()
        {
            // The sim only ever steps at the fixed tick, so apex height cannot vary with the
            // render frame rate BY CONSTRUCTION — this pins the integrator against the
            // closed form (v²/2g) so a regression back to variable-dt stepping shows up.
            _player.ApplySimState(new Volleyball.PlayerSimState());

            var jump = Volleyball.InputCommand.Empty(0);
            jump.jump = true;
            _player.Simulate(in jump, Dt);

            float apex = _player.SimPosition.y;
            var idle = Volleyball.InputCommand.Empty(0);
            for (int i = 1; i < 300 && !(_player.IsGrounded && i > 5); i++)
            {
                _player.Simulate(in idle, Dt);
                apex = Mathf.Max(apex, _player.SimPosition.y);
            }

            float v = _player.jumpSpeed;
            float g = -Physics.gravity.y;
            float closedForm = v * v / (2f * g);
            Assert.AreEqual(closedForm, apex, v * Dt,
                "fixed-tick apex must stay within one tick's drift of the closed form");
            Assert.IsTrue(_player.IsGrounded, "player should land again");
        }
    }
}
