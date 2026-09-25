using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// Bakes the World Tour screen's art (committed PNGs; needs a GPU — run from the editor menu
    /// or batch WITHOUT -nographics):
    /// <list type="bullet">
    /// <item><b>Toon world map</b> — Assets/Art/Map/prop_world_map.fbx (Tools/blender/map_gen.py)
    /// rendered straight down with an orthographic camera, so a region's
    /// <see cref="RegionDef.mapSpot"/> UV maps linearly onto the image, like the old pixel map.</item>
    /// <item><b>Arena postcards</b> — each region's toon arena through its broadcast camera
    /// (2D sprite players hidden; the 3D animals are runtime-only).</item>
    /// </list>
    /// </summary>
    public static class WorldTourArtBaker
    {
        public const string MapPath = "Assets/Resources/UI/world_map_toon.png";
        public const string ThumbDir = "Assets/Resources/UI/ArenaThumbs";
        const float MapW = 36f, MapH = 18f; // map_gen.py's world size

        [MenuItem("Volleyball/3D/Bake World Tour Art (map + postcards)", priority = 205)]
        public static void BakeAll()
        {
            BakeMap();
            BakeArenaThumbs();
            AssetDatabase.Refresh();
        }

        public static void BakeMap()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Map Rig").transform;
            var env = ToonArtKit.BuildLighting(root);
            env.preset.sunEuler = new Vector3(52f, -35f, 0f); // raking light so the relief reads
            env.preset.sunIntensity = 1.15f;
            env.Apply();
            RenderSettings.fog = false;

            var mat = ToonArtKit.MaterialsFor("Assets/Art/Map", "Map").props;
            mat.SetFloat("_OutlineWidth", 0.6f);
            ToonArtKit.PropFrom("Assets/Art/Map", "world_map", Vector3.zero, 0f, 1f, mat, root);

            // a tilted "diorama" view from the south — relief, mountains and landmarks read as 3D
            var cam = new GameObject("Map Camera").AddComponent<Camera>();
            cam.fieldOfView = 30f;
            cam.aspect = 2f;
            var look = new Vector3(0f, 0f, 0.6f);
            cam.transform.position = look + Quaternion.Euler(MapPitch, 0f, 0f) * new Vector3(0f, 0f, -MapDistance);
            cam.transform.LookAt(look);
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.20f, 0.50f, 0.78f);

            Render(cam, 2048, 1024, MapPath);
            WritePins(cam);
            Debug.Log("[Volleyball] Baked toon world map to " + MapPath);
        }

        public const string PinsPath = "Assets/Resources/UI/world_map_pins.txt";
        const float MapPitch = 58f, MapDistance = 35f;

        /// <summary>
        /// Where each region's landmark landed in the tilted render, as viewport UV — the menu
        /// builder places the UI pins from this (a perspective view is no longer a linear
        /// function of <see cref="RegionDef.mapSpot"/>). Ground height comes from a raycast.
        /// </summary>
        static void WritePins(Camera cam)
        {
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
                if (mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            Physics.SyncTransforms();

            var sb = new System.Text.StringBuilder();
            foreach (RegionDef r in RegionRoster.All)
            {
                var p = new Vector3((r.mapSpot.x - 0.5f) * MapW, 20f, (r.mapSpot.y - 0.5f) * MapH);
                float y = Physics.Raycast(p, Vector3.down, out RaycastHit hit, 40f) ? hit.point.y : 0f;
                Vector3 vp = cam.WorldToViewportPoint(new Vector3(p.x, y, p.z));
                sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0} {1:0.0000} {2:0.0000}", r.id, vp.x, vp.y));
            }
            File.WriteAllText(PinsPath, sb.ToString());
        }

        /// <summary>Region pin positions baked with the map (viewport UV), or null if not baked.</summary>
        public static System.Collections.Generic.Dictionary<string, Vector2> ReadPins()
        {
            if (!File.Exists(PinsPath)) return null;
            var d = new System.Collections.Generic.Dictionary<string, Vector2>();
            foreach (var line in File.ReadAllLines(PinsPath))
            {
                var parts = line.Split(' ');
                if (parts.Length != 3) continue;
                d[parts[0]] = new Vector2(
                    float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
            }
            return d;
        }

        public static void BakeArenaThumbs()
        {
            Directory.CreateDirectory(ThumbDir);
            foreach (RegionDef region in RegionRoster.All)
            {
                string scenePath = $"Assets/Scenes/{region.sceneName}.unity";
                if (!File.Exists(scenePath)) continue;
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                foreach (var sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)) sr.enabled = false;
                foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false;
                Object.FindFirstObjectByType<ToonEnvironment>()?.Apply();
                var cam = Camera.main;
                if (cam == null) continue;
                Render(cam, 640, 360, $"{ThumbDir}/{region.sceneName}.png");
            }
            Debug.Log("[Volleyball] Baked arena postcards into " + ThumbDir);
        }

        static void Render(Camera cam, int w, int h, string path)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = prev;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
        }
    }
}
