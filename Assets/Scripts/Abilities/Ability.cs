using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// One running ability. Lives on EVERY machine: the authority (offline, or the server) plans
    /// it and does the gameplay; clients rebuild it from the replicated <see cref="AbilityParams"/>
    /// to show it, and to feed anything the player simulation reads (zones, the net height).
    ///
    /// Gameplay that is a match-level judgement — knocking someone down, stunning them, striking
    /// the ball — happens only where <see cref="Authority"/> is true, exactly like the contact
    /// rolls; its results reach clients through the player sim state and the ball snapshots.
    /// </summary>
    public abstract class Ability
    {
        public AbilityDef Def { get; internal set; }
        public VolleyPlayer Owner { get; internal set; }
        public AbilityParams P;
        /// <summary>True offline and on the server: the machine whose word is final.</summary>
        public bool Authority { get; internal set; }
        /// <summary>Seconds since it fired (real simulation seconds on this machine).</summary>
        public float Elapsed { get; private set; }
        /// <summary>Set to finish; the director ends it after this tick.</summary>
        public bool Done { get; protected set; }

        protected MatchManager Match => Owner != null ? Owner.Match : null;
        protected BallController Ball => AbilityDirector.Ball;
        protected TeamSide Team => Owner.team;
        protected TeamSide Opp => Owner.team.Other();
        /// <summary>+1 / −1: the Z direction from this team toward the opponents.</summary>
        protected float Toward => CourtGeometry.SideSign(Owner.team.Other());

        /// <summary>
        /// AUTHORITY ONLY, before anything else: decide the parameters (where, which way). Return
        /// false to refuse — the meter is kept, nothing fires. Randomness is fine here: the
        /// result is replicated, never re-rolled.
        /// </summary>
        public virtual bool Plan(ref AbilityParams p) => true;

        /// <summary>All machines, once the params are known: build visuals, zones, state.</summary>
        public virtual void Begin() { }

        /// <summary>All machines, every fixed tick while running. Gate gameplay on <see cref="Authority"/>.</summary>
        public virtual void FixedTick(float dt) { }

        /// <summary>All machines, every rendered frame: animate the visuals.</summary>
        public virtual void Update(float dt) { }

        /// <summary>All machines: tear down visuals and anything it changed (also at rally end).</summary>
        public virtual void End() { }

        /// <summary>Authority: the ball was just launched by someone (the owner or anyone).</summary>
        public virtual void OnBallLaunched(VolleyPlayer by, HitType type) { }

        /// <summary>A one-off moment the authority announced (see <see cref="AbilityDirector.Announce"/>) —
        /// clients play its effect. Also called on the authority itself.</summary>
        public virtual void OnMoment(int code, Vector3 at, Vector3 dir) { }

        /// <summary>Moment code that ends the ability everywhere (see <see cref="Finish"/>).</summary>
        internal const int EndMoment = -1;

        /// <summary>AUTHORITY: end now, on every machine (clients learn through a moment).</summary>
        protected void Finish()
        {
            if (Done) return;
            if (Authority) Announce(EndMoment, Vector3.zero);
            Done = true;
        }

        /// <summary>Route a moment: the end code finishes the ability, anything else is the ability's.</summary>
        internal void HandleMoment(int code, Vector3 at, Vector3 dir)
        {
            if (code == EndMoment) Done = true;
            else OnMoment(code, at, dir);
        }

        /// <summary>Is the ball in free flight (not held, frozen or carried)?</summary>
        protected bool BallInFlight => Ball != null && !Ball.Body.isKinematic;

        /// <summary>A stable key for zones this ability owns.</summary>
        protected int ZoneKey => P.seed;

        /// <summary>My teammates (not me).</summary>
        protected System.Collections.Generic.IEnumerable<VolleyPlayer> Teammates()
        {
            if (Match == null) yield break;
            foreach (var p in Match.players)
                if (p != null && p != Owner && p.team == Owner.team) yield return p;
        }

        /// <summary>Is any player within <paramref name="clearance"/> of this ground point?</summary>
        protected bool NearAnyPlayer(Vector3 at, float clearance)
        {
            if (Match == null) return false;
            foreach (var p in Match.players)
                if (p != null && (p.GroundPosition - new Vector3(at.x, 0f, at.z)).magnitude < clearance) return true;
            return false;
        }

        /// <summary>Remaining fraction for the HUD bar (1 → 0).</summary>
        public virtual float Remaining01 => Mathf.Clamp01(1f - Elapsed / Mathf.Max(Def.duration, 0.01f));

        internal void Advance(float dt)
        {
            Elapsed += dt;
            FixedTick(dt);
            if (Elapsed >= Def.duration + ExtraLifetime) Done = true;
        }

        /// <summary>Seconds it may keep running past <see cref="AbilityDef.duration"/> (a lingering effect).</summary>
        protected virtual float ExtraLifetime => 0f;

        /// <summary>Tell every machine something happened (authority only) — see <see cref="OnMoment"/>.</summary>
        protected void Announce(int code, Vector3 at, Vector3 dir = default)
        {
            if (Authority) AbilityDirector.Announce(this, code, at, dir);
        }

        // ---- shared queries --------------------------------------------------------------

        /// <summary>The players on the other team.</summary>
        protected System.Collections.Generic.IEnumerable<VolleyPlayer> Opponents()
        {
            if (Match == null) yield break;
            foreach (var p in Match.players)
                if (p != null && p.team != Owner.team) yield return p;
        }

        /// <summary>Where the ball will come down (flat ground, gravity + current wind ignored),
        /// and how long until then. Returns the ball position with t=0 if it's held.</summary>
        protected Vector3 PredictLanding(out float t)
        {
            t = 0f;
            if (Ball == null) return Vector3.zero;
            Vector3 p = Ball.transform.position;
            Vector3 v = Ball.Body.isKinematic ? Vector3.zero : Ball.Body.linearVelocity;
            float g = Mathf.Max(0.1f, -Ball.EffectiveGravity.y);
            float disc = v.y * v.y + 2f * g * Mathf.Max(p.y - 0.3f, 0f);
            t = (v.y + Mathf.Sqrt(Mathf.Max(disc, 0f))) / g;
            return new Vector3(p.x + v.x * t, 0f, p.z + v.z * t);
        }
    }
}
