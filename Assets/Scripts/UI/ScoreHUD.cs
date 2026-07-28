using UnityEngine;
using UnityEngine.UI;

namespace Volleyball
{
    /// <summary>Reflects match score, the current banner message, and (in the campaign)
    /// where on the world tour this match sits.</summary>
    public class ScoreHUD : MonoBehaviour
    {
        public MatchManager match;
        public Text scoreText;
        public Text bannerText;
        public Text matchLabelText; // "Sunny Savanna — Match 2/3 vs Stripe Sprinters"

        void Start()
        {
            if (match == null) match = FindAnyObjectByType<MatchManager>();
            if (matchLabelText != null)
                matchLabelText.text = MatchSetup.matchLabel ?? "";
        }

        void Update()
        {
            if (match == null) return;
            if (scoreText != null) scoreText.text = $"YOU  {match.ScoreA} - {match.ScoreB}  CPU";
            if (bannerText != null) bannerText.text = match.Banner;
        }
    }
}
