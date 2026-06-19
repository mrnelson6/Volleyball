using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Player-facing options that persist across sessions. Currently just the five audio
    /// volumes the <see cref="GameAudio"/> mixer reads. Values live in <see cref="PlayerPrefs"/>
    /// and are mirrored onto <see cref="GameConfig.Instance"/> at boot — because GameAudio reads
    /// those volume fields live every frame, applying them once is enough to take effect anywhere.
    ///
    /// NOTE: we deliberately only set the in-memory fields on the GameConfig instance (never
    /// <c>EditorUtility.SetDirty</c>), so editing volumes in Play mode does not modify the
    /// shared <c>Resources/GameConfig.asset</c> on disk.
    /// </summary>
    public enum AudioChannel { Master, Sfx, Ambient, Movement, Crowd }

    public static class GameSettings
    {
        const string KeyPrefix = "vol.";

        /// <summary>Read a channel's volume — the saved value, or the GameConfig default if unset.</summary>
        public static float GetVolume(AudioChannel ch)
        {
            return PlayerPrefs.GetFloat(KeyPrefix + ch, Default(ch));
        }

        /// <summary>Set, persist, and live-apply a channel's volume.</summary>
        public static void SetVolume(AudioChannel ch, float value)
        {
            value = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(KeyPrefix + ch, value);
            PlayerPrefs.Save();
            Apply(ch, value);
        }

        /// <summary>The GameConfig built-in default for a channel (used when nothing is saved yet).</summary>
        static float Default(AudioChannel ch)
        {
            var cfg = GameConfig.Instance;
            return ch switch
            {
                AudioChannel.Master => cfg.masterVolume,
                AudioChannel.Sfx => cfg.sfxVolume,
                AudioChannel.Ambient => cfg.ambientVolume,
                AudioChannel.Movement => cfg.movementVolume,
                AudioChannel.Crowd => cfg.crowdVolume,
                _ => 1f,
            };
        }

        static void Apply(AudioChannel ch, float value)
        {
            var cfg = GameConfig.Instance;
            switch (ch)
            {
                case AudioChannel.Master: cfg.masterVolume = value; break;
                case AudioChannel.Sfx: cfg.sfxVolume = value; break;
                case AudioChannel.Ambient: cfg.ambientVolume = value; break;
                case AudioChannel.Movement: cfg.movementVolume = value; break;
                case AudioChannel.Crowd: cfg.crowdVolume = value; break;
            }
        }

        /// <summary>Push every saved volume onto GameConfig at startup, before the first scene loads.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void ApplyAll()
        {
            foreach (AudioChannel ch in System.Enum.GetValues(typeof(AudioChannel)))
                Apply(ch, GetVolume(ch));
        }
    }
}
