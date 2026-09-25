using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Runtime switch between a player's 2D sprite view and its generated 3D model. The scene
    /// builders still bake the sprite child (the fallback for any animal without a model); when
    /// <c>Resources/Characters3D/animal_&lt;id&gt;</c> exists, a <see cref="ModelCharacterView"/>
    /// is instantiated under the player and the sprite child is deactivated. Everything happens
    /// at runtime on plain child objects, so baked scenes, network prefabs and in-scene hashes
    /// are untouched.
    /// </summary>
    public static class CharacterModels
    {
        public const string ResourceDir = "Characters3D";
        public const string ModelChildName = "Model";

        /// <summary>Master switch: false keeps every player on the 2D sprite view.</summary>
        public static bool Enabled = true;

        public static GameObject LoadPrefab(string characterId)
            => Enabled ? Resources.Load<GameObject>($"{ResourceDir}/animal_{characterId}") : null;

        /// <summary>Make sure <paramref name="player"/> is drawn by the right view for its
        /// current character (called from VolleyPlayer.Start).</summary>
        public static void Ensure(VolleyPlayer player)
        {
            if (player == null) return;
            var ch = player.Character;
            if (ch != null) Apply(player, ch);
        }

        /// <summary>
        /// Dress <paramref name="player"/> as <paramref name="ch"/> in 3D if a model exists:
        /// reuse the current model when it's already this animal (just re-colour the jersey),
        /// otherwise replace it. Without a model the sprite child is reactivated.
        /// </summary>
        public static void Apply(VolleyPlayer player, CharacterDef ch)
        {
            var sprite = player.GetComponentInChildren<CharacterAnimator>(true);
            var current = player.GetComponentInChildren<ModelCharacterView>(true);
            var prefab = LoadPrefab(ch.id);

            if (prefab == null)
            {
                if (current != null) Discard(current.gameObject);
                if (sprite != null) sprite.gameObject.SetActive(true);
                SetBlobShadow(player, true);
                return;
            }
            // the model casts a real shadow; the sprite-era blob would double it up
            SetBlobShadow(player, false);

            if (current != null && current.CharacterId == ch.id)
            {
                current.SetJersey(player.jerseyColor);
                current.Rebind(player);
                return;
            }

            bool hadGlow = HasGlow(player);
            if (current != null) Discard(current.gameObject);

            var go = Object.Instantiate(prefab, player.transform, false);
            go.name = ModelChildName;
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one;
            go.GetComponent<ModelCharacterView>().Init(player, ch, player.jerseyColor);

            if (sprite != null) sprite.gameObject.SetActive(false);
            if (hadGlow) EnsureGlow(player);
        }

        /// <summary>Hang the power-up glow on whichever view is active (idempotent).</summary>
        public static void EnsureGlow(VolleyPlayer player)
        {
            var view = CharacterView.Of(player);
            if (view != null && view.GetComponent<PowerUpGlow>() == null)
                view.gameObject.AddComponent<PowerUpGlow>();
        }

        static void SetBlobShadow(VolleyPlayer player, bool on)
        {
            foreach (var ds in Object.FindObjectsByType<DropShadow>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (ds.target == player.transform && ds.TryGetComponent(out Renderer r))
                    r.enabled = on;
        }

        static bool HasGlow(VolleyPlayer player)
            => player.GetComponentInChildren<PowerUpGlow>(true) != null;

        static void Discard(GameObject go)
        {
            // deactivate first: Destroy is deferred to end of frame, and lookups this frame
            // (CharacterView.Of) must already see the replacement
            go.SetActive(false);
            Object.Destroy(go);
        }
    }
}
