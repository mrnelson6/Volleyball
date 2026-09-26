using NUnit.Framework;
using UnityEngine;

namespace Volleyball.EditorTests
{
    /// <summary>
    /// Stick aiming (the stick at contact is the aim): sets/bumps go relative to you with neutral =
    /// straight up and never cross the net; spikes/serves map the stick onto the opponents' court.
    /// Team A plays on -Z and attacks +Z.
    /// </summary>
    public class ShotAimTests
    {
        class ScriptedPlayer : VolleyPlayer
        {
            public override InputCommand GetCommand(int tick) => InputCommand.Empty(tick);
        }

        ScriptedPlayer _p;

        [SetUp]
        public void SetUp()
        {
            _p = new GameObject("Aimer").AddComponent<ScriptedPlayer>();
            _p.team = TeamSide.A;
            _p.ApplySimState(new PlayerSimState { position = new Vector3(1f, 0f, -4f) });
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_p.gameObject);

        [Test]
        public void Set_NeutralStick_GoesStraightUp()
        {
            Vector3 t = _p.PreviewAim(HitType.Set, Vector2.zero);
            Assert.AreEqual(1f, t.x, 1e-4f);
            Assert.AreEqual(-4f, t.z, 1e-4f);
        }

        [Test]
        public void Set_Stick_SendsItThatWay_ButNeverOverTheNet()
        {
            Vector3 left = _p.PreviewAim(HitType.Set, new Vector2(-1f, 0f));
            Assert.Less(left.x, 0f, "full stick sets it well to that side");
            Vector3 net = _p.PreviewAim(HitType.Set, new Vector2(0f, 1f)); // hard toward the net (+Z)
            Assert.Less(net.z, -0.5f, "a set stays on our own side of the net");
            Assert.Greater(net.z, -4f, "...but does move toward it");
        }

        [Test]
        public void Bump_NeutralUp_FirmTowardOpponents_GoesOver()
        {
            Assert.AreEqual(-4f, _p.PreviewAim(HitType.Bump, Vector2.zero).z, 1e-4f);
            Assert.Greater(_p.PreviewAim(HitType.Bump, new Vector2(0f, 1f)).z, 0f, "firm push plays it over");
        }

        [Test]
        public void Spike_MapsStickOntoTheirCourt()
        {
            Vector3 mid = _p.PreviewAim(HitType.Spike, Vector2.zero);
            Vector3 deep = _p.PreviewAim(HitType.Spike, new Vector2(0f, 1f));
            Vector3 tip = _p.PreviewAim(HitType.Spike, new Vector2(0f, -1f));
            Vector3 line = _p.PreviewAim(HitType.Spike, new Vector2(1f, 0f));
            Assert.Greater(mid.z, 0f, "always into their court");
            Assert.AreEqual(0f, mid.x, 1e-4f, "neutral = centre");
            Assert.Greater(deep.z, mid.z, "toward them = deeper");
            Assert.Less(tip.z, mid.z, "back toward the net = short");
            Assert.Greater(tip.z, 0f, "...but still over");
            Assert.Greater(line.x, 3f, "sideways = down the line");
            Assert.LessOrEqual(Mathf.Abs(line.x), CourtGeometry.HalfWidth, "inside the court");
        }

        [Test]
        public void TinyStickDrift_CountsAsNeutral()
        {
            Vector3 a = VolleyPlayer.CourtAimPoint(TeamSide.A, new Vector2(0.1f, -0.1f), 0.75f, 0.2f, 0f);
            Vector3 b = VolleyPlayer.CourtAimPoint(TeamSide.A, Vector2.zero, 0.75f, 0.2f, 0f);
            Assert.AreEqual(b, a);
        }
    }
}
