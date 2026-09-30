using NUnit.Framework;
using UnityEngine;

namespace Volleyball.EditorTests
{
    /// <summary>
    /// The simulation pieces abilities are built from: tick-stamped ground zones (mud), and the
    /// stun / dash / burrow player states. All must be pure functions of (state, command, zones,
    /// tick) so a predicting client replays them exactly like the server.
    /// </summary>
    public class AbilitySimTests
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
            FieldZones.Clear();
            _p = new GameObject("Tester").AddComponent<ScriptedPlayer>();
            _p.team = TeamSide.A;
            _p.ApplySimState(new PlayerSimState { position = new Vector3(0f, 0f, -5f) });
        }

        [TearDown]
        public void TearDown()
        {
            FieldZones.Clear();
            NetDynamics.Reset();
            Physics.gravity = _savedGravity;
            Object.DestroyImmediate(_p.gameObject);
        }

        void Step(int tick, Vector2 move, bool jump = false)
        {
            var c = InputCommand.Empty(tick);
            c.moveWorld = move;
            c.jump = jump;
            _p.Simulate(in c, Dt, SimRole.Authority, new BodyFrame(new BodyEntry[0], 0));
        }

        float RunRight(int fromTick, int ticks)
        {
            float x0 = _p.SimPosition.x;
            for (int t = fromTick; t < fromTick + ticks; t++) Step(t, Vector2.right);
            return _p.SimPosition.x - x0;
        }

        [Test]
        public void Mud_SlowsRunning_OnlyInsideItsTicks()
        {
            float free = RunRight(0, 10);
            _p.ApplySimState(new PlayerSimState { position = new Vector3(0f, 0f, -5f) });
            FieldZones.Add(new FieldZone { kind = ZoneKind.Mud, center = new Vector2(0f, -5f), radius = 3f,
                                           startTick = 100, endTick = 200 });
            float before = RunRight(10, 10);   // ticks 10..19: zone not started yet
            _p.ApplySimState(new PlayerSimState { position = new Vector3(0f, 0f, -5f) });
            float inMud = RunRight(100, 10);  // ticks 100..109: in the mud
            Assert.AreEqual(free, before, 1e-4f, "a zone must not act before its start tick");
            Assert.Less(inMud, free * 0.6f, "mud should slow running a lot");
        }

        [Test]
        public void Mud_WeakensJumps()
        {
            FieldZones.Add(new FieldZone { kind = ZoneKind.Mud, center = new Vector2(0f, -5f), radius = 3f,
                                           startTick = 0, endTick = 1000 });
            Step(0, Vector2.zero, jump: true);
            float muddy = _p.VerticalVelocity;
            FieldZones.Clear();
            _p.ApplySimState(new PlayerSimState { position = new Vector3(0f, 0f, -5f) });
            Step(1, Vector2.zero, jump: true);
            Assert.Less(muddy, _p.VerticalVelocity * 0.8f);
        }

        [Test]
        public void Stun_RootsAndBlocksJumping_ThenWearsOff()
        {
            _p.Stun(0.5f);
            Vector3 start = _p.SimPosition;
            for (int t = 0; t < 20; t++) Step(t, Vector2.right, jump: true);
            Assert.AreEqual(start.x, _p.SimPosition.x, 1e-4f, "stunned: no movement");
            Assert.AreEqual(0f, _p.VerticalVelocity, 1e-4f, "stunned: no jump");
            for (int t = 20; t < 40; t++) Step(t, Vector2.right);
            Assert.IsFalse(_p.IsStunned);
            Assert.Greater(_p.SimPosition.x, start.x + 0.5f, "free again once the stun ends");
        }

        [Test]
        public void Dash_IgnoresTheStick_AndCoversGround()
        {
            _p.ApplySimState(new PlayerSimState { position = new Vector3(0f, 0f, -12f) }); // room before the net
            _p.StartDash(new Vector2(0f, 1f), 12f, 0.5f);
            Vector3 start = _p.SimPosition;
            for (int t = 0; t < 25; t++) Step(t, Vector2.left); // stick says left; dash says +z
            Assert.Greater(_p.SimPosition.z - start.z, 5f, "the dash carries us ~6m");
            Assert.AreEqual(start.x, _p.SimPosition.x, 0.05f, "the stick has no say mid-dash");
            Assert.IsFalse(_p.IsDashing);
        }

        [Test]
        public void Burrow_MovesUnderground_HiddenAndRootedForAMoment()
        {
            _p.BurrowTo(new Vector3(2f, 0f, -3f), 0.4f);
            Assert.IsTrue(_p.IsHidden);
            Assert.AreEqual(2f, _p.SimPosition.x, 1e-4f);
            Step(0, Vector2.left, jump: true);
            Assert.AreEqual(2f, _p.SimPosition.x, 1e-4f, "rooted while underground");
            for (int t = 1; t < 25; t++) Step(t, Vector2.zero);
            Assert.IsFalse(_p.IsHidden, "surfaces after the hide time");
        }

        [Test]
        public void TallOrder_RaisesTheNetTop()
        {
            float regulation = CourtGeometry.NetTop;
            NetDynamics.SetExtra(1f);
            Assert.AreEqual(regulation + 1f, CourtGeometry.NetTop, 1e-4f);
            NetDynamics.Reset();
            Assert.AreEqual(regulation, CourtGeometry.NetTop, 1e-4f);
        }

        [Test]
        public void AbilityShot_AlwaysClearsTheNet()
        {
            foreach (float extra in new[] { 0f, 1f })
            {
                NetDynamics.SetExtra(extra);
                Vector3 start = new Vector3(1f, 0.8f, -3f);
                Vector3 target = new Vector3(-1f, 0.4f, 6f);
                float t = AbilityShots.FlightTimeClearingNet(start, target);
                float g = 9.81f;
                float vy = (target.y - start.y + 0.5f * g * t * t) / t;
                float tn = (0f - start.z) / (target.z - start.z) * t;
                float yAtNet = start.y + vy * tn - 0.5f * g * tn * tn;
                Assert.Greater(yAtNet, CourtGeometry.NetTop, $"clears a net raised by {extra}m");
            }
        }
    }
}
