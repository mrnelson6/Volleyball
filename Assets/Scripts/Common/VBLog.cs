using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Lightweight event logger for diagnosing ball behaviour. Every line is prefixed and
    /// timestamped (game time + frame) so the sequence of events is unambiguous when shared.
    /// Toggle with <see cref="Enabled"/>.
    /// </summary>
    public static class VBLog
    {
        public static bool Enabled = true;

        public static void Event(string msg)
        {
            if (!Enabled) return;
            Debug.Log($"[VB t={Time.time:F2} f={Time.frameCount}] {msg}");
        }

        public static string V(Vector3 v) => $"({v.x:F1},{v.y:F1},{v.z:F1})";
    }
}
