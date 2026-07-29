using UnityEngine;
using UnityEngine.UI;

namespace Volleyball
{
    /// <summary>
    /// Reflects match score, the current banner, (in the campaign) where on the world tour
    /// this match sits, and the local player's power-up meter. Everything renders from THIS
    /// viewer's perspective: the match reports structured facts (<see cref="BannerMessage"/>,
    /// raw scores) and the HUD turns them into "you/them" — so two humans on opposite teams
    /// can watch the same match and each read it correctly.
    /// </summary>
    public class ScoreHUD : MonoBehaviour
    {
        public MatchManager match;
        public Text scoreText;
        public Text bannerText;
        public Text matchLabelText; // "Sunny Savanna — Match 2/3 vs Stripe Sprinters"

        [Header("Power-up meter (local player)")]
        public Image powerFill;   // fill bar, sized via anchorMax like the select-screen stat bars
        public Text powerLabel;   // the power-up's name / ready hint

        // The serve instructions carry keybindings, so their text belongs to the client,
        // not to the (eventually server-side) match state.
        const string ServeHintText = "Your serve —  J: underhand    K: toss, then Space + L: jump serve";
        const string TossHintText = "Run in — Jump (Space) and Spike (L) at the peak!";

        VolleyPlayer _viewer; // the human this machine controls (null on an all-AI scene)

        void Start()
        {
            if (match == null) match = FindAnyObjectByType<MatchManager>();
            if (matchLabelText != null)
                matchLabelText.text = MatchSetup.Current?.matchLabel ?? "";

            foreach (var p in FindObjectsByType<VolleyPlayer>(FindObjectsSortMode.None))
                if (p.IsHuman && p.IsLocallyControlled) { _viewer = p; break; }
        }

        TeamSide ViewerTeam => _viewer != null ? _viewer.team : TeamSide.A;

        void Update()
        {
            if (match == null) return;

            if (scoreText != null)
            {
                bool viewerIsA = ViewerTeam == TeamSide.A;
                int mine = viewerIsA ? match.ScoreA : match.ScoreB;
                int theirs = viewerIsA ? match.ScoreB : match.ScoreA;
                scoreText.text = $"YOU  {mine} - {theirs}  {OpponentLabel()}";
            }
            if (bannerText != null) bannerText.text = RenderBanner(match.Banner);
            UpdatePowerMeter();
        }

        /// <summary>"CPU" against an all-AI team, "THEM" once any human plays over there.</summary>
        string OpponentLabel()
        {
            TeamSide opp = ViewerTeam.Other();
            if (match.players != null)
                foreach (var p in match.players)
                    if (p != null && p.team == opp && p.IsHuman) return "THEM";
            return "CPU";
        }

        string RenderBanner(BannerMessage b)
        {
            switch (b.kind)
            {
                case BannerKind.Raw:
                case BannerKind.PowerShout:
                    return b.text ?? "";

                case BannerKind.Point:
                {
                    string who = b.team == ViewerTeam ? "Point — You!" : "Point — Opponents";
                    return string.IsNullOrEmpty(b.text) ? who : $"{who} ({b.text})";
                }

                case BannerKind.MatchWon:
                    return (b.team == ViewerTeam ? "You win the match!" : "Opponents win the match!")
                           + "  —  press Hit to play again";

                case BannerKind.Perfect:
                    return "PERFECT SERVE!";

                // Serve instructions only for the player actually holding the ball; anyone
                // else just sees whose serve it is.
                case BannerKind.ServeHint:
                    return match.IsServePhaseFor(_viewer) ? ServeHintText : $"{b.text}'s serve";
                case BannerKind.TossHint:
                    return match.IsServePhaseFor(_viewer) ? TossHintText : $"{b.text}'s serve";

                case BannerKind.AiServing:
                    return b.team == ViewerTeam ? $"{b.text}'s serve" : "";

                default:
                    return "";
            }
        }

        void UpdatePowerMeter()
        {
            if (powerFill == null) return;

            GameObject bar = powerFill.transform.parent != null
                ? powerFill.transform.parent.gameObject : powerFill.gameObject;
            bool show = _viewer != null && GameConfig.Instance.powerUpsEnabled;
            if (bar.activeSelf != show) bar.SetActive(show);
            if (!show) return;

            PowerUpState power = _viewer.Power;
            PowerUpDef def = power.Def;
            PowerUpDef active = power.OwnActiveDef;

            // charging: the bar fills up; active: it drains down as the effect runs out
            float frac = active != null ? power.OwnActiveRemaining01 : power.Charge;
            RectTransform rt = powerFill.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(Mathf.Clamp01(frac), 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Color c = def.color;
            if (active == null && power.IsFull)
                c = Color.Lerp(def.color, Color.white, Mathf.PingPong(Time.time * 2.4f, 0.6f));
            powerFill.color = c;

            if (powerLabel != null)
            {
                string hint = _viewer is PlayerController pc ? pc.Input.PowerHintLabel : "";
                powerLabel.text = active != null ? $"{def.displayName}!"
                                : power.IsFull   ? (hint == "" ? $"{def.displayName} ready"
                                                              : $"{def.displayName} ready — {hint}")
                                                 : def.displayName;
            }
        }
    }
}
