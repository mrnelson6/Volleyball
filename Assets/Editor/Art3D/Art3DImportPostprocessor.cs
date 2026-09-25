using UnityEditor;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// Import settings for the generated 3D art under Assets/Art/ (written by
    /// Tools/blender/animal_gen.py and props_gen.py): no imported materials (everything uses
    /// Volleyball/Stylized + a palette strip), Blender axis conversion baked in, Generic rigs
    /// for animals with clips renamed "Rig|Idle" → "Idle", point-sampled palette textures, and
    /// sprite import for the baked character portraits.
    /// </summary>
    public class Art3DImportPostprocessor : AssetPostprocessor
    {
        const string ArtRoot = "Assets/Art/";
        const string CharRoot = "Assets/Art/Characters/";
        const string PortraitRoot = "Assets/Resources/" + CharacterPortraits.ResourceDir + "/";

        static readonly string[] LoopingClips = { "Idle", "Run", "Cheer" };

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ArtRoot)) return;
            var imp = (ModelImporter)assetImporter;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.bakeAxisConversion = true;
            imp.importCameras = false;
            imp.importLights = false;
            imp.importBlendShapes = false;
            imp.importNormals = ModelImporterNormals.Import;
            imp.globalScale = 1f;
            imp.useFileScale = true;

            if (assetPath.StartsWith(CharRoot))
            {
                imp.animationType = ModelImporterAnimationType.Generic;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.importAnimation = true;
                imp.optimizeGameObjects = false; // bones stay addressable (procedural layer, attach points)
            }
            else
            {
                imp.animationType = ModelImporterAnimationType.None;
                imp.importAnimation = false;
            }
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(CharRoot)) return;
            var imp = (ModelImporter)assetImporter;
            var clips = imp.defaultClipAnimations;
            foreach (var c in clips)
            {
                int bar = c.name.LastIndexOf('|');
                if (bar >= 0) c.name = c.name.Substring(bar + 1);
                c.loopTime = System.Array.IndexOf(LoopingClips, c.name) >= 0;
            }
            imp.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(PortraitRoot))
            {
                var pi = (TextureImporter)assetImporter;
                pi.textureType = TextureImporterType.Sprite;
                pi.spriteImportMode = SpriteImportMode.Single;
                pi.alphaIsTransparency = true;
                pi.mipmapEnabled = false;
                pi.sRGBTexture = true;
                return;
            }
            if (!assetPath.StartsWith(ArtRoot) || !assetPath.Contains("palette")) return;
            var imp = (TextureImporter)assetImporter;
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.mipmapEnabled = false;
            imp.filterMode = UnityEngine.FilterMode.Point;
            imp.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
