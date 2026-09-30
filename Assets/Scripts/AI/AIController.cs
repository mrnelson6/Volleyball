using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Volleyball AI for the teammate and opponents. It plays a real rally: the team's
    /// 1st touch is a bump (pass to its own setter zone), the 2nd is a set toward the
    /// best-placed teammate near the net (which can be the human), and the 3rd is an
    /// attack over the net (a jump spike when it can, otherwise a driven bump). Players
    /// pursue only when they are the closest teammate to where the ball will come down,
    /// so the two players don't fight over the same ball.
    ///
    /// The AI is just another command source: <see cref="GetCommand"/> runs the decision
    /// pass and emits the same <see cref="InputCommand"/> a human would — so the shared
    /// simulation treats them identically, and online the AI simply runs on the server.
    /// Its decisions may use randomness and wall-clock time (they're authority-side, never
    /// predicted); only the simulation consuming the command must stay deterministic.
    /// </summary>
    public class AIController : VolleyPlayer
    {
        // AI tuning is global — edit it in the GameConfig asset.
        float spikeHeightThreshold => GameConfig.Instance.aiSpikeHeightThreshold;

        // AI contacts run through the same skill/error model as the human, scaled by aiErrorMult.
        // A campaign match overrides the global value per-opponent-team (the difficulty ramp).
        protected override float ContactSkill
            => MatchSetup.Current != null && MatchSetup.Current.aiErrorMult > 0f
                ? MatchSetup.Current.aiErrorMult
                : GameConfig.Instance.aiErrorMult;

        // Campaign difficulty also scales how fast the AI reacts to an incoming ball.
        static float ReactionScale
            => MatchSetup.Current != null && MatchSetup.Current.aiReactionScale > 0f
                ? MatchSetup.Current.aiReactionScale : 1f;

        Vector3 _home;
        Vector2 _desiredMove;
        bool _wantJump;
        bool _wantDive;
        bool _wantHit;
        bool _wantBlock;
        bool _wantPower;
        bool _wantJumpHeld;
        float _blinkCooldown;
        bool _attacking;
        HitType _hitType;
        Vector3 _hitTarget;
        float _jumpCooldown;

        // Human-like reaction latency: when an opponent sends the ball our way, we can't act on
        // it until this time passes — so a ball blocked straight back can't be dug instantly.
        VolleyPlayer _prevToucher;
        float _reactUntil;

        protected override void Start()
        {
            base.Start();
            _home = new Vector3(
                halfSign * CourtGeometry.HalfWidth * 0.45f,
                0f,
                CourtGeometry.SideSign(team) * CourtGeometry.HalfDepth * 0.5f);
        }

        public override InputCommand GetCommand(int tick)
        {
            if (match != null && match.InIntro) return InputCommand.Empty(tick); // posing for the intro
            Decide();
            return new InputCommand
            {
                tick = tick,
                moveWorld = _desiredMove, // AI steering is planned in world space already
                jump = _wantJump,
                jumpHeld = _wantJumpHeld,
                dive = _wantDive,
                power = _wantPower,
                hitPressed = _wantHit,
                // A block asks for Bump explicitly: at the net that's what the sim reads as
                // "block", and a Spike there would cancel the attempt. Otherwise a planned
                // attack becomes a real spike once airborne, else a driven bump.
                hitType = _wantBlock ? HitType.Bump
                        : (_attacking && !IsGrounded) ? HitType.Spike : _hitType,
                aimMode = AimMode.Explicit, // the AI aims at its planned point, not by steer
                hitAim = _hitTarget,
                serve = ServeIntent.None,   // AI serves fire from MatchManager's timer
                // chat stays None: callouts are the human's voice. The AI only listens (below).
            };
        }

        void Decide()
        {
            _desiredMove = Vector2.zero;
            _wantJump = false;
            _wantDive = false;
            _wantHit = false;
            _wantBlock = false;
            _wantPower = false;
            _wantJumpHeld = false;
            if (ball == null) return;

            // A dead ball can't be played: between rallies (the point pause, the ball held in
            // the server's hand, a jump-serve toss) hold formation instead of chasing. Without
            // this, a held ball's near-zero flight time reads as an emergency and triggers
            // dives straight at the server.
            bool rallyLive = match == null || match.State == MatchState.Rallying;

            // Reaction latency: the instant an opponent sends the ball our way is when our
            // clock starts — until it elapses we can't contact the ball and our pursuit is
            // sluggish (see below), so a hard-driven ball can beat the read without us
            // standing frozen while it does.
            if (ball.LastTouchPlayer != _prevToucher)
            {
                _prevToucher = ball.LastTouchPlayer;
                if (ball.LastTouchTeam == team.Other())
                    _reactUntil = Time.time + Random.Range(GameConfig.Instance.aiReactionMin,
                                                           GameConfig.Instance.aiReactionMax)
                                            * ReactionScale;
            }
            bool reacting = Time.time < _reactUntil;
            // a Phantom Strike from the other side is read late: sluggish until it's nearly down
            if (PhantomStrikeAbility.InFlight && ball.LastTouchTeam == team.Other() && ball.transform.position.y > 1.6f)
                reacting = true;

            Vector3 bp = SeenBallPos;
            Vector3 landing = PredictLanding(out float tLand);

            bool teamInPossession = match != null && match.Possession == team;
            int nextTouch = teamInPossession ? match.Touches + 1 : 1;

            // never take a contact that would exceed the 3-touch limit — let it drop instead
            bool touchesRemain = !teamInPossession || match.Touches < match.maxTouches;

            PlanHit(nextTouch); // sets _attacking / _hitType / _hitTarget

            // The ball is ours to play if it will come down on our side, OR we already have
            // possession and it's currently on our side. The second clause guarantees a
            // teammate always steps up after a touch (e.g. after the human bumps a pass).
            // Decide what's "ours" by the predicted LANDING, not the current position — so we
            // never chase a ball we just sent over the net. It's ours if it will come down on
            // our side, or (while we have possession) it will land near the net, which covers a
            // block or pass that rebounds back toward us.
            bool landsOnOurSide = CourtGeometry.SideOf(landing) == team
                                  && Mathf.Abs(landing.x) <= CourtGeometry.HalfWidth + 2f
                                  && Mathf.Abs(landing.z) <= CourtGeometry.HalfDepth + 2f;
            bool landsNearNetForUs = teamInPossession
                                     && Mathf.Abs(landing.z) < 2f
                                     && Mathf.Abs(landing.x) <= CourtGeometry.HalfWidth + 2f;
            bool ballComingToUs = landsOnOurSide || landsNearNetForUs;

            // (ClosestEligibleTo excludes whoever touched last, so we naturally alternate.)
            // A serve must cross the net on its own — never chase our own serve in flight.
            bool ownServeInFlight = match != null && match.ServeInFlight && teamInPossession;

            // A human teammate's callouts (see ChatDirector) override the closest-player rule,
            // which is the whole point of saying them: "I got it" pulls us off the ball, "You
            // got it" hands it to us even when we'd have deferred. Both expire quickly and die
            // the moment our team actually plays the ball, so neither can leave us idle — and
            // with nothing said, both are false and every decision below is the one we'd have
            // made before chat existed.
            bool yielding = ChatDirector.TeammateClaimed(this);
            bool invited = ChatDirector.InvitedToTake(this, landing);
            bool designated = invited || ClosestEligibleTo(landing);
            bool pursue = rallyLive && ballComingToUs && !ownServeInFlight && !yielding && designated;

            // when attacking, move under the ball's apex so we can spike it at its peak
            Vector3 moveTarget;
            if (pursue) moveTarget = _attacking ? ApexPoint() : landing;
            else if (yielding || teamInPossession) moveTarget = SupportSpot(); // cover, don't crowd
            else moveTarget = _home;

            Vector3 to = moveTarget - GroundPosition;
            Vector2 dir = new Vector2(to.x, to.z);
            _desiredMove = dir.magnitude > 0.15f ? Vector2.ClampMagnitude(dir, 1f) : Vector2.zero;

            // In the air we only have a nudge (GameConfig.airControl) on top of our take-off
            // momentum, so steer on where that momentum is TAKING us: aim the predicted position
            // at the ball's, and leave the stick neutral (= keep momentum) when already on course.
            if (!IsGrounded && pursue)
            {
                float gAir = -Physics.gravity.y;
                float tLeft = Mathf.Max(VerticalVelocity / gAir, 0.1f); // to our apex (strike time)
                Vector2 drift = PlanarVelocity * tLeft;
                Vector3 ballThen = bp + SeenBallVel * tLeft;
                Vector2 miss = new Vector2(ballThen.x - GroundPosition.x - drift.x,
                                           ballThen.z - GroundPosition.z - drift.y);
                _desiredMove = miss.magnitude > 0.2f ? Vector2.ClampMagnitude(miss, 1f) : Vector2.zero;
            }

            // Up on something (a Cliff Hop ledge) with the attack coming to us: hold the high
            // ground and let the ball arrive, rather than shuffling the last bit and stepping off.
            if (IsGrounded && GroundHeight > 0.5f && pursue && _attacking
                && Vector2.Distance(new Vector2(GroundPosition.x, GroundPosition.z), new Vector2(moveTarget.x, moveTarget.z)) < 1.6f)
                _desiredMove = Vector2.zero;

            // Reaction latency is a sluggish first step, not a freeze: while "reacting" we
            // still visibly start toward the ball, just too slowly to make every get — the
            // imperfection reads as a late read instead of a statue watching the spike land.
            if (reacting) _desiredMove *= 0.35f;

            // Jump so we reach the top of our jump exactly when the ball is in the strike zone.
            // The time-to-apex and apex height both come from jumpSpeed, so the timing tracks
            // that value: predict where the ball will be after tApex and jump only if it'll be
            // reachable at the height of our jump.
            _jumpCooldown -= Time.fixedDeltaTime; // Decide runs once per simulation tick
            float g = -Physics.gravity.y;                        // OUR gravity (the jump)
            float tApex = jumpSpeed / g;                          // time for us to reach our apex
            float apexHeight = jumpSpeed * jumpSpeed / (2f * g);  // how high our jump reaches
            // highest we can contact at apex — from whatever we're standing on (a ledge...)
            float maxReach = GroundHeight + apexHeight + hitReachHeight;
            Vector3 ballAtApex = bp + SeenBallVel * tApex
                                 + 0.5f * (ball.EffectiveGravity + CourtEnvironment.Active.wind)
                                        * (tApex * tApex);
            // Jumps carry momentum, so judge the jump from where it will CARRY us by our apex —
            // a running approach that flies into the ball is a good jump; one that sails past
            // isn't, however close we are now. Air control can still make up a little.
            Vector2 carried = new Vector2(GroundPosition.x, GroundPosition.z) + PlanarVelocity * tApex;
            float airSlack = 0.5f * GameConfig.Instance.airControl * tApex * tApex * 0.6f;
            float hDistApex = Mathf.Max(0f, Vector2.Distance(carried, new Vector2(ballAtApex.x, ballAtApex.z)) - airSlack);
            if (pursue && _attacking && IsGrounded && touchesRemain && _jumpCooldown <= 0f
                && hDistApex < reach
                && ballAtApex.y >= spikeHeightThreshold && ballAtApex.y <= maxReach)
            {
                _wantJump = true;
                _jumpCooldown = 0.9f;
            }

            // Signature moves that ride on ordinary inputs while their ability runs.
            _blinkCooldown -= Time.fixedDeltaTime;
            if (BlinkCharges > 0 && pursue && landsOnOurSide && _blinkCooldown <= 0f
                && Vector2.Distance(new Vector2(GroundPosition.x, GroundPosition.z), new Vector2(landing.x, landing.z)) > reach + 0.8f)
            {
                _wantPower = true;           // a blink along the steer toward the ball
                _blinkCooldown = 0.35f;
            }
            if (CanDoubleJump && pursue && _attacking && VerticalVelocity < 0.5f
                && bp.y > SimPosition.y + hitReachHeight - 0.2f && bp.y < SimPosition.y + hitReachHeight + 2f)
                _wantJump = true;            // the second hop, to meet a ball above our reach
            _wantJumpHeld = CanGlide && !IsGrounded && pursue;

            // Emergency dive: the ball will drop too far away to run to in time, but a dive's
            // burst of speed can still get a platform under it. Only when defending/receiving
            // (never to start an attack), only if we're upright on the ground, and only for a
            // ball that actually comes down on OUR side — a dive can never reach across the
            // net, so a teammate's block landing just over it must not bait one.
            if (pursue && landsOnOurSide && !_attacking && IsGrounded && !IsDiving && touchesRemain && tLand < 1.1f)
            {
                var cfg = GameConfig.Instance;
                float dist = Vector2.Distance(new Vector2(GroundPosition.x, GroundPosition.z),
                                              new Vector2(landing.x, landing.z));
                bool canRunThere = dist <= moveSpeed * tLand + reach * 0.6f;
                bool diveGetsThere = dist <= moveSpeed * Mathf.Max(tLand - cfg.diveDuration, 0f)
                                             + diveSpeed * cfg.diveDuration + cfg.diveReach;
                if (!canRunThere && diveGetsThere) _wantDive = true;
            }

            // Contact only a ball that's actually coming to us (never swat one we just sent
            // over). Plus: the single closest *eligible* teammate — never two players on one
            // ball, never the player who just touched it, and never a 4th touch.
            if (rallyLive && !reacting && touchesRemain && ballComingToUs && BallInReach()
                && !yielding && (invited || ClosestEligibleTo(bp)))
                _wantHit = true;

            // Blocking is a press now, so we have to go for it deliberately: take it the moment
            // the window opens. We don't model hesitation here — BlockTiming applies
            // aiBlockTiming as a flat handicap instead, so this stays a simple "yes, block" and
            // the difficulty lives in one config value. We still never JUMP to block: as before,
            // a block comes off a jump we were already making, so this changes when we press,
            // not how often we're up there to press at all.
            if (rallyLive && !reacting && BlockWindowOpen()) { _wantHit = true; _wantBlock = true; }

            // A full power-up fires at its cue moment, so the effect lands where it matters:
            // offensive buffs as we move in to attack, defensive ones as the opponents build
            // their attack, the cyclone right before our own serve.
            if (GameConfig.Instance.powerUpsEnabled && Power.IsFull && Power.Ability != null)
            {
                _wantPower = AbilityCue(Power.Ability.id, rallyLive, pursue, teamInPossession,
                                        landing, tLand, landsOnOurSide);
            }
            else if (GameConfig.Instance.powerUpsEnabled && Power.IsFull)
            {
                bool fire;
                switch (Power.Def.aiCue)
                {
                    case PowerAiCue.OwnAttack:
                        fire = rallyLive && pursue && _attacking; break;
                    case PowerAiCue.OwnServe:
                        fire = match != null && match.IsServePhaseFor(this); break;
                    case PowerAiCue.OwnPossession:
                        fire = rallyLive && teamInPossession; break;
                    case PowerAiCue.OpponentAttack:
                        fire = rallyLive && match != null
                               && match.Possession == team.Other() && match.Touches >= 2; break;
                    default: // Anytime
                        fire = rallyLive; break;
                }
                _wantPower = fire; // the command carries it; the simulation gates and fires
            }
        }

        /// <summary>Choose the kind and target of the next contact based on the touch count.</summary>
        void PlanHit(int nextTouch)
        {
            if (nextTouch >= 3)              // attack: send it over the net
            {
                _attacking = true;
                _hitType = HitType.Bump;     // becomes a Spike in GetCommand while airborne
                _hitTarget = OpponentTarget();
            }
            else if (nextTouch == 2)         // set: feed a teammate near the net
            {
                _attacking = false;
                _hitType = HitType.Set;
                _hitTarget = SetTarget();
            }
            else                             // receive: pass up to our own setter zone
            {
                _attacking = false;
                _hitType = HitType.Bump;
                _hitTarget = ReceiveTarget();
            }
        }

        // ---- targets -------------------------------------------------------

        Vector3 OpponentTarget()
        {
            // This is the AI's *intent* — a spot inside the opponents' court. Execution error
            // (the shared contact-error model in VolleyPlayer) is layered on at hit time, so a
            // pressured AI attack can stray out just like a human's.
            float sign = CourtGeometry.SideSign(team.Other());
            float x = Random.Range(-CourtGeometry.HalfWidth * 0.8f, CourtGeometry.HalfWidth * 0.8f);
            x = Mathf.Clamp(x, -CourtGeometry.HalfWidth + 0.3f, CourtGeometry.HalfWidth - 0.3f);
            float z = sign * CourtGeometry.HalfDepth * Random.Range(0.5f, 0.9f);
            return new Vector3(x, 0.6f, z);
        }

        Vector3 SetTarget()
        {
            VolleyPlayer spiker = BestSpikerMate();
            float x = spiker != null ? spiker.SimPosition.x : 0f;
            x = Mathf.Clamp(x, -CourtGeometry.HalfWidth + 0.5f, CourtGeometry.HalfWidth - 0.5f);
            // own side, just in front of the net so the spiker can jump on it
            return new Vector3(x, 0.6f, CourtGeometry.SideSign(team) * CourtGeometry.HalfDepth * 0.16f);
        }

        Vector3 ReceiveTarget()
            => new Vector3(0f, 0.6f, CourtGeometry.SideSign(team) * CourtGeometry.HalfDepth * 0.3f);

        Vector3 SupportSpot()
            => new Vector3(halfSign * CourtGeometry.HalfWidth * 0.35f, 0f,
                           CourtGeometry.SideSign(team) * CourtGeometry.HalfDepth * 0.28f);

        // ---- teammate awareness -------------------------------------------

        /// <summary>
        /// True if this player may take the ball: it is not the last player to have touched
        /// it (no consecutive contacts), and it is the closest of its remaining eligible
        /// teammates (the human included) to the point. Exact ties break by instance id.
        /// </summary>
        bool ClosestEligibleTo(Vector3 point)
        {
            if (ball != null && (Object)ball.LastTouchPlayer == this) return false; // I just hit it
            if (match == null || match.players == null) return true;

            Vector2 q = new Vector2(point.x, point.z);
            float mine = Vector2.Distance(new Vector2(GroundPosition.x, GroundPosition.z), q);
            foreach (var p in match.players)
            {
                if (p == null || p == this || p.team != team) continue;
                if (ball != null && (Object)ball.LastTouchPlayer == p) continue; // ineligible too
                float d = Vector2.Distance(new Vector2(p.SimPosition.x, p.SimPosition.z), q);
                if (d < mine) return false;
                if (Mathf.Approximately(d, mine) && p.GetInstanceID() < GetInstanceID()) return false;
            }
            return true;
        }

        /// <summary>The teammate (excluding self) currently closest to the net — our spiker.</summary>
        VolleyPlayer BestSpikerMate()
        {
            if (match == null || match.players == null) return null;
            VolleyPlayer best = null;
            float bestZ = float.MaxValue;
            foreach (var p in match.players)
            {
                if (p == null || p == this || p.team != team) continue;
                float distToNet = Mathf.Abs(p.SimPosition.z);
                if (distToNet < bestZ) { bestZ = distToNet; best = p; }
            }
            return best;
        }

        /// <summary>
        /// When to fire a signature ability — each one at the moment it actually swings a rally,
        /// read from the same picture of the rally the rest of the AI uses.
        /// </summary>
        bool AbilityCue(AbilityId id, bool rallyLive, bool pursue, bool teamInPossession,
                        Vector3 landing, float tLand, bool landsOnOurSide)
        {
            if (match == null) return false;
            bool theyHaveIt = rallyLive && match.Possession == team.Other();
            bool theyAttackNext = theyHaveIt && match.Touches >= 2;
            float runDist = Vector2.Distance(new Vector2(GroundPosition.x, GroundPosition.z),
                                             new Vector2(landing.x, landing.z));
            switch (id)
            {
                case AbilityId.FoxTrick:   // right before our attack, or on our serve
                    return (rallyLive && pursue && _attacking) || match.IsServePhaseFor(this);
                case AbilityId.BearSlam:   // going up for a spike: land on their blockers
                    return rallyLive && pursue && _attacking;
                case AbilityId.Burrow:     // a ball dropping on our side, well out of reach
                    return rallyLive && pursue && landsOnOurSide && tLand > 0.55f && tLand < 1.6f
                           && runDist > reach + 1.5f;
                case AbilityId.Charge:     // same, but a burst along the ground gets there
                    return rallyLive && pursue && landsOnOurSide && tLand > 0.35f && tLand < 1.1f
                           && runDist > moveSpeed * tLand * 0.8f;
                case AbilityId.Stampede:   // they're playing it: flatten them mid-rally
                case AbilityId.MudWallow:
                    return theyHaveIt && CourtGeometry.SideOf(SeenBallPos) == team.Other();
                case AbilityId.TallOrder:  // they're about to attack: raise the wall
                case AbilityId.Roar:
                case AbilityId.AntlerParry:
                case AbilityId.WideLoad:
                case AbilityId.Earthquake:
                case AbilityId.Iceberg:
                    return theyAttackNext;
                case AbilityId.NetWalker:  // at the net for their attack, or ours
                    return theyAttackNext || (rallyLive && pursue && _attacking);
                case AbilityId.IceRink:    // they're playing it
                case AbilityId.SandstormDevil:
                case AbilityId.Avalanche:
                case AbilityId.TunnelTrap:
                case AbilityId.CubeDrop:
                    return theyHaveIt && CourtGeometry.SideOf(SeenBallPos) == team.Other();
                case AbilityId.Blizzard:   // our shot is on its way over: let the gale take it
                    return rallyLive && ball.LastTouchTeam == team && !landsOnOurSide;
                case AbilityId.BananaBall: // right before our attack, or on our serve
                case AbilityId.Rampage:
                case AbilityId.PhantomStrike:
                    return (rallyLive && pursue && _attacking) || match.IsServePhaseFor(this);
                case AbilityId.Trampoline: // under our feet — once we're standing on our strike spot
                case AbilityId.CliffHop:
                    return rallyLive && pursue && _attacking && IsGrounded
                           && Vector2.Distance(new Vector2(GroundPosition.x, GroundPosition.z),
                                               new Vector2(ApexPoint().x, ApexPoint().z)) < 0.9f;
                case AbilityId.DoubleJump:
                case AbilityId.Glide:
                case AbilityId.BigStride:
                    return rallyLive && pursue && _attacking && IsGrounded;
                case AbilityId.Balance:    // about to set
                    return rallyLive && pursue && teamInPossession && match.Touches == 1;
                case AbilityId.StickyPaws: // catch the next one coming to us
                case AbilityId.HotSpring:
                    return rallyLive && pursue && landsOnOurSide && tLand < 1.8f;
                case AbilityId.SlowMo:     // a ball we can't make in time
                case AbilityId.Pounce:
                case AbilityId.Oasis:
                case AbilityId.BlinkHop:
                    return rallyLive && pursue && landsOnOurSide && tLand > 0.4f && tLand < 1.5f
                           && runDist > moveSpeed * tLand * 0.85f;
                case AbilityId.PackHunt:   // a ball coming down that isn't ours to reach
                    return rallyLive && landsOnOurSide && tLand > 0.5f && !pursue;
                case AbilityId.SoundBlast: // our shot over, with a defender under it: push it away
                    return rallyLive && ball.LastTouchTeam == team && !landsOnOurSide && tLand > 0.4f;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The ball as this AI perceives it. Normally just the ball — but while an opponent's Fox
        /// Trick decoy is in the air and it fooled us, the decoy, until it's about to land and
        /// the penny drops. Hits still test the REAL ball, so chasing the decoy costs us.
        /// </summary>
        Vector3 SeenBallPos => Decoyed(out DecoyBall d) ? d.Position : ball.transform.position;
        Vector3 SeenBallVel => Decoyed(out DecoyBall d) ? d.Velocity : ball.Body.linearVelocity;

        bool Decoyed(out DecoyBall d)
        {
            d = DecoyBall.Current;
            return d != null && ball.LastTouchTeam == team.Other() && d.Fools(this) && d.TimeToLand > 0.3f;
        }

        /// <summary>Horizontal point where the ball peaks (or where it lands if already falling).</summary>
        Vector3 ApexPoint()
        {
            Vector3 p = SeenBallPos;
            Vector3 v = SeenBallVel;
            if (v.y > 0.1f)
            {
                float tA = v.y / (-ball.EffectiveGravity.y);
                Vector3 w = CourtEnvironment.Active.wind;
                return new Vector3(p.x + v.x * tA + 0.5f * w.x * tA * tA, 0f,
                                   p.z + v.z * tA + 0.5f * w.z * tA * tA);
            }
            return PredictLanding();
        }

        Vector3 PredictLanding() => PredictLanding(out _);

        Vector3 PredictLanding(out float t)
        {
            Vector3 p = SeenBallPos;
            Vector3 v = SeenBallVel;
            float g = -ball.EffectiveGravity.y;
            const float targetY = 1f;

            float a = 0.5f * g;
            float b = -v.y;
            float c = targetY - p.y;
            float disc = b * b - 4f * a * c;

            if (disc <= 0f) t = Mathf.Max(v.y / g, 0.2f);
            else t = (-b + Mathf.Sqrt(disc)) / (2f * a);
            t = Mathf.Clamp(t, 0.05f, 4f);

            // compensate for the CONSTANT part of any regional wind (drift ~ ½·w·t²); the
            // gust component stays unmodelled on purpose — it reads as honest misjudgement
            Vector3 wind = CourtEnvironment.Active.wind;
            return new Vector3(p.x + v.x * t + 0.5f * wind.x * t * t, 0f,
                               p.z + v.z * t + 0.5f * wind.z * t * t);
        }
    }
}
