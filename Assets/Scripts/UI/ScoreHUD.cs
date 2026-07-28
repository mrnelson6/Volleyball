using UnityEngine;
using UnityEngine.UI;

namespace Volleyball
{
    /// <summary>Reflects match score, the current banner message, (in the campaign) where on
    /// the world tour this match sits, and the human player's power-up meter.</summary>
    public class ScoreHUD : MonoBehaviour
    {
        public MatchManager match;
        public Text scoreText;
        public Text bannerText;
        public Text matchLabelText; // "Sunny Savanna — Match 2/3 vs Stripe Sprinters"

        [Header("Power-up meter (human player)")]
        public Image powerFill;   // fill bar, sized via anchorMax like the select-screen stat bars
        public Text powerLabel;   // the power-up's name / ready hint

        PlayerController _human;

        void Start()
        {
            if (match == null) match = FindAnyObjectByType<MatchManager>();
            if (matchLabelText != null)
                matchLabelText.text = MatchSetup.matchLabel ?? "";
            _human = FindAnyObjectByType<PlayerController>();
        }

        void Update()
        {
            if (match == null) return;
            if (scoreText != null) scoreText.text = $"YOU  {match.ScoreA} - {match.ScoreB}  CPU";
            if (bannerText != null) bannerText.text = match.Banner;
            UpdatePowerMeter();
        }

        void UpdatePowerMeter()
        {
            if (powerFill == null) return;

            GameObject bar = powerFill.transform.parent != null
                ? powerFill.transform.parent.gameObject : powerFill.gameObject;
            bool show = _human != null && GameConfig.Instance.powerUpsEnabled;
            if (bar.activeSelf != show) bar.SetActive(show);
            if (!show) return;

            PowerUpState power = _human.Power;
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
                powerLabel.text = active != null ? $"{def.displayName}!"
                                : power.IsFull   ? $"{def.displayName} ready — E"
                                                 : def.displayName;
        }
    }
}
