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
        [Header("Rules")]
        public int pointsToWin = 7;
        public int maxTouches = 3;
        public float pointPauseSeconds = 1.5f;
        public float aiServeDelay = 1f;

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

        void Start()
        {
            if (ball == null) ball = FindFirstObjectByType<BallController>();
            if (players.Count == 0)
                players.AddRange(FindObjectsByType<VolleyPlayer>(FindObjectsSortMode.None));

            if (ball != null) ball.OnGroundHit += HandleGroundHit;
            BeginServe(TeamSide.A);
        }

        void OnDestroy()
        {
            if (ball != null) ball.OnGroundHit -= HandleGroundHit;
        }

        public bool CanTeamTouch(TeamSide t) => State == MatchState.Rallying;

        public void RegisterTouch(TeamSide t, VolleyPlayer p)
        {
            if (State != MatchState.Rallying) return;

            if (t == Possession) Touches++;
            else { Possession = t; Touches = 1; }

            if (Touches > maxTouches)
                EndRally(Possession.Other(), $"over {maxTouches} touches");
        }

        void HandleGroundHit(Vector3 point)
        {
            if (State != MatchState.Rallying) return;

            TeamSide scorer;
            if (CourtGeometry.InBounds(point))
                scorer = CourtGeometry.SideOf(point).Other();   // landed in a court -> opponents score
            else
                scorer = ball.LastTouchTeam.Other();             // sent out -> opponents of last toucher

            if (scorer == TeamSide.None)
                scorer = CourtGeometry.SideOf(point).Other();

            EndRally(scorer, "");
        }

        void EndRally(TeamSide scorer, string reason)
        {
            if (scorer == TeamSide.A) ScoreA++;
            else if (scorer == TeamSide.B) ScoreB++;

            ServingTeam = scorer;
            Banner = scorer == TeamSide.A ? "Point — You!" : "Point — Opponents";
            if (!string.IsNullOrEmpty(reason)) Banner += $" ({reason})";

            if (ScoreA >= pointsToWin || ScoreB >= pointsToWin)
            {
                State = MatchState.MatchOver;
                Banner = (ScoreA > ScoreB ? "You win the match!" : "Opponents win the match!")
                         + "  —  press Hit to play again";
            }
            else
            {
                State = MatchState.PointScored;
                _timer = pointPauseSeconds;
            }
        }

        void BeginServe(TeamSide t)
        {
            ServingTeam = t;
            Possession = t;
            Touches = 0;
            State = MatchState.Serving;
            Banner = "";

            ResetPositions();
            _server = FirstPlayerOf(t);
            ball.Hold(ServePosition());
            _timer = aiServeDelay;
        }

        void Update()
        {
            switch (State)
            {
                case MatchState.Serving:
                    if (_server != null) ball.Hold(ServePosition());
                    if (_server is AIController)
                    {
                        _timer -= Time.deltaTime;
                        if (_timer <= 0f) DoServe();
                    }
                    else if (GameInput.Instance != null && GameInput.Instance.HitPressed)
                    {
                        DoServe();
                    }
                    break;

                case MatchState.PointScored:
                    _timer -= Time.deltaTime;
                    if (_timer <= 0f) BeginServe(ServingTeam);
                    break;

                case MatchState.MatchOver:
                    if (GameInput.Instance != null && GameInput.Instance.HitPressed)
                        RestartMatch();
                    break;
            }
        }

        void DoServe()
        {
            State = MatchState.Rallying;
            Possession = ServingTeam;
            Touches = 1;

            Vector3 target = CourtGeometry.CourtCenter(ServingTeam.Other());
            target.x += Random.Range(-2f, 2f);
            ball.LaunchTo(target, 3f, ServingTeam, _server);
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
