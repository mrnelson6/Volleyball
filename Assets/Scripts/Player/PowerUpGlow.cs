using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Highlights the player's view by their power-up state: a pulsing glow in the power-up's
    /// colour while the meter is full, a steady one while their own cast is running, none
    /// otherwise. Added at runtime onto the active <see cref="CharacterView"/> (sprite or 3D
    /// model — see VolleyPlayer.Start and CharacterModels), so no scene rebuild is needed.
    /// </summary>
    public class PowerUpGlow : MonoBehaviour
    {
        CharacterView _view;
        VolleyPlayer _player;

        void Awake()
        {
            _view = GetComponent<CharacterView>();
            _player = GetComponentInParent<VolleyPlayer>();
        }

        void LateUpdate()
        {
            if (_view == null || _player == null) return;

            PowerUpState power = _player.Power;
            PowerUpDef active = power.OwnActiveDef;
            if (active != null)
                _view.SetGlow(active.color, 0.6f);
            else if (power.IsFull && GameConfig.Instance.powerUpsEnabled)
                _view.SetGlow(power.Def.color, Mathf.PingPong(Time.time * 2.4f, 0.55f));
            else
                _view.SetGlow(Color.white, 0f);
        }
    }
}
