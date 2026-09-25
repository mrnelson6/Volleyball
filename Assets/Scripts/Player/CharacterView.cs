using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// What draws a player: the 2D billboard sprite (<see cref="CharacterAnimator"/>) or a
    /// generated 3D model (<see cref="ModelCharacterView"/>). Pure view — it only READS the
    /// <see cref="VolleyPlayer"/> it sits under (ViewGroundPosition, IsGrounded, IsDiving,
    /// Swung...), never feeds the simulation. Game code that needs "the player's visual" (power-up
    /// glow, giant growth, controller swaps) talks to this base so either view works.
    /// </summary>
    public abstract class CharacterView : MonoBehaviour
    {
        /// <summary>Point at a replacement player component (the online slot binder swaps
        /// PlayerController/AIController on the same GameObject at runtime).</summary>
        public abstract void Rebind(VolleyPlayer p);

        /// <summary>Re-read the view's resting local height after its transform was moved or
        /// scaled from outside (character swap, giant growth).</summary>
        public virtual void CaptureBaseLocalY() { }

        /// <summary>Power-up highlight: <paramref name="amount"/> 0 = none, 1 = full
        /// <paramref name="color"/>.</summary>
        public abstract void SetGlow(Color color, float amount);

        /// <summary>The view drawing <paramref name="player"/> right now (inactive fallbacks skipped).</summary>
        public static CharacterView Of(VolleyPlayer player)
            => player != null ? player.GetComponentInChildren<CharacterView>() : null;
    }
}
