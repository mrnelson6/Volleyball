using UnityEngine;
using UnityEngine.UI;

namespace Volleyball
{
    /// <summary>
    /// Audio settings page: one slider per <see cref="AudioChannel"/>. Slider changes write
    /// through <see cref="GameSettings"/>, which both persists the value and applies it live to
    /// <see cref="GameConfig.Instance"/> (the audio mixer reads those fields every frame, so the
    /// change is audible immediately). Refs are wired by the editor scene builder.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [System.Serializable]
        public class Row
        {
            public AudioChannel channel;
            public Slider slider;
            public Text valueLabel; // optional "73%" readout
        }

        public Row[] rows;
        public Button backButton;

        void Awake()
        {
            // Hook each slider once; the lambda captures its row.
            if (rows != null)
            {
                foreach (var row in rows)
                {
                    if (row?.slider == null) continue;
                    var r = row;
                    r.slider.onValueChanged.AddListener(v => OnChanged(r, v));
                }
            }
            if (backButton != null) backButton.onClick.AddListener(Close);
        }

        void OnEnable() => Refresh();

        /// <summary>Pull current saved values into the sliders (without re-triggering writes).</summary>
        void Refresh()
        {
            if (rows == null) return;
            foreach (var row in rows)
            {
                if (row?.slider == null) continue;
                float v = GameSettings.GetVolume(row.channel);
                row.slider.SetValueWithoutNotify(v);
                UpdateLabel(row, v);
            }
        }

        void OnChanged(Row row, float v)
        {
            GameSettings.SetVolume(row.channel, v);
            UpdateLabel(row, v);
        }

        static void UpdateLabel(Row row, float v)
        {
            if (row.valueLabel != null) row.valueLabel.text = Mathf.RoundToInt(v * 100f) + "%";
        }

        void Close() => gameObject.SetActive(false);
    }
}
