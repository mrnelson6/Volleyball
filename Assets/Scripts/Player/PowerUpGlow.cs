using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Tints the player's sprite by their power-up state: a pulsing glow in the power-up's
    /// colour while the meter is full, a steady tint while their own cast is running, plain
    /// white otherwise. Added at runtime by VolleyPlayer.Start onto the sprite child (no
    /// scene rebuild needed). The colour channel is free — CharacterAnimator only ever swaps
    /// sprites, never tints.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PowerUpGlow : MonoBehaviour
    {
        SpriteRenderer _sr;
        VolleyPlayer _player;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _player = GetComponentInParent<VolleyPlayer>();
        }

        void LateUpdate()
        {
            if (_sr == null || _player == null) return;

            PowerUpState power = _player.Power;
            Color c = Color.white;
            PowerUpDef active = power.OwnActiveDef;
            if (active != null)
                c = Color.Lerp(Color.white, active.color, 0.6f);
            else if (power.IsFull && GameConfig.Instance.powerUpsEnabled)
                c = Color.Lerp(Color.white, power.Def.color, Mathf.PingPong(Time.time * 2.4f, 0.55f));
            _sr.color = c;
        }
    }
}
