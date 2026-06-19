using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// Builds a procedural "golden-hour beach arena" environment around an origin-centred court:
    /// a sunset skybox + sun, an ocean, palm trees, grandstands packed with a crowd, net posts,
    /// tiki torches with warm point lights, beach umbrellas, drifting clouds and a touch of haze.
    ///
    /// It is purely cosmetic. Every decorative mesh has its collider stripped and everything sits
    /// clear of the play volume, so the ball, scoring and player movement are never affected — the
    /// only colliders the ball can meet remain the GroundMarker plane and the Net built by
    /// <see cref="CourtKit"/>. Decorations are grouped under a single root so a designer can toggle
    /// or delete the whole dressing in one click.
    /// </summary>
    public static class ArenaDecorator
    {
        const string MatDir = "Assets/Materials/Arena";
        public const string DecorRootName = "Beach Arena Decor";

        // warm sunset palette
        static readonly Color SunColor = new Color(1.0f, 0.78f, 0.55f);
        static readonly Color SandColor = new Color(0.93f, 0.82f, 0.58f);
        static readonly Color OceanColor = new Color(0.10f, 0.42f, 0.62f);
        static readonly Color SkyTint = new Color(0.86f, 0.55f, 0.45f);

        static readonly Color[] CrowdColors =
        {
            new Color(0.90f, 0.30f, 0.28f), new Color(0.95f, 0.70f, 0.25f),
            new Color(0.25f, 0.55f, 0.85f), new Color(0.35f, 0.75f, 0.45f),
            new Color(0.85f, 0.85f, 0.88f), new Color(0.55f, 0.35f, 0.70f),
            new Color(0.95f, 0.55f, 0.65f),
        };

        /// <summary>One-call entry: environment, sun, camera and all decorations.</summary>
        public static void BuildSunsetBeachArena()
        {
            var root = GetOrCreateRoot(DecorRootName).transform;

            Light sun = ConfigureEnvironmentAndSun(root);
            BuildShowcaseCamera();

            BuildOcean(root);
            BuildGrandstand(root, Axis.AlongZ, -1f);   // main stand behind the far sideline (-X)
            BuildGrandstand(root, Axis.AlongX, 1f);     // end stand behind the far baseline (+Z)
            BuildNetPosts(root);
            BuildPalms(root);
            BuildTorches(root);
            BuildUmbrellas(root);
            BuildClouds(root);

            // keep the sun reference live for the skybox sun-disc
            if (sun != null) RenderSettings.sun = sun;
        }

        // ----------------------------------------------------------------- environment + sun

        public static Light ConfigureEnvironmentAndSun(Transform root)
        {
            // procedural sky tinted for sunset; the sun disc tracks the directional light
            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                Material sky = MakeOrLoadMaterial("SunsetSky", skyShader);
                sky.SetFloat("_SunSize", 0.07f);
                sky.SetFloat("_SunSizeConvergence", 3f);
                sky.SetFloat("_AtmosphereThickness", 1.5f);
                sky.SetColor("_SkyTint", SkyTint);
                sky.SetColor("_GroundColor", new Color(0.45f, 0.38f, 0.32f));
                sky.SetFloat("_Exposure", 1.35f);
                RenderSettings.skybox = sky;
            }

            // warm, low sun raking across the court
            var go = new GameObject("Sun (Directional Light)");
            go.transform.SetParent(root, true);
            var sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = SunColor;
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.6f;
            go.transform.rotation = Quaternion.Euler(16f, -52f, 0f); // low golden angle

            // warm ambient + light haze for depth
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.46f, 0.42f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.95f, 0.72f, 0.58f);
            RenderSettings.fogDensity = 0.006f;
            DynamicGI.UpdateEnvironment();

            return sun;
        }

        public static void BuildShowcaseCamera()
        {
            if (Camera.main != null || GameObject.FindGameObjectWithTag("MainCamera") != null)
                return;

            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox; // show the sunset
            cam.fieldOfView = 36f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 400f;                   // far enough to see the horizon/clouds
            go.AddComponent<AudioListener>();
            go.transform.position = new Vector3(20f, 12f, -3f);
            go.transform.LookAt(new Vector3(0f, 1.6f, 0f));
        }

        // ----------------------------------------------------------------- ocean

        static void BuildOcean(Transform root)
        {
            var ocean = GameObject.CreatePrimitive(PrimitiveType.Plane);
            StripCollider(ocean);
            ocean.name = "Ocean";
            ocean.transform.SetParent(root, true);
            ocean.transform.position = new Vector3(0f, -0.08f, 0f); // just below the sand court
            ocean.transform.localScale = new Vector3(60f, 1f, 60f); // ~600 x 600, runs to horizon
            ocean.GetComponent<MeshRenderer>().sharedMaterial =
                MakeLit("Ocean", OceanColor, smoothness: 0.85f, metallic: 0.1f);
        }

        // ----------------------------------------------------------------- grandstands

        enum Axis { AlongZ, AlongX }

        static void BuildGrandstand(Transform root, Axis axis, float side)
        {
            var stand = new GameObject(axis == Axis.AlongZ ? "Grandstand (Sideline)" : "Grandstand (End)");
            stand.transform.SetParent(root, true);

            const int tiers = 7;
            const float stepDepth = 1.4f;
            const float stepHeight = 0.8f;
            float length = (axis == Axis.AlongZ ? CourtGeometry.HalfDepth : CourtGeometry.HalfWidth) * 2f + 8f;
            float startOffset = (axis == Axis.AlongZ ? CourtGeometry.HalfWidth : CourtGeometry.HalfDepth) + 3f;

            Material concrete = MakeLit("StandConcrete", new Color(0.62f, 0.60f, 0.58f));

            for (int i = 0; i < tiers; i++)
            {
                float offset = side * (startOffset + i * stepDepth);
                float y = (i + 0.5f) * stepHeight;

                var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                StripCollider(step);
                step.name = $"Tier {i}";
                step.transform.SetParent(stand.transform, true);
                step.GetComponent<MeshRenderer>().sharedMaterial = concrete;

                if (axis == Axis.AlongZ)
                {
                    step.transform.position = new Vector3(offset, y, 0f);
                    step.transform.localScale = new Vector3(stepDepth, stepHeight + i * stepHeight, length);
                }
                else
                {
                    step.transform.position = new Vector3(0f, y, offset);
                    step.transform.localScale = new Vector3(length, stepHeight + i * stepHeight, stepDepth);
                }

                // a sparse, colourful crowd standing on the top three rows
                if (i >= tiers - 3)
                    PopulateCrowd(stand.transform, axis, offset, (i + 1) * stepHeight, length);
            }
        }

        static void PopulateCrowd(Transform parent, Axis axis, float offset, float topY, float length)
        {
            const float spacing = 1.5f;
            int count = Mathf.FloorToInt(length / spacing);
            float start = -length * 0.5f + spacing * 0.5f;

            for (int j = 0; j < count; j++)
            {
                float along = start + j * spacing + Random.Range(-0.2f, 0.2f);
                var fan = GameObject.CreatePrimitive(PrimitiveType.Cube);
                StripCollider(fan);
                fan.name = "Fan";
                fan.transform.SetParent(parent, true);
                fan.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
                float y = topY + 0.45f;
                fan.transform.position = axis == Axis.AlongZ
                    ? new Vector3(offset, y, along)
                    : new Vector3(along, y, offset);
                fan.GetComponent<MeshRenderer>().sharedMaterial = CrowdMaterial(j);
            }
        }

        // ----------------------------------------------------------------- net posts

        static void BuildNetPosts(Transform root)
        {
            Material postMat = MakeLit("NetPost", new Color(0.20f, 0.22f, 0.26f), smoothness: 0.4f, metallic: 0.6f);
            float h = CourtGeometry.NetHeight + 0.25f;
            float x = CourtGeometry.HalfWidth + 0.55f;
            foreach (float sx in new[] { -x, x })
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                StripCollider(post);
                post.name = "Net Post";
                post.transform.SetParent(root, true);
                post.transform.position = new Vector3(sx, h * 0.5f, 0f);
                post.transform.localScale = new Vector3(0.16f, h * 0.5f, 0.16f); // cylinder height = 2*scaleY
                post.GetComponent<MeshRenderer>().sharedMaterial = postMat;
            }
        }

        // ----------------------------------------------------------------- palm trees

        static void BuildPalms(Transform root)
        {
            Vector3[] spots =
            {
                new Vector3(-12f, 0f, -14f), new Vector3(12f, 0f, -15f),
                new Vector3(-14f, 0f, 12f),  new Vector3(13f, 0f, 14f),
                new Vector3(-9f, 0f, 17f),
            };
            Material trunk = MakeLit("PalmTrunk", new Color(0.45f, 0.32f, 0.20f));
            Material frond = MakeLit("PalmFrond", new Color(0.20f, 0.50f, 0.22f));

            for (int i = 0; i < spots.Length; i++)
                BuildPalm(root, spots[i], trunk, frond, 3f + (i % 3) * 0.8f);
        }

        static void BuildPalm(Transform root, Vector3 basePos, Material trunkMat, Material frondMat, float height)
        {
            var palm = new GameObject("Palm");
            palm.transform.SetParent(root, true);
            palm.transform.position = basePos;

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            StripCollider(trunk);
            trunk.name = "Trunk";
            trunk.transform.SetParent(palm.transform, false);
            trunk.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            trunk.transform.localScale = new Vector3(0.22f, height * 0.5f, 0.22f);
            trunk.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-6f, 6f));
            trunk.GetComponent<MeshRenderer>().sharedMaterial = trunkMat;

            // a crown of flattened, drooping fronds
            for (int f = 0; f < 6; f++)
            {
                var leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                StripCollider(leaf);
                leaf.name = "Frond";
                leaf.transform.SetParent(palm.transform, false);
                leaf.transform.localPosition = new Vector3(0f, height, 0f);
                float yaw = f * 60f + Random.Range(-10f, 10f);
                leaf.transform.localRotation = Quaternion.Euler(28f, yaw, 0f);
                leaf.transform.localScale = new Vector3(0.6f, 0.14f, 2.4f);
                leaf.transform.localPosition += leaf.transform.forward * 0.9f;
                leaf.GetComponent<MeshRenderer>().sharedMaterial = frondMat;
            }
        }

        // ----------------------------------------------------------------- tiki torches

        static void BuildTorches(Transform root)
        {
            Vector3[] spots =
            {
                new Vector3(-6f, 0f, -9f), new Vector3(6f, 0f, -9f),
                new Vector3(-6f, 0f, 9f),  new Vector3(6f, 0f, 9f),
            };
            Material pole = MakeLit("TorchPole", new Color(0.35f, 0.24f, 0.16f));
            Material flameMat = MakeLit("TorchFlame", new Color(1f, 0.55f, 0.15f),
                                        emission: new Color(1f, 0.5f, 0.12f) * 3f);

            foreach (var p in spots)
            {
                var torch = new GameObject("Tiki Torch");
                torch.transform.SetParent(root, true);
                torch.transform.position = p;

                var stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                StripCollider(stick);
                stick.name = "Pole";
                stick.transform.SetParent(torch.transform, false);
                stick.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                stick.transform.localScale = new Vector3(0.09f, 1.1f, 0.09f);
                stick.GetComponent<MeshRenderer>().sharedMaterial = pole;

                var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                StripCollider(flame);
                flame.name = "Flame";
                flame.transform.SetParent(torch.transform, false);
                flame.transform.localPosition = new Vector3(0f, 2.35f, 0f);
                flame.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
                flame.GetComponent<MeshRenderer>().sharedMaterial = flameMat;

                var lightGO = new GameObject("Torch Light");
                lightGO.transform.SetParent(torch.transform, false);
                lightGO.transform.localPosition = new Vector3(0f, 2.4f, 0f);
                var lt = lightGO.AddComponent<Light>();
                lt.type = LightType.Point;
                lt.color = new Color(1f, 0.6f, 0.25f);
                lt.range = 10f;
                lt.intensity = 2.2f;
            }
        }

        // ----------------------------------------------------------------- umbrellas

        static void BuildUmbrellas(Transform root)
        {
            (Vector3 pos, Color col)[] spots =
            {
                (new Vector3(-8f, 0f, -5f), new Color(0.90f, 0.30f, 0.28f)),
                (new Vector3(-9f, 0f, 4f),  new Color(0.95f, 0.80f, 0.25f)),
                (new Vector3(9f, 0f, -6f),  new Color(0.30f, 0.55f, 0.85f)),
            };
            Material pole = MakeLit("UmbrellaPole", new Color(0.75f, 0.72f, 0.68f));

            for (int i = 0; i < spots.Length; i++)
            {
                var (pos, col) = spots[i];
                var umb = new GameObject("Beach Umbrella");
                umb.transform.SetParent(root, true);
                umb.transform.position = pos;
                umb.transform.localRotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), 0f);

                var stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                StripCollider(stick);
                stick.name = "Pole";
                stick.transform.SetParent(umb.transform, false);
                stick.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                stick.transform.localScale = new Vector3(0.06f, 1.2f, 0.06f);
                stick.GetComponent<MeshRenderer>().sharedMaterial = pole;

                var canopy = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                StripCollider(canopy);
                canopy.name = "Canopy";
                canopy.transform.SetParent(umb.transform, false);
                canopy.transform.localPosition = new Vector3(0f, 2.45f, 0f);
                canopy.transform.localScale = new Vector3(3.2f, 0.08f, 3.2f); // wide flat disc
                canopy.GetComponent<MeshRenderer>().sharedMaterial = MakeLit($"Umbrella{i}", col);
            }
        }

        // ----------------------------------------------------------------- clouds

        static void BuildClouds(Transform root)
        {
            Material cloud = MakeLit("Cloud", new Color(0.98f, 0.88f, 0.84f), smoothness: 0f);
            Vector3[] spots =
            {
                new Vector3(-30f, 24f, 20f), new Vector3(25f, 28f, 35f),
                new Vector3(-10f, 30f, 45f), new Vector3(40f, 22f, -10f),
                new Vector3(0f, 34f, 60f),   new Vector3(-45f, 26f, -20f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                var puff = new GameObject("Cloud");
                puff.transform.SetParent(root, true);
                puff.transform.position = spots[i];
                int blobs = Random.Range(3, 6);
                for (int b = 0; b < blobs; b++)
                {
                    var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    StripCollider(s);
                    s.name = "Puff";
                    s.transform.SetParent(puff.transform, false);
                    s.transform.localPosition = new Vector3(Random.Range(-5f, 5f), Random.Range(-1f, 1f), Random.Range(-3f, 3f));
                    float r = Random.Range(4f, 8f);
                    s.transform.localScale = new Vector3(r, r * 0.55f, r);
                    s.GetComponent<MeshRenderer>().sharedMaterial = cloud;
                }
            }
        }

        // ----------------------------------------------------------------- helpers

        static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }

        static GameObject GetOrCreateRoot(string name)
        {
            var existing = GameObject.Find(name);
            return existing != null ? existing : new GameObject(name);
        }

        static Material CrowdMaterial(int index)
        {
            Color c = CrowdColors[index % CrowdColors.Length];
            return MakeLit($"Crowd{index % CrowdColors.Length}", c);
        }

        static Material MakeLit(string name, Color color, float smoothness = 0.1f,
                                float metallic = 0f, Color? emission = null)
        {
            string path = $"{MatDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = name };

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                mat.SetColor("_EmissionColor", emission.Value);
            }

            EnsureDir(MatDir);
            AssetDatabase.CreateAsset(mat, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        static Material MakeOrLoadMaterial(string name, Shader shader)
        {
            string path = $"{MatDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            EnsureDir(MatDir);
            var mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        static void EnsureDir(string assetDir)
        {
            if (AssetDatabase.IsValidFolder(assetDir)) return;

            string parent = Path.GetDirectoryName(assetDir).Replace('\\', '/');
            string leaf = Path.GetFileName(assetDir);
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureDir(parent); // recurse so "Assets/Materials" exists before "Assets/Materials/Arena"
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
