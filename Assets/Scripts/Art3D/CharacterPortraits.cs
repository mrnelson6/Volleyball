using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Headshot sprites of the 3D animals, baked by the editor's PortraitBaker into
    /// <c>Resources/Portraits/animal_&lt;id&gt;.png</c> (blue jersey, transparent background).
    /// Menus use them wherever a character is shown as a picture.
    /// </summary>
    public static class CharacterPortraits
    {
        public const string ResourceDir = "Portraits";

        public static Sprite Get(string characterId)
            => Resources.Load<Sprite>($"{ResourceDir}/animal_{characterId}");
    }
}
