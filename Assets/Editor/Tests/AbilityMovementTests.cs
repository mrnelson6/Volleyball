using NUnit.Framework;
using UnityEngine;

namespace Volleyball.EditorTests
{
    /// <summary>
    /// The movement pieces the second wave of abilities is built from: more ground zones (ice,
    /// pads, holes, quakes, whirlwinds) and the sim-state moves (perch, double jump, glide,
    /// blink, big stride, the pounce leap). Pure simulation, like the rest of the suite.
    /// </summary>
    public class AbilityMovementTests
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
            _p = new GameObject("Mover").AddComponent<ScriptedPlayer>();
            _p.team = TeamSide.A;
            Place(new Vector3(0f, 0f, -6f));
        }

        [TearDown]
        public void TearDown()
        {
            FieldZones.Clear();
            Physics.gravity = _savedGravity;
            Object.DestroyImmediate(_p.gameObject);
        }

        void Place(Vector3 at) => _p.ApplySimState(new PlayerSimState { position = at });

        void Step(int tick, Vector2 move, bool jump = false, bool held = false, bool power = false)
        {
            var c = InputCommand.Empty(tick);
            c.moveWorld = move;
            c.jump = jump;
            c.jumpHeld = held;
            c.power = power;
            _p.Simulate(in c, Dt, SimRole.Predict, new BodyFrame(new BodyEntry[0], 0)); // Predict: no power firing
        }

        FieldZone Zone(ZoneKind k, float r = 4f) => new FieldZone
        {
            kind = k, center = new Vector2(0f, -6f), radius = r, startTick = 0, endTick = 100000,
        };

        [Test]
        public void Ice_SpeedBuildsUpSlowly_AndCarriesOn()
        {
            FieldZones.Add(Zone(ZoneKind.Ice));
            Step(0, Vector2.right);
            float firstTick = _p.SimPosition.x;
            Assert.Less(firstTick, 6f * Dt * 0.5f, "on ice the first step is only a shuffle");
            for (int t = 1; t < 60; t++) Step(t, Vector2.right);
            float before = _p.SimPosition.x;
            Step(60, Vector2.zero);
            Assert.Greater(_p.SimPosition.x - before, 0.05f, "let go on ice and you keep sliding");
        }

        [Test]
        public void Pad_LaunchesHigher_Quake_BlocksJumping()
        {
            Step(0, Vector2.zero, jump: true);
            float normal = _p.VerticalVelocity;
            Place(new Vector3(0f, 0f, -6f));
            FieldZones.Add(Zone(ZoneKind.Pad));
            Step(1, Vector2.zero, jump: true);
            Assert.Greater(_p.VerticalVelocity, normal * 1.3f, "a pad throws you much higher");

            FieldZones.Clear();
            Place(new Vector3(0f, 0f, -6f));
            FieldZones.Add(Zone(ZoneKind.Quake));
            Step(2, Vector2.zero, jump: true);
            Assert.LessOrEqual(_p.VerticalVelocity, 0f, "no jumping on a quaking court");
        }

        [Test]
        public void Hole_TrapsOnce_ThenLetsYouOut()
        {
            FieldZones.Add(Zone(ZoneKind.Hole, 0.6f));
            Step(0, Vector2.right);
            Assert.IsTrue(_p.IsStunned, "stepped in the hole: stuck");
            for (int t = 1; t < 60; t++) Step(t, Vector2.right); // 1s stuck, then walk out
            Assert.IsFalse(_p.IsStunned);
            Assert.Greater(_p.SimPosition.x, 0.6f, "climbed out and away — not re-trapped");
        }

        [Test]
        public void Whirl_ShovesYou()
        {
            var z = Zone(ZoneKind.Whirl, 2f);
            z.center = new Vector2(1f, -6f);
            FieldZones.Add(z);
            Vector3 start = _p.SimPosition;
            for (int t = 0; t < 10; t++) Step(t, Vector2.zero);
            Assert.Greater(Vector3.Distance(start, _p.SimPosition), 0.3f);
        }

        [Test]
        public void Perch_SitsOnTheTape_AirborneForContacts_MovesSideways()
        {
            _p.StartPerch(1f);
            Step(0, Vector2.right);
            Assert.AreEqual(CourtGeometry.NetTop + 0.02f, _p.SimPosition.y, 1e-3f);
            Assert.IsFalse(_p.IsGrounded, "perched counts as in the air (spikes, blocks)");
            float x0 = _p.SimPosition.x;
            for (int t = 1; t < 10; t++) Step(t, new Vector2(1f, 1f));
            Assert.Greater(_p.SimPosition.x, x0 + 0.3f, "prowls along the net");
            Assert.AreEqual(-CourtGeometry.NetStandoff, _p.SimPosition.z, 1e-3f, "never leaves the tape");
            for (int t = 10; t < 150; t++) Step(t, Vector2.zero);
            Assert.IsTrue(_p.IsGrounded, "drops off when it ends");
        }

        [Test]
        public void DoubleJump_OnlyWhileActive_OncePerLeap()
        {
            Step(0, Vector2.zero, jump: true);
            for (int t = 1; t < 15; t++) Step(t, Vector2.zero);
            float vy = _p.VerticalVelocity;
            Step(15, Vector2.zero, jump: true);
            Assert.Less(_p.VerticalVelocity, vy, "no double jump without the ability");

            Place(new Vector3(0f, 0f, -6f));
            _p.StartDoubleJump(5f);
            Step(20, Vector2.zero, jump: true);
            for (int t = 21; t < 35; t++) Step(t, Vector2.zero);
            Step(35, Vector2.zero, jump: true);
            Assert.Greater(_p.VerticalVelocity, 4f, "second hop in mid-air");
            for (int t = 36; t < 40; t++) Step(t, Vector2.zero);
            float before = _p.VerticalVelocity;
            Step(40, Vector2.zero, jump: true);
            Assert.Less(_p.VerticalVelocity, before, "only once per leap");
        }

        [Test]
        public void Glide_HoldingJumpFloats()
        {
            _p.StartGlide(5f);
            Step(0, Vector2.zero, jump: true);
            for (int t = 1; t < 80; t++) Step(t, Vector2.zero, held: true);
            Assert.GreaterOrEqual(_p.VerticalVelocity, -1.11f, "a gliding fall is gentle");
        }

        [Test]
        public void Blink_HopsAlongTheStick_SpendingACharge()
        {
            _p.GiveBlinks(3, 5f);
            Vector3 start = _p.SimPosition;
            Step(0, Vector2.right, power: true);
            Assert.Greater(_p.SimPosition.x - start.x, 3f, "an instant ~3m hop");
            Assert.AreEqual(2, _p.BlinkCharges);
        }

        [Test]
        public void BigStride_JumpsCarryFarther()
        {
            float Bound()
            {
                float x0 = _p.SimPosition.x;
                for (int t = 0; t < 10; t++) Step(t, Vector2.right);
                Step(10, Vector2.right, jump: true);
                int k = 11;
                while (!_p.IsGrounded && k < 400) Step(k++, Vector2.zero);
                return _p.SimPosition.x - x0;
            }
            float normal = Bound();
            Place(new Vector3(-8f, 0f, -6f));
            _p.StartStride(10f);
            Assert.Greater(Bound(), normal * 1.6f);
        }

        [Test]
        public void Leap_LandsOnTarget()
        {
            Vector3 target = new Vector3(3f, 0f, -3f);
            _p.Leap(target, 0.8f);
            int t = 0;
            do Step(t++, Vector2.zero); while ((!_p.IsGrounded || t < 5) && t < 200);
            Assert.Less(Vector2.Distance(new Vector2(_p.SimPosition.x, _p.SimPosition.z), new Vector2(target.x, target.z)), 0.4f);
        }

        [Test]
        public void SlowMoBall_KeepsItsArc()
        {
            var go = new GameObject("Ball", typeof(Rigidbody), typeof(SphereCollider));
            var ball = go.AddComponent<BallController>();
            go.GetComponent<Rigidbody>().useGravity = true;
            ball.SetTimeScale(0.4f);
            Assert.AreEqual(-9.81f * 0.16f, ball.EffectiveGravity.y, 1e-3f, "gravity scales with the square");
            ball.SetTimeScale(1f);
            Object.DestroyImmediate(go);
        }
    }
}
