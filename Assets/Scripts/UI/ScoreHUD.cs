using UnityEngine;
using UnityEngine.UI;

namespace Volleyball
{
    /// <summary>Reflects match score and the current banner message on screen.</summary>
    public class ScoreHUD : MonoBehaviour
    {
        public MatchManager match;
        public Text scoreText;
        public Text bannerText;

        void Start()
        {
            if (match == null) match = FindAnyObjectByType<MatchManager>();
        }

        void Update()
        {
            if (match == null) return;
            if (scoreText != null) scoreText.text = $"YOU  {match.ScoreA} - {match.ScoreB}  CPU";
            if (bannerText != null) bannerText.text = match.Banner;
        }
    }
}
