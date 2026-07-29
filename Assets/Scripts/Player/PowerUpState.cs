using System.Collections.Generic;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// One player's power-up meter and any effects currently on them. Owned by
    /// <see cref="VolleyPlayer"/> (a plain class, not a component, so the baked arena scenes
    /// need no rewiring) and ticked from its fixed-tick simulation, so effects freeze
    /// under the pause menu (timeScale 0 stops the fixed tick). The meter fills from any participation — every touch plus a
    /// chunk at each rally end — and empties on activation.
    ///
    /// A status on the list is either the player's own cast (its self-buff fields apply) or
    /// a debuff inflicted by an opponent's cast (its opp* fields apply). The aggregate
    /// multiplier properties are read per-frame by the stat getters, the contact-error
    /// model, and the ball's driven-shot pace, so effects reach every consumer — the AI's
    /// decision-making included — with no extra wiring. Everything expires at rally end.
    /// </summary>
    public class PowerUpState
    {
        class ActiveStatus
        {
            public PowerUpDef def;
            public bool inflicted; // true = an opponent put this on us (use def.opp* fields)
            public float remaining;
        }

        VolleyPlayer _owner;
        MatchManager _match;
        readonly List<ActiveStatus> _statuses = new List<ActiveStatus>();

        // Giant Growth visual state, cached so the sprite/shadow revert exactly.
        CharacterAnimator _giantAnim;
        DropShadow _giantShadow;
        Vector3 _giantBaseScale;
        float _giantBaseLocalY;
        float _giantBaseShadowSize;

        /// <summary>Meter fill, 0..1. Persists across rallies; resets on use and at match start.</summary>
        public float Charge { get; private set; }
        public bool IsFull => Charge >= 1f;

        /// <summary>This character's assigned power-up.</summary>
        public PowerUpDef Def => PowerUpRoster.Get(_owner != null
            ? _owner.Character.powerUp : CharacterRoster.All[0].powerUp);

        /// <summary>The player's own cast currently running, or null. (Inflicted debuffs
        /// don't count — they're someone else's power-up.)</summary>
        public PowerUpDef OwnActiveDef
        {
            get
            {
                foreach (var s in _statuses)
                    if (!s.inflicted) return s.def;
                return null;
            }
        }

        /// <summary>Fraction of the own cast's duration still left (0 when none) — the HUD
        /// meter shows this draining while the effect runs.</summary>
        public float OwnActiveRemaining01
        {
            get
            {
                foreach (var s in _statuses)
                    if (!s.inflicted)
                        return Mathf.Clamp01(s.remaining / Mathf.Max(s.def.duration, 0.01f));
                return 0f;
            }
        }

        // ---- aggregate effect multipliers (1 = unaffected) ----
        public float MoveMult        => Product(s => s.inflicted ? s.def.oppMoveMult : s.def.moveMult);
        public float JumpMult        => Product(s => s.inflicted ? 1f : s.def.jumpMult);
        public float ReachMult       => Product(s => s.inflicted ? 1f : s.def.reachMult);
        public float ReachHeightMult => Product(s => s.inflicted ? 1f : s.def.reachHeightMult);
        public float BlockReachMult  => Product(s => s.inflicted ? 1f : s.def.blockReachMult);
        public float AttackPaceMult  => Product(s => s.inflicted ? 1f : s.def.attackPaceMult);
        public float ErrorMult       => Product(s => s.inflicted ? s.def.oppErrorMult : s.def.selfErrorMult);

        float Product(System.Func<ActiveStatus, float> pick)
        {
            float m = 1f;
            foreach (var s in _statuses) m *= pick(s);
            return m;
        }

        public void Bind(VolleyPlayer owner, MatchManager match)
        {
            _owner = owner;
            _match = match;
        }

        public void AddCharge(float amount)
        {
            if (_owner == null || amount <= 0f || !GameConfig.Instance.powerUpsEnabled) return;
            if (IsFull) return;
            Charge = Mathf.Min(1f, Charge + amount);
            if (IsFull)
            {
                VBLog.Event($"POWERUP READY {Def.type} for '{_owner.name}' team={_owner.team}");
                // the ready-jingle is for the player at THIS screen, not for every human
                if (_owner.IsHuman && _owner.IsLocallyControlled) GameAudio.PlayPowerReady();
            }
        }

        /// <summary>Raised when this player's own cast fires — the hook the network layer
        /// uses to replicate activations to every client.</summary>
        public event System.Action<PowerUpDef> Activated;

        /// <summary>Fire the power-up: empty the meter, start the own-cast status, put any
        /// debuffs on both opponents, and flip any global wind/gravity effect on. The caller
        /// (VolleyPlayer.TryActivatePower) gates on match state and does the fanfare.</summary>
        public bool Activate()
        {
            if (_owner == null || !GameConfig.Instance.powerUpsEnabled) return false;
            if (!IsFull || OwnActiveDef != null) return false;
            DoActivate();
            return true;
        }

        /// <summary>A client mirroring the server's activation: same effects, no meter gate —
        /// the server already validated it.</summary>
        internal void MirrorActivate()
        {
            if (_owner == null || OwnActiveDef != null) return;
            DoActivate();
        }

        void DoActivate()
        {
            PowerUpDef def = Def;
            Charge = 0f;
            _statuses.Add(new ActiveStatus { def = def, inflicted = false, remaining = def.duration });

            if (_match != null && (def.oppMoveMult != 1f || def.oppErrorMult != 1f))
                foreach (var p in _match.players)
                    if (p != null && p.team != _owner.team)
                        p.Power.Inflict(def);

            if (def.gravityMult != 1f) PowerUpDirector.SetGravityMult(def.gravityMult);
            if (def.extraWind != Vector3.zero) PowerUpDirector.SetExtraWind(def.extraWind);
            if (def.spriteScale != 1f) ApplyGiantVisuals(def.spriteScale);
            Activated?.Invoke(def);
        }

        /// <summary>Adopt the server's authoritative meter value (rides in every snapshot).
        /// Plays the ready jingle on the fill edge, same as charging locally would.</summary>
        internal void MirrorCharge(float value)
        {
            bool wasFull = IsFull;
            Charge = Mathf.Clamp01(value);
            if (!wasFull && IsFull && _owner != null && _owner.IsHuman && _owner.IsLocallyControlled)
                GameAudio.PlayPowerReady();
        }

        /// <summary>Client mirror of the rally-end clean slate (charge itself comes from
        /// snapshots, so only the statuses expire here).</summary>
        internal void MirrorRallyEnd() => ExpireAll();

        /// <summary>An opponent's cast lands its debuff on this player for its duration.</summary>
        public void Inflict(PowerUpDef def)
            => _statuses.Add(new ActiveStatus { def = def, inflicted = true, remaining = def.duration });

        /// <summary>Count down and expire effects. Runs on scaled time — paused game, paused effects.</summary>
        public void Tick(float dt)
        {
            for (int i = _statuses.Count - 1; i >= 0; i--)
            {
                _statuses[i].remaining -= dt;
                if (_statuses[i].remaining <= 0f)
                {
                    ActiveStatus s = _statuses[i];
                    _statuses.RemoveAt(i);
                    Expire(s);
                }
            }
        }

        /// <summary>Rally over: every effect ends (clean slate for the next serve) and the
        /// participation chunk lands — win or lose the point.</summary>
        public void OnRallyEnd(float chunk)
        {
            ExpireAll();
            AddCharge(chunk);
        }

        public void ResetForMatch()
        {
            ExpireAll();
            Charge = 0f;
        }

        void ExpireAll()
        {
            if (_statuses.Count == 0) return;
            var expiring = new List<ActiveStatus>(_statuses);
            _statuses.Clear();
            foreach (var s in expiring) Expire(s);
        }

        void Expire(ActiveStatus s)
        {
            if (s.inflicted) return; // a debuff just falls off — the caster owns the cleanup

            if (s.def.spriteScale != 1f) RevertGiantVisuals();
            if (s.def.gravityMult != 1f) PowerUpDirector.SetGravityMult(1f);
            if (s.def.extraWind != Vector3.zero) PowerUpDirector.SetExtraWind(Vector3.zero);
            VBLog.Event($"POWERUP END {s.def.type} on '{_owner.name}'");
        }

        // ---- Giant Growth visuals: scale the sprite child + shadow, revert exactly ----

        void ApplyGiantVisuals(float scale)
        {
            CharacterAnimator anim = _owner.GetComponentInChildren<CharacterAnimator>();
            if (anim != null)
            {
                _giantAnim = anim;
                Transform t = anim.transform;
                _giantBaseScale = t.localScale;
                _giantBaseLocalY = t.localPosition.y;
                t.localScale = _giantBaseScale * scale;
                Vector3 lp = t.localPosition;
                lp.y = _giantBaseLocalY * scale; // feet stay planted: offset scales with the body
                t.localPosition = lp;
                anim.CaptureBaseLocalY();
            }

            foreach (var ds in Object.FindObjectsByType<DropShadow>(FindObjectsSortMode.None))
                if (ds.target == _owner.transform)
                {
                    _giantShadow = ds;
                    _giantBaseShadowSize = ds.baseSize;
                    ds.baseSize *= scale;
                    break;
                }
        }

        void RevertGiantVisuals()
        {
            if (_giantAnim != null)
            {
                Transform t = _giantAnim.transform;
                t.localScale = _giantBaseScale;
                Vector3 lp = t.localPosition;
                lp.y = _giantBaseLocalY;
                t.localPosition = lp;
                _giantAnim.CaptureBaseLocalY();
                _giantAnim = null;
            }
            if (_giantShadow != null)
            {
                _giantShadow.baseSize = _giantBaseShadowSize;
                _giantShadow = null;
            }
        }
    }
}
