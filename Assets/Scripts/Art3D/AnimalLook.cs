using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Colours a generated 3D animal model: pushes the (character, jersey) palette into every
    /// child renderer through a MaterialPropertyBlock, so all animals share one material.
    /// Runs in edit mode too, so generated scenes and the look-dev contact sheet show real colours.
    /// </summary>
    [ExecuteAlways]
    public class AnimalLook : MonoBehaviour
    {
        static readonly int PaletteId = Shader.PropertyToID("_PaletteTex");

        public string characterId = CharacterRoster.DefaultId;
        public Color jersey = new Color(0.20f, 0.50f, 0.95f);

        MaterialPropertyBlock _mpb;

        void OnEnable() => Apply();
        void OnValidate() => Apply();

        public void Set(string id, Color jerseyColor)
        {
            characterId = id;
            jersey = jerseyColor;
            Apply();
        }

        public void Apply()
        {
            var ch = CharacterRoster.Get(characterId);
            if (ch == null) return;
            var tex = AnimalPalette.Get(ch, jersey);
            _mpb ??= new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                r.GetPropertyBlock(_mpb);
                _mpb.SetTexture(PaletteId, tex);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
