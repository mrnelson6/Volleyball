using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// Renders a headshot of every 3D animal prefab (Resources/Characters3D) in the toon style —
    /// blue jersey, idle pose, transparent background — to
    /// <c>Resources/Portraits/animal_&lt;id&gt;.png</c>, used by the character-select grid and the
    /// online lobby cards (<see cref="CharacterPortraits"/>).
    ///
    /// Needs a GPU: run it from the editor menu, or batch WITHOUT -nographics
    /// (<c>-executeMethod Volleyball.EditorTools.PortraitBaker.BakeAll</c>). The PNGs are committed,
    /// so the -nographics World Tour / player builds just use them.
    /// </summary>
    public static class PortraitBaker
    {
        public const string OutDir = "Assets/Resources/" + CharacterPortraits.ResourceDir;
        const int Size = 256;

        [MenuItem("Volleyball/3D/Bake Character Portraits", priority = 204)]
        public static void BakeAll()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Portrait Rig").transform;
            var env = ToonArtKit.BuildLighting(root);
            env.preset.sunEuler = new Vector3(35f, 150f, 0f); // key light from the camera side, onto the face
            env.Apply();

            var cam = new GameObject("Portrait Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 28f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 50f;
            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
            cam.targetTexture = rt;
            var grab = new Texture2D(Size, Size, TextureFormat.RGBA32, false);

            Directory.CreateDirectory(OutDir);
            int baked = 0;
            foreach (CharacterDef ch in CharacterRoster.All)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"{CharacterPrefabBuilder.PrefabDir}/animal_{ch.id}.prefab");
                if (prefab == null) continue;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.GetComponent<AnimalLook>()?.Set(ch.id, PlayerColors.Human);
                var view = inst.GetComponent<ModelCharacterView>();
                if (view != null && view.idle != null && view.animator != null)
                    view.idle.SampleAnimation(view.animator.gameObject, 0f);

                Transform head = FindDeep(inst.transform, "Head");
                float r = 0.3f * ch.height;                        // head radius, scaled with the body
                Vector3 look = head != null ? head.position + Vector3.up * r * 0.9f
                                            : Vector3.up * 1.8f * ch.height * 0.8f;
                float halfFrame = r * 2.4f;                         // head + ears/horns + a bit of jersey
                float dist = halfFrame / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                cam.transform.position = look + new Vector3(0.35f, 0.12f, 1f).normalized * dist;
                cam.transform.LookAt(look);

                cam.Render();
                RenderTexture.active = rt;
                grab.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                grab.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes($"{OutDir}/animal_{ch.id}.png", grab.EncodeToPNG());
                Object.DestroyImmediate(inst);
                baked++;
            }

            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(grab);
            AssetDatabase.Refresh();
            Debug.Log($"[Volleyball] Baked {baked} character portraits into {OutDir}");
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var hit = FindDeep(c, name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}
