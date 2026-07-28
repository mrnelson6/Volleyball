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
    /// </summary>
    public class AIController : VolleyPlayer
    {
        // AI tuning is global — edit it in the GameConfig asset.
        float spikeHeightThreshold => GameConfig.Instance.aiSpikeHeightThreshold;

        // AI contacts run through the same skill/error model as the human, scaled by aiErrorMult.
        // A campaign match overrides the global value per-opponent-team (the difficulty ramp).
        protected override float ContactSkill
            => MatchSetup.aiErrorMult > 0f ? MatchSetup.aiErrorMult
                                           : GameConfig.Instance.aiErrorMult;

        // Campaign difficulty also scales how fast the AI reacts to an incoming ball.
        static float ReactionScale => MatchSetup.aiReactionScale > 0f ? MatchSetup.aiReactionScale : 1f;

        Vector3 _home;
        Vector2 _desiredMove;
        bool _wantJump;
        bool _wantDive;
        bool _wantHit;
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

        protected override void Update()
        {
            Decide();
            base.Update();
        }

        void Decide()
        {
            _desiredMove = Vector2.zero;
            _wantJump = false;
            _wantDive = false;
            _wantHit = false;
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

            Vector3 bp = ball.transform.position;
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
            bool pursue = rallyLive && ballComingToUs && !ownServeInFlight && ClosestEligibleTo(landing);

            // when attacking, move under the ball's apex so we can spike it at its peak
            Vector3 moveTarget;
            if (pursue) moveTarget = _attacking ? ApexPoint() : landing;
            else if (teamInPossession) moveTarget = SupportSpot();
            else moveTarget = _home;

            Vector3 to = moveTarget - GroundPosition;
            Vector2 dir = new Vector2(to.x, to.z);
            _desiredMove = dir.magnitude > 0.15f ? Vector2.ClampMagnitude(dir, 1f) : Vector2.zero;

            // Reaction latency is a sluggish first step, not a freeze: while "reacting" we
            // still visibly start toward the ball, just too slowly to make every get — the
            // imperfection reads as a late read instead of a statue watching the spike land.
            if (reacting) _desiredMove *= 0.35f;

            // Jump so we reach the top of our jump exactly when the ball is in the strike zone.
            // The time-to-apex and apex height both come from jumpSpeed, so the timing tracks
            // that value: predict where the ball will be after tApex and jump only if it'll be
            // reachable at the height of our jump.
            _jumpCooldown -= Time.deltaTime;
            float g = -Physics.gravity.y;
            float tApex = jumpSpeed / g;                          // time for us to reach our apex
            float apexHeight = jumpSpeed * jumpSpeed / (2f * g);  // how high our jump reaches
            float maxReach = apexHeight + hitReachHeight;         // highest we can contact at apex
            Vector3 ballAtApex = bp + ball.Body.linearVelocity * tApex
                                 + 0.5f * (Physics.gravity + CourtEnvironment.Active.wind)
                                        * (tApex * tApex);
            float hDistApex = Vector2.Distance(new Vector2(GroundPosition.x, GroundPosition.z),
                                               new Vector2(ballAtApex.x, ballAtApex.z));
            if (pursue && _attacking && IsGrounded && touchesRemain && _jumpCooldown <= 0f
                && hDistApex < reach
                && ballAtApex.y >= spikeHeightThreshold && ballAtApex.y <= maxReach)
            {
                _wantJump = true;
                _jumpCooldown = 0.9f;
            }

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
            if (rallyLive && !reacting && touchesRemain && ballComingToUs && BallInReach() && ClosestEligibleTo(bp))
                _wantHit = true;
        }

        /// <summary>Choose the kind and target of the next contact based on the touch count.</summary>
        void PlanHit(int nextTouch)
        {
            if (nextTouch >= 3)              // attack: send it over the net
            {
                _attacking = true;
                _hitType = HitType.Bump;     // becomes a Spike in TryGetDesiredHit while airborne
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

        protected override Vector2 ReadMove() => _desiredMove;
        protected override bool ReadJumpPressed() => _wantJump;
        protected override bool ReadDivePressed() => _wantDive;

        protected override bool TryGetDesiredHit(out HitType type)
        {
            // a planned attack becomes a real spike once airborne, otherwise a driven bump
            type = (_attacking && !IsGrounded) ? HitType.Spike : _hitType;
            return _wantHit;
        }

        protected override Vector3 ChooseHitTarget(HitType type) => _hitTarget;

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
            float x = spiker != null ? spiker.transform.position.x : 0f;
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
                float d = Vector2.Distance(new Vector2(p.transform.position.x, p.transform.position.z), q);
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
                float distToNet = Mathf.Abs(p.transform.position.z);
                if (distToNet < bestZ) { bestZ = distToNet; best = p; }
            }
            return best;
        }

        /// <summary>Horizontal point where the ball peaks (or where it lands if already falling).</summary>
        Vector3 ApexPoint()
        {
            Vector3 p = ball.transform.position;
            Vector3 v = ball.Body.linearVelocity;
            if (v.y > 0.1f)
            {
                float tA = v.y / (-Physics.gravity.y);
                Vector3 w = CourtEnvironment.Active.wind;
                return new Vector3(p.x + v.x * tA + 0.5f * w.x * tA * tA, 0f,
                                   p.z + v.z * tA + 0.5f * w.z * tA * tA);
            }
            return PredictLanding();
        }

        Vector3 PredictLanding() => PredictLanding(out _);

        Vector3 PredictLanding(out float t)
        {
            Vector3 p = ball.transform.position;
            Vector3 v = ball.Body.linearVelocity;
            float g = -Physics.gravity.y;
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
