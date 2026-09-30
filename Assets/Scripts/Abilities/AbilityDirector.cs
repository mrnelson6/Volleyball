using System.Collections.Generic;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Runs every character ability in the match. The authority (offline, or the server) fires
    /// them from the command stream — the same <c>cmd.power</c> press as the old power-ups — plans
    /// their parameters, and raises <see cref="Fired"/> so the network layer can rebuild them on
    /// every client (<see cref="MirrorFire"/>). Everything ends at rally end, everywhere.
    /// Static state lives with a runner object in the scene, so a scene change clears it.
    /// </summary>
    public static class AbilityDirector
    {
        static readonly List<Ability> _active = new List<Ability>();
        static AbilityRunner _runner;
        static BallController _ball;

        /// <summary>Authority fired an ability: (player, id, params). The network layer relays it.</summary>
        public static event System.Action<VolleyPlayer, AbilityId, AbilityParams> Fired;
        /// <summary>Authority announced a moment of a running ability: (owner, code, where, direction).</summary>
        public static event System.Action<VolleyPlayer, int, Vector3, Vector3> Moment;

        public static BallController Ball
        {
            get
            {
                if (_ball == null) _ball = Object.FindAnyObjectByType<BallController>();
                return _ball;
            }
        }

        public static IReadOnlyList<Ability> Active => _active;

        /// <summary>This player's running ability, or null.</summary>
        public static Ability ActiveFor(VolleyPlayer p)
        {
            foreach (var a in _active)
                if (a.Owner == p) return a;
            return null;
        }

        /// <summary>A running ability of type <typeparamref name="T"/>, or null.</summary>
        public static T Find<T>() where T : Ability
        {
            foreach (var a in _active)
                if (a is T t) return t;
            return null;
        }

        /// <summary>
        /// AUTHORITY: fire <paramref name="p"/>'s ability on simulation tick <paramref name="tick"/>.
        /// Returns false (and changes nothing) if they have none, one is already running, or the
        /// ability refuses to plan right now.
        /// </summary>
        public static bool TryFire(VolleyPlayer p, int tick)
        {
            AbilityDef def = AbilityRoster.Get(p.Character.ability);
            if (def == null || ActiveFor(p) != null) return false;

            Ability a = def.create();
            a.Def = def;
            a.Owner = p;
            a.Authority = true;
            a.P = new AbilityParams { startTick = tick, seed = Random.Range(1, int.MaxValue) };
            if (!a.Plan(ref a.P)) return false;

            Start(a);
            Fired?.Invoke(p, def.id, a.P);
            return true;
        }

        /// <summary>CLIENT: rebuild an ability the server fired, from its replicated params.</summary>
        public static void MirrorFire(VolleyPlayer p, AbilityId id, AbilityParams prm)
        {
            AbilityDef def = AbilityRoster.Get(id);
            if (def == null || p == null) return;
            Ability old = ActiveFor(p);
            if (old != null) Stop(old);

            Ability a = def.create();
            a.Def = def;
            a.Owner = p;
            a.Authority = false;
            a.P = prm;
            Start(a);
        }

        /// <summary>AUTHORITY: a running ability announces a moment (a slam landing...). Plays it
        /// here and raises <see cref="Moment"/> for the clients.</summary>
        internal static void Announce(Ability a, int code, Vector3 at, Vector3 dir)
        {
            a.HandleMoment(code, at, dir);
            Moment?.Invoke(a.Owner, code, at, dir);
        }

        /// <summary>CLIENT: the server announced a moment of <paramref name="owner"/>'s ability.</summary>
        public static void MirrorMoment(VolleyPlayer owner, int code, Vector3 at, Vector3 dir)
            => ActiveFor(owner)?.HandleMoment(code, at, dir);

        /// <summary>Rally over (or match reset): every ability ends and the field is cleared.</summary>
        public static void EndAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--) Stop(_active[i]);
            FieldZones.Clear();
            NetDynamics.Reset();
            DecoyBall.ClearAll();
            if (Ball != null) Ball.SetTimeScale(1f);
        }

        static void Start(Ability a)
        {
            EnsureRunner();
            _active.Add(a);
            a.Begin();
            VBLog.Event($"ABILITY {a.Def.id} by '{a.Owner.name}' team={a.Owner.team} " +
                        $"a={VBLog.V(a.P.a)} b={VBLog.V(a.P.b)} f={a.P.f:F2} tick={a.P.startTick}");
        }

        static void Stop(Ability a)
        {
            _active.Remove(a);
            a.End();
        }

        static void EnsureRunner()
        {
            if (_runner != null) return;
            var go = new GameObject("AbilityDirector");
            _runner = go.AddComponent<AbilityRunner>();
        }

        // ---- per-frame driving (the runner forwards Unity's callbacks) ----

        internal static void FixedStep(float dt)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (i >= _active.Count) continue; // an ability ended others during its tick
                Ability a = _active[i];
                a.Advance(dt);
                if (a.Done) Stop(a);
            }
        }

        internal static void FrameStep(float dt)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                if (i < _active.Count) _active[i].Update(dt);
        }

        internal static void OnBallLaunched()
        {
            BallController b = Ball;
            if (b == null) return;
            for (int i = _active.Count - 1; i >= 0; i--)
                if (i < _active.Count && _active[i].Authority)
                    _active[i].OnBallLaunched(b.LastTouchPlayer, b.LastHitType);
        }

        internal static void RunnerGone(AbilityRunner r)
        {
            if (_runner != r) return;
            _runner = null;
            _active.Clear();
            _ball = null;
            FieldZones.Clear();
            NetDynamics.Reset();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _active.Clear();
            _runner = null;
            _ball = null;
            Fired = null;
            Moment = null;
        }
    }

    /// <summary>Scene-lifetime driver for <see cref="AbilityDirector"/> (created on first use).</summary>
    public class AbilityRunner : MonoBehaviour
    {
        BallController _hooked;

        void FixedUpdate()
        {
            HookBall();
            AbilityDirector.FixedStep(Time.fixedDeltaTime);
        }

        void Update() => AbilityDirector.FrameStep(Time.deltaTime);

        void HookBall()
        {
            BallController b = AbilityDirector.Ball;
            if (b == _hooked) return;
            if (_hooked != null) _hooked.OnLaunched -= AbilityDirector.OnBallLaunched;
            _hooked = b;
            if (_hooked != null) _hooked.OnLaunched += AbilityDirector.OnBallLaunched;
        }

        void OnDestroy()
        {
            if (_hooked != null) _hooked.OnLaunched -= AbilityDirector.OnBallLaunched;
            AbilityDirector.RunnerGone(this);
        }
    }
}
