using System.Collections.Generic;
using UnityEngine;

namespace Volleyball
{
    public enum MatchState { Serving, Rallying, PointScored, MatchOver }

    /// <summary>
    /// The match "brain": owns score, serve flow, rally state, the 3-touch rule and
    /// in/out scoring. Other components query it (CanTeamTouch) and report to it
    /// (RegisterTouch); it listens to the ball's ground-contact event to end rallies.
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        // Rules and timing are global — edit them in the GameConfig asset.
        static GameConfig Cfg => GameConfig.Instance;
        public int pointsToWin => Cfg.pointsToWin;
        public int maxTouches => Cfg.maxTouches;
        public float pointPauseSeconds => Cfg.pointPauseSeconds;
        public float aiServeDelay => Cfg.aiServeDelay;

        [Header("References (auto-found if empty)")]
        public BallController ball;
        public List<VolleyPlayer> players = new List<VolleyPlayer>();

        public int ScoreA { get; private set; }
        public int ScoreB { get; private set; }
        public MatchState State { get; private set; }
        public string Banner { get; private set; } = "";
        public TeamSide ServingTeam { get; private set; } = TeamSide.A;
        public int Touches { get; private set; }
        public TeamSide Possession { get; private set; }

        float _timer;
        VolleyPlayer _server;
        VolleyPlayer _lastToucher;
        bool _serveTossed; // the server has tossed the ball for a jump serve

        /// <summary>True from the serve until the receiving team first touches it. You may not
        /// block a serve, so blocks check this.</summary>
        public bool ServeInFlight { get; private set; }

        void Start()
        {
            if (ball == null) ball = FindAnyObjectByType<BallController>();
            if (players.Count == 0)
                players.AddRange(FindObjectsByType<VolleyPlayer>());

            if (ball != null) ball.OnGroundHit += HandleGroundHit;
            BeginServe(TeamSide.A);
        }

        void OnDestroy()
        {
            if (ball != null) ball.OnGroundHit -= HandleGroundHit;
        }

        public bool CanTeamTouch(TeamSide t) => State == MatchState.Rallying;

        /// <summary>True while this player is the server and the serve hasn't happened yet.</summary>
        public bool IsServePhaseFor(VolleyPlayer p) => State == MatchState.Serving && _server == p;

        public void RegisterTouch(TeamSide t, VolleyPlayer p)
        {
            if (State != MatchState.Rallying) return;

            // a player may not contact the ball twice in a row
            if (p != null && p == _lastToucher)
            {
                VBLog.Event($"FAULT double-contact by '{p.name}' team={t}");
                EndRally(t.Other(), "double contact");
                return;
            }
            _lastToucher = p;

            if (t == Possession) Touches++;
            else { Possession = t; Touches = 1; }

            if (t != ServingTeam) ServeInFlight = false; // serve received — blocking is legal again

            if (Touches > maxTouches)
                EndRally(Possession.Other(), $"over {maxTouches} touches");
        }

        void HandleGroundHit(Vector3 point, Vector3 impactVel)
        {
            if (State != MatchState.Rallying) return;

            bool inBounds = CourtGeometry.InBounds(point);
            TeamSide landingSide = CourtGeometry.SideOf(point);
            TeamSide scorer = inBounds ? landingSide.Other() : ball.LastTouchTeam.Other();
            if (scorer == TeamSide.None) scorer = landingSide.Other();

            VBLog.Event($"BALL LANDED at {VBLog.V(point)} impactVel={VBLog.V(impactVel)} " +
                        $"inBounds={inBounds} side={landingSide} lastTouch={ball.LastTouchTeam}");
            GameAudio.PlayLanding(inBounds, point);
            EndRally(scorer, inBounds ? "in-bounds" : "out");
        }

        void EndRally(TeamSide scorer, string reason)
        {
            if (scorer == TeamSide.A) ScoreA++;
            else if (scorer == TeamSide.B) ScoreB++;

            VBLog.Event($"RALLY END scorer={scorer} reason='{reason}' -> score A={ScoreA} B={ScoreB}");

            ServingTeam = scorer;
            Banner = scorer == TeamSide.A ? "Point — You!" : "Point — Opponents";
            if (!string.IsNullOrEmpty(reason)) Banner += $" ({reason})";

            if (ScoreA >= pointsToWin || ScoreB >= pointsToWin)
            {
                State = MatchState.MatchOver;
                Banner = (ScoreA > ScoreB ? "You win the match!" : "Opponents win the match!")
                         + "  —  press Hit to play again";
                GameAudio.PlayMatchWin();
            }
            else
            {
                State = MatchState.PointScored;
                _timer = pointPauseSeconds;
                GameAudio.PlayPoint(scorer == TeamSide.A);
            }
        }

        void BeginServe(TeamSide t)
        {
            ServingTeam = t;
            Possession = t;
            Touches = 0;
            _lastToucher = null;
            State = MatchState.Serving;
            Banner = "";
            _serveTossed = false;
            ServeInFlight = false;

            ResetPositions();
            _server = FirstPlayerOf(t);

            // server stands behind their own back line to serve
            if (_server != null)
            {
                _server.transform.position = new Vector3(
                    0f, 0f, CourtGeometry.SideSign(t) * (CourtGeometry.HalfDepth + 0.8f));
                _server.ResetState();
            }

            ball.Hold(ServePosition());
            _timer = aiServeDelay;

            GameAudio.PlayWhistle(); // referee authorises the serve
            VBLog.Event($"BEGIN SERVE team={t} server='{(_server != null ? _server.name : "?")}' score A={ScoreA} B={ScoreB}");
        }

        void Update()
        {
            switch (State)
            {
                case MatchState.Serving:
                    if (_server is AIController)
                    {
                        if (!_serveTossed) ball.Hold(ServePosition());
                        _timer -= Time.deltaTime;
                        if (_timer <= 0f) DoServe(); // AI always serves underhand
                    }
                    else
                    {
                        var gi = GameInput.Instance;
                        if (!_serveTossed)
                        {
                            ball.Hold(ServePosition());
                            if (gi != null)
                            {
                                if (gi.BumpPressed) DoServe();        // underhand serve (J)
                                else if (gi.SetPressed) DoToss();      // toss up for a jump serve (K)
                            }
                        }
                        else
                        {
                            // ball is in the air: jump and Spike (L) to jump-serve it
                            Vector3 bp = ball.transform.position;
                            if (gi != null && gi.SpikePressed && _server != null
                                && !_server.IsGrounded && bp.y > 1.5f)
                                DoJumpServe();
                            else if (bp.y < 0.6f && ball.Body.linearVelocity.y <= 0f)
                                _serveTossed = false; // missed the toss — settle back to a held ball
                        }
                    }
                    break;

                case MatchState.PointScored:
                    _timer -= Time.deltaTime;
                    if (_timer <= 0f) BeginServe(ServingTeam);
                    break;

                case MatchState.MatchOver:
                    if (GameInput.Instance != null && GameInput.Instance.AnyHitPressed)
                        RestartMatch();
                    break;
            }
        }

        void DoServe()
        {
            State = MatchState.Rallying;
            Possession = ServingTeam;
            Touches = 1;
            _lastToucher = _server;
            _serveTossed = false;
            ServeInFlight = true;

            Vector3 target = VolleyPlayer.ApplyHitChaos(CourtGeometry.CourtCenter(ServingTeam.Other()));
            ball.LaunchTo(target, 4f, ServingTeam, _server, HitType.Serve); // high apex to clear the net from behind the baseline
            ball.LockHits(0.45f);
            _server?.TriggerSwing(HitType.Serve); // animate the serve (DoServe bypasses TryHit)
            GameAudio.PlayHit(HitType.Serve, ball.transform.position); // the serve contact (DoServe bypasses LogContact)

            Vector3 sv = ball.Body.linearVelocity;
            VBLog.Event($"Serve by '{(_server != null ? _server.name : "?")}' team={ServingTeam} touch#1 " +
                        $"vel={VBLog.V(sv)} speed={sv.magnitude:F1} spin={ball.Spin:F0}");
        }

        /// <summary>Toss the ball up for a jump serve; the server then jumps and spikes it.</summary>
        void DoToss()
        {
            _serveTossed = true;
            ball.Toss(6f);
            _server?.TriggerSwing(HitType.Set); // small toss motion
            VBLog.Event($"SERVE TOSS by '{(_server != null ? _server.name : "?")}' team={ServingTeam}");
        }

        /// <summary>
        /// Spike the tossed ball over as a jump serve. Contact height matters: the higher you
        /// hit it (the top of the jump), the flatter and faster the serve.
        /// </summary>
        void DoJumpServe()
        {
            State = MatchState.Rallying;
            Possession = ServingTeam;
            Touches = 1;
            _lastToucher = _server;
            _serveTossed = false;
            ServeInFlight = true;

            // higher contact → lower apex → flatter, faster, harder-to-receive serve
            float contactY = ball.transform.position.y;
            float apex = Mathf.Lerp(3.4f, 1.4f, Mathf.InverseLerp(1.6f, 4f, contactY));

            Vector3 target = VolleyPlayer.ApplyHitChaos(CourtGeometry.CourtCenter(ServingTeam.Other()));
            ball.LaunchTo(target, apex, ServingTeam, _server, HitType.Serve);
            ball.LockHits(0.45f);
            _server?.TriggerSwing(HitType.Spike); // spike motion for the jump serve
            GameAudio.PlayHit(HitType.Spike, ball.transform.position);

            Vector3 sv = ball.Body.linearVelocity;
            VBLog.Event($"JUMP SERVE by '{(_server != null ? _server.name : "?")}' team={ServingTeam} touch#1 " +
                        $"contactY={contactY:F2} apex={apex:F2} vel={VBLog.V(sv)} speed={sv.magnitude:F1} spin={ball.Spin:F0}");
        }

        void RestartMatch()
        {
            ScoreA = 0;
            ScoreB = 0;
            BeginServe(TeamSide.A);
        }

        Vector3 ServePosition()
        {
            if (_server == null) return new Vector3(0f, 1.5f, CourtGeometry.SideSign(ServingTeam) * CourtGeometry.HalfDepth * 0.9f);
            Vector3 p = _server.transform.position;
            return new Vector3(p.x, 1.5f, p.z + CourtGeometry.SideSign(ServingTeam) * 0.3f);
        }

        VolleyPlayer FirstPlayerOf(TeamSide t)
        {
            foreach (var p in players)
                if (p != null && p.team == t) return p;
            return players.Count > 0 ? players[0] : null;
        }

        void ResetPositions()
        {
            foreach (var p in players)
            {
                if (p == null) continue;
                float x = p.halfSign * CourtGeometry.HalfWidth * 0.45f;
                float z = CourtGeometry.SideSign(p.team) * CourtGeometry.HalfDepth * 0.55f;
                p.transform.position = new Vector3(x, 0f, z);
                p.ResetState();
            }
        }
    }
}
