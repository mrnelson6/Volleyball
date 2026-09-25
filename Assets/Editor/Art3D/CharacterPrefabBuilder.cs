using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// Wraps each generated animal FBX (Assets/Art/Characters/animal_&lt;id&gt;.fbx) into a
    /// runtime prefab at Resources/Characters3D/animal_&lt;id&gt;: toon material, AnimalLook
    /// palette, and a <see cref="ModelCharacterView"/> wired to the FBX's Animator and clips.
    /// CharacterModels loads these by id at runtime. Part of the World Tour build.
    /// </summary>
    public static class CharacterPrefabBuilder
    {
        public const string PrefabDir = "Assets/Resources/" + CharacterModels.ResourceDir;

        [MenuItem("Volleyball/3D/Build Character Prefabs", priority = 203)]
        public static void BuildAll()
        {
            AssetDatabase.Refresh();
            Directory.CreateDirectory(PrefabDir);
            var mat = ToonArtKit.AnimalMaterial();

            int built = 0;
            foreach (string fbx in Directory.GetFiles(ToonArtKit.CharDir, "animal_*.fbx"))
            {
                string assetPath = fbx.Replace('\\', '/');
                string id = Path.GetFileNameWithoutExtension(assetPath).Substring("animal_".Length);
                if (CharacterRoster.All.All(c => c.id != id)) continue; // stale export
                if (Build(id, assetPath, mat)) built++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Volleyball] Built {built} 3D character prefabs in {PrefabDir}");
        }

        static bool Build(string id, string fbxPath, Material mat)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null) return false;

            var root = new GameObject("animal_" + id);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.transform.SetParent(root.transform, false);
            // generated animals face -Z after import; turn them so the prefab root's forward
            // (+Z, what ModelCharacterView steers) is the way the animal looks
            inst.transform.localRotation = Quaternion.Euler(0f, ToonArtKit.ModelFacingYaw, 0f) * inst.transform.localRotation;

            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.On;
                // dives and jumps move the skinned body well outside its bind-pose bounds
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }

            var animator = inst.GetComponent<Animator>() ?? inst.AddComponent<Animator>();
            animator.runtimeAnimatorController = null; // driven by a PlayableGraph
            animator.applyRootMotion = false;

            var look = root.AddComponent<AnimalLook>();
            look.characterId = id;

            var view = root.AddComponent<ModelCharacterView>();
            view.animator = animator;
            var clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            AnimationClip Clip(string n)
            {
                if (clips.TryGetValue(n, out var c)) return c;
                Debug.LogWarning($"[Volleyball] {fbxPath} has no '{n}' clip");
                return null;
            }
            view.idle = Clip("Idle");
            view.run = Clip("Run");
            view.jump = Clip("Jump");
            view.spike = Clip("Spike");
            view.bump = Clip("Bump");
            view.set = Clip("Set");
            view.block = Clip("Block");
            view.dive = Clip("Dive");
            view.cheer = Clip("Cheer");

            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/animal_{id}.prefab");
            Object.DestroyImmediate(root);
            return true;
        }
    }
}
