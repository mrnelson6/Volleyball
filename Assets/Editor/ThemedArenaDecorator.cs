using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// A data-driven sibling to <see cref="ArenaDecorator"/>. Where the beach decorator hard-codes a
    /// single golden-hour scene, this one describes an arena as a <see cref="ArenaTheme"/> (sky, sun,
    /// fog, surrounding floor, grandstands and a signature-prop callback) and builds it generically,
    /// so a whole roster of "outlandish places to play volleyball" can share one well-tested core.
    ///
    /// Like the beach decorator, every mesh sits clear of the origin-centred play volume, so
    /// gameplay and scoring are untouched (the GroundMarker plane still decides every landing) —
    /// but the dressing is solid, so balls bounce off it instead of through it. <see
    /// cref="DecorColliders"/> applies that at the end of the build under the same rules for
    /// every theme. Everything is parented under a single "<i>Theme</i> Decor" root so a designer
    /// can toggle or delete the dressing in one click.
    /// </summary>
    public static class ThemedArenaDecorator
    {
        const string MatDir = "Assets/Materials/Arena";

        // ----------------------------------------------------------------- theme description

        /// <summary>Everything the generic builder needs to dress (and name) one arena.</summary>
        public class ArenaTheme
        {
            public string key;            // scene/material prefix, e.g. "VolcanoArena"
            public string displayName;    // human label, e.g. "Volcano Rim"
            public string blurb;          // one-line pitch shown in the level designer window

            // sky — a tinted procedural skybox, or a flat colour for "no sky" themes (space/underwater)
            public bool proceduralSky = true;
            public Color skyTint = Color.white;
            public Color skyGround = new Color(0.4f, 0.4f, 0.4f);
            public float atmosphere = 1.0f;
            public float exposure = 1.2f;
            public float sunSize = 0.05f;
            public Color flatBackground = Color.black; // used when proceduralSky is false

            // sun
            public Color sunColor = Color.white;
            public float sunIntensity = 1.1f;
            public Vector3 sunEuler = new Vector3(50f, -30f, 0f);
            public LightShadows shadows = LightShadows.Soft;

            // ambient + fog
            public Color ambient = new Color(0.5f, 0.5f, 0.5f);
            public bool fog = true;
            public Color fogColor = new Color(0.7f, 0.7f, 0.7f);
            public float fogDensity = 0.006f;

            // the large surrounding "floor" plane that runs to the horizon (beach's ocean analog)
            public Color floorColor = new Color(0.3f, 0.3f, 0.3f);
            public float floorSmoothness = 0.2f;
            public float floorMetallic = 0f;
            public Color? floorEmission = null;

            // grandstands + crowd
            public bool stands = true;
            public Color standColor = new Color(0.62f, 0.60f, 0.58f);
            public Color[] crowdColors;

            // net posts
            public Color netPostColor = new Color(0.20f, 0.22f, 0.26f);

            // signature props unique to the theme
            public System.Action<Transform, ArenaTheme> props;
        }

        public static string DecorRootName(ArenaTheme t) => $"{t.displayName} Decor";

        // ----------------------------------------------------------------- the roster

        static ArenaTheme[] _themes;

        /// <summary>
        /// Every buildable arena: the six fantasy venues below plus the world-tour regional
        /// courts from <see cref="RegionalArenaThemes"/>. Lazy so prop delegates resolve cleanly.
        /// </summary>
        public static ArenaTheme[] Themes
        {
            get
            {
                if (_themes != null) return _themes;
                ArenaTheme[] fantasy = FantasyThemes();
                ArenaTheme[] regional = RegionalArenaThemes.All();
                _themes = new ArenaTheme[fantasy.Length + regional.Length];
                fantasy.CopyTo(_themes, 0);
                regional.CopyTo(_themes, fantasy.Length);
                return _themes;
            }
        }

        /// <summary>The six original fantasy arenas.</summary>
        static ArenaTheme[] FantasyThemes() => new[]
        {
            new ArenaTheme
            {
                key = "VolcanoArena", displayName = "Volcano Rim",
                blurb = "A court perched on the lip of an active volcano, ringed by a sea of lava.",
                proceduralSky = false, flatBackground = new Color(0.10f, 0.05f, 0.06f),
                sunColor = new Color(1f, 0.55f, 0.35f), sunIntensity = 0.7f,
                sunEuler = new Vector3(18f, 40f, 0f), shadows = LightShadows.Soft,
                ambient = new Color(0.32f, 0.16f, 0.12f),
                fog = true, fogColor = new Color(0.30f, 0.12f, 0.08f), fogDensity = 0.012f,
                floorColor = new Color(1f, 0.32f, 0.06f), floorSmoothness = 0.55f,
                floorEmission = new Color(1f, 0.30f, 0.04f) * 1.6f,
                standColor = new Color(0.18f, 0.16f, 0.16f),
                crowdColors = new[]
                {
                    new Color(0.85f, 0.30f, 0.18f), new Color(0.95f, 0.55f, 0.20f),
                    new Color(0.55f, 0.18f, 0.14f), new Color(0.20f, 0.18f, 0.18f),
                },
                netPostColor = new Color(0.12f, 0.10f, 0.10f),
                props = BuildVolcanoProps,
            },
            new ArenaTheme
            {
                key = "LunarArena", displayName = "Lunar Base",
                blurb = "Low-gravity volleyball in the regolith, Earth hanging over the grandstand.",
                proceduralSky = false, flatBackground = new Color(0.01f, 0.01f, 0.03f),
                sunColor = new Color(1f, 0.98f, 0.95f), sunIntensity = 1.5f,
                sunEuler = new Vector3(28f, -20f, 0f), shadows = LightShadows.Hard,
                ambient = new Color(0.06f, 0.07f, 0.10f),
                fog = false,
                floorColor = new Color(0.55f, 0.55f, 0.58f), floorSmoothness = 0.05f,
                standColor = new Color(0.40f, 0.42f, 0.46f),
                crowdColors = new[]
                {
                    new Color(0.85f, 0.88f, 0.92f), new Color(0.55f, 0.85f, 0.65f),
                    new Color(0.60f, 0.62f, 0.70f), new Color(0.90f, 0.65f, 0.30f),
                },
                netPostColor = new Color(0.70f, 0.72f, 0.78f),
                props = BuildLunarProps,
            },
            new ArenaTheme
            {
                key = "AtlantisArena", displayName = "Atlantis Deep",
                blurb = "A sunken court on the ocean floor, lit by drifting caustics and kelp.",
                proceduralSky = false, flatBackground = new Color(0.02f, 0.10f, 0.16f),
                sunColor = new Color(0.55f, 0.85f, 0.95f), sunIntensity = 1.0f,
                sunEuler = new Vector3(70f, 10f, 0f), shadows = LightShadows.Soft,
                ambient = new Color(0.08f, 0.22f, 0.28f),
                fog = true, fogColor = new Color(0.04f, 0.22f, 0.30f), fogDensity = 0.035f,
                floorColor = new Color(0.18f, 0.30f, 0.30f), floorSmoothness = 0.2f,
                standColor = new Color(0.20f, 0.34f, 0.36f),
                crowdColors = new[]
                {
                    new Color(0.95f, 0.55f, 0.25f), new Color(0.95f, 0.85f, 0.30f),
                    new Color(0.30f, 0.70f, 0.85f), new Color(0.85f, 0.40f, 0.55f),
                },
                netPostColor = new Color(0.30f, 0.45f, 0.45f),
                props = BuildAtlantisProps,
            },
            new ArenaTheme
            {
                key = "SkyArena", displayName = "Cloud Kingdom",
                blurb = "A court floating among the clouds, with balloons, birds and a rainbow.",
                proceduralSky = true, skyTint = new Color(0.55f, 0.72f, 0.95f),
                skyGround = new Color(0.85f, 0.90f, 0.96f), atmosphere = 0.7f, exposure = 1.45f,
                sunColor = new Color(1f, 0.97f, 0.88f), sunIntensity = 1.25f,
                sunEuler = new Vector3(45f, -25f, 0f),
                ambient = new Color(0.62f, 0.66f, 0.74f),
                fog = true, fogColor = new Color(0.85f, 0.90f, 0.97f), fogDensity = 0.004f,
                floorColor = new Color(0.96f, 0.97f, 1f), floorSmoothness = 0f,
                standColor = new Color(0.92f, 0.94f, 0.98f),
                crowdColors = new[]
                {
                    new Color(0.95f, 0.55f, 0.65f), new Color(0.55f, 0.75f, 0.95f),
                    new Color(0.95f, 0.85f, 0.45f), new Color(0.65f, 0.90f, 0.70f),
                },
                netPostColor = new Color(0.80f, 0.82f, 0.88f),
                props = BuildSkyProps,
            },
            new ArenaTheme
            {
                key = "GraveyardArena", displayName = "Haunted Graveyard",
                blurb = "A midnight match among the tombstones, lit by jack-o'-lanterns and a full moon.",
                proceduralSky = false, flatBackground = new Color(0.03f, 0.04f, 0.07f),
                sunColor = new Color(0.55f, 0.62f, 0.85f), sunIntensity = 0.45f,
                sunEuler = new Vector3(35f, 60f, 0f), shadows = LightShadows.Soft,
                ambient = new Color(0.10f, 0.12f, 0.18f),
                fog = true, fogColor = new Color(0.10f, 0.13f, 0.16f), fogDensity = 0.02f,
                floorColor = new Color(0.14f, 0.16f, 0.14f), floorSmoothness = 0.1f,
                standColor = new Color(0.22f, 0.22f, 0.26f),
                crowdColors = new[]
                {
                    new Color(0.80f, 0.82f, 0.85f), new Color(0.55f, 0.70f, 0.55f),
                    new Color(0.50f, 0.52f, 0.58f), new Color(0.70f, 0.65f, 0.45f),
                },
                netPostColor = new Color(0.18f, 0.18f, 0.20f),
                props = BuildGraveyardProps,
            },
            new ArenaTheme
            {
                key = "NeonArena", displayName = "Neon Rooftop",
                blurb = "A rain-slick rooftop court in a neon megacity, searchlights raking the sky.",
                proceduralSky = false, flatBackground = new Color(0.04f, 0.03f, 0.10f),
                sunColor = new Color(0.45f, 0.50f, 0.85f), sunIntensity = 0.4f,
                sunEuler = new Vector3(30f, 200f, 0f), shadows = LightShadows.Soft,
                ambient = new Color(0.12f, 0.10f, 0.20f),
                fog = true, fogColor = new Color(0.10f, 0.06f, 0.18f), fogDensity = 0.01f,
                floorColor = new Color(0.06f, 0.06f, 0.10f), floorSmoothness = 0.92f, floorMetallic = 0.4f,
                standColor = new Color(0.10f, 0.10f, 0.16f),
                crowdColors = new[]
                {
                    new Color(0.95f, 0.20f, 0.65f), new Color(0.20f, 0.90f, 0.95f),
                    new Color(0.70f, 0.30f, 0.95f), new Color(0.95f, 0.85f, 0.20f),
                },
                netPostColor = new Color(0.16f, 0.16f, 0.22f),
                props = BuildNeonProps,
            },
        };

        public static ArenaTheme Find(string key)
        {
            foreach (var t in Themes) if (t.key == key) return t;
            return null;
        }

        // ----------------------------------------------------------------- one-call entry

        /// <summary>Build the full environment, camera, stands, net posts and signature props.</summary>
        public static void BuildArena(ArenaTheme theme)
        {
            var root = GetOrCreateRoot(DecorRootName(theme)).transform;

            Light sun = ConfigureEnvironmentAndSun(root, theme);
            BuildShowcaseCamera(theme);

            BuildFloor(root, theme);
            if (theme.stands)
            {
                BuildGrandstand(root, theme, Axis.AlongZ, -1f); // sideline stand behind -X
                BuildGrandstand(root, theme, Axis.AlongX, 1f);  // end stand behind +Z
            }
            BuildNetPosts(root, theme);

            theme.props?.Invoke(root, theme);

            // solid dressing bounces the ball back; the play volume, the horizon floor sheet and
            // anything out of reach stay pass-through — see DecorColliders
            int solids = DecorColliders.ApplyTo(root);

            if (sun != null) RenderSettings.sun = sun;
            Debug.Log($"[Volleyball] {theme.displayName} dressed; {solids} props made solid.");
        }

        // ----------------------------------------------------------------- environment + sun

        static Light ConfigureEnvironmentAndSun(Transform root, ArenaTheme t)
        {
            if (t.proceduralSky)
            {
                Shader skyShader = Shader.Find("Skybox/Procedural");
                if (skyShader != null)
                {
                    Material sky = MakeOrLoadMaterial($"{t.key}_Sky", skyShader);
                    sky.SetFloat("_SunSize", t.sunSize);
                    sky.SetFloat("_SunSizeConvergence", 3f);
                    sky.SetFloat("_AtmosphereThickness", t.atmosphere);
                    sky.SetColor("_SkyTint", t.skyTint);
                    sky.SetColor("_GroundColor", t.skyGround);
                    sky.SetFloat("_Exposure", t.exposure);
                    RenderSettings.skybox = sky;
                }
            }
            else
            {
                RenderSettings.skybox = null; // camera clears to flatBackground instead
            }

            var go = new GameObject("Sun (Directional Light)");
            go.transform.SetParent(root, true);
            var sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = t.sunColor;
            sun.intensity = t.sunIntensity;
            sun.shadows = t.shadows;
            sun.shadowStrength = 0.6f;
            go.transform.rotation = Quaternion.Euler(t.sunEuler);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = t.ambient;
            RenderSettings.fog = t.fog;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = t.fogColor;
            RenderSettings.fogDensity = t.fogDensity;
            DynamicGI.UpdateEnvironment();

            return sun;
        }

        static void BuildShowcaseCamera(ArenaTheme t)
        {
            if (Camera.main != null || GameObject.FindGameObjectWithTag("MainCamera") != null)
                return;

            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            if (t.proceduralSky)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
            }
            else
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = t.flatBackground;
            }
            cam.fieldOfView = 36f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 400f;
            go.AddComponent<AudioListener>();
            go.transform.position = new Vector3(20f, 12f, -3f);
            go.transform.LookAt(new Vector3(0f, 1.6f, 0f));
        }

        // ----------------------------------------------------------------- surrounding floor

        static void BuildFloor(Transform root, ArenaTheme t)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            StripCollider(floor);
            floor.name = "Surrounding Floor";
            floor.transform.SetParent(root, true);
            floor.transform.position = new Vector3(0f, -0.08f, 0f); // just below the playable court
            floor.transform.localScale = new Vector3(60f, 1f, 60f);  // ~600 x 600, runs to horizon
            floor.GetComponent<MeshRenderer>().sharedMaterial =
                Mat(t, "Floor", t.floorColor, t.floorSmoothness, t.floorMetallic, t.floorEmission);
        }

        // ----------------------------------------------------------------- grandstands (shared)

        enum Axis { AlongZ, AlongX }

        static void BuildGrandstand(Transform root, ArenaTheme t, Axis axis, float side)
        {
            var stand = new GameObject(axis == Axis.AlongZ ? "Grandstand (Sideline)" : "Grandstand (End)");
            stand.transform.SetParent(root, true);

            const int tiers = 7;
            const float stepDepth = 1.4f;
            const float stepHeight = 0.8f;
            float length = (axis == Axis.AlongZ ? CourtGeometry.HalfDepth : CourtGeometry.HalfWidth) * 2f + 8f;
            float startOffset = (axis == Axis.AlongZ ? CourtGeometry.HalfWidth : CourtGeometry.HalfDepth) + 3f;

            Material concrete = Mat(t, "Stand", t.standColor);

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

                if (i >= tiers - 3)
                    PopulateCrowd(stand.transform, t, axis, offset, (i + 1) * stepHeight, length);
            }
        }

        static void PopulateCrowd(Transform parent, ArenaTheme t, Axis axis, float offset, float topY, float length)
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
                Color c = t.crowdColors[j % t.crowdColors.Length];
                fan.GetComponent<MeshRenderer>().sharedMaterial = Mat(t, $"Crowd{j % t.crowdColors.Length}", c);
            }
        }

        // ----------------------------------------------------------------- net posts (shared)

        static void BuildNetPosts(Transform root, ArenaTheme t)
        {
            Material postMat = Mat(t, "NetPost", t.netPostColor, smoothness: 0.4f, metallic: 0.6f);
            float h = CourtGeometry.NetHeight + 0.25f;
            float x = CourtGeometry.HalfWidth + 0.55f;
            foreach (float sx in new[] { -x, x })
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                StripCollider(post);
                post.name = "Net Post";
                post.transform.SetParent(root, true);
                post.transform.position = new Vector3(sx, h * 0.5f, 0f);
                post.transform.localScale = new Vector3(0.16f, h * 0.5f, 0.16f);
                post.GetComponent<MeshRenderer>().sharedMaterial = postMat;
            }
        }

        // ================================================================= VOLCANO

        static void BuildVolcanoProps(Transform root, ArenaTheme t)
        {
            Material rock = Mat(t, "Rock", new Color(0.16f, 0.13f, 0.13f));
            Material crater = Mat(t, "Crater", new Color(1f, 0.45f, 0.10f), 0.4f, 0f,
                                  new Color(1f, 0.35f, 0.05f) * 4f);
            Material ember = Mat(t, "Ember", new Color(1f, 0.6f, 0.2f), 0.2f, 0f,
                                 new Color(1f, 0.5f, 0.12f) * 3f);

            Vector3[] cones =
            {
                new Vector3(-17f, 0f, -15f), new Vector3(16f, 0f, -16f),
                new Vector3(-16f, 0f, 14f),  new Vector3(15f, 0f, 16f),
            };
            for (int i = 0; i < cones.Length; i++)
            {
                var vol = new GameObject("Volcano");
                vol.transform.SetParent(root, true);
                vol.transform.position = cones[i];
                float h = 6f + (i % 2) * 3f;

                // a 3-segment stepped cone of dark basalt
                for (int s = 0; s < 3; s++)
                {
                    float r = Mathf.Lerp(5f, 1.2f, s / 2f);
                    float segH = h / 3f;
                    Spawn(PrimitiveType.Cylinder, vol.transform, "Slope",
                        cones[i] + new Vector3(0f, segH * (s + 0.5f), 0f),
                        new Vector3(r, segH * 0.5f, r), rock);
                }
                // glowing crater + ember light at the summit
                Spawn(PrimitiveType.Sphere, vol.transform, "Crater",
                    cones[i] + new Vector3(0f, h, 0f), new Vector3(2.2f, 1.1f, 2.2f), crater);
                AddPoint(vol.transform, cones[i] + new Vector3(0f, h + 1.5f, 0f),
                    new Color(1f, 0.5f, 0.15f), 22f, 4f);

                // a couple of drifting embers
                for (int e = 0; e < 3; e++)
                    Spawn(PrimitiveType.Sphere, vol.transform, "Ember",
                        cones[i] + new Vector3(Random.Range(-2f, 2f), h + Random.Range(2f, 5f), Random.Range(-2f, 2f)),
                        Vector3.one * 0.3f, ember);
            }

            // smouldering rocks scattered just outside the court
            Material smoulder = Mat(t, "Smoulder", new Color(0.25f, 0.10f, 0.06f), 0.3f, 0f,
                                    new Color(0.9f, 0.25f, 0.05f) * 1.2f);
            for (int i = 0; i < 14; i++)
            {
                float ang = i / 14f * Mathf.PI * 2f;
                float rad = Random.Range(8f, 11f);
                var p = new Vector3(Mathf.Cos(ang) * rad, 0.2f, Mathf.Sin(ang) * rad);
                Spawn(PrimitiveType.Cube, root, "Lava Rock", p,
                    Vector3.one * Random.Range(0.4f, 1.1f), smoulder,
                    new Vector3(Random.Range(0f, 40f), Random.Range(0f, 360f), Random.Range(0f, 40f)));
            }
        }

        // ================================================================= LUNAR

        static void BuildLunarProps(Transform root, ArenaTheme t)
        {
            // Earthrise over the far stand
            Material earth = Mat(t, "Earth", new Color(0.20f, 0.45f, 0.80f), 0.1f, 0f,
                                 new Color(0.18f, 0.40f, 0.75f) * 1.6f);
            Spawn(PrimitiveType.Sphere, root, "Earth", new Vector3(-38f, 34f, 58f),
                Vector3.one * 22f, earth);

            // a scattered starfield on a far dome
            Material star = Mat(t, "Star", Color.white, 0f, 0f, Color.white * 2f);
            var stars = new GameObject("Stars");
            stars.transform.SetParent(root, true);
            for (int i = 0; i < 120; i++)
            {
                var dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.9f + 0.1f; // keep them above the horizon
                Spawn(PrimitiveType.Sphere, stars.transform, "Star",
                    dir.normalized * Random.Range(160f, 190f), Vector3.one * Random.Range(0.4f, 1.0f), star);
            }

            // shallow craters ringing the court (a darker disc with a raised rim)
            Material regolith = Mat(t, "Regolith", new Color(0.46f, 0.46f, 0.50f));
            Material rim = Mat(t, "Rim", new Color(0.62f, 0.62f, 0.66f));
            for (int i = 0; i < 7; i++)
            {
                float ang = i / 7f * Mathf.PI * 2f + 0.3f;
                float rad = Random.Range(9f, 14f);
                var c = new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                float cr = Random.Range(2f, 4f);
                Spawn(PrimitiveType.Cylinder, root, "Crater Rim", c + Vector3.up * 0.05f,
                    new Vector3(cr + 0.6f, 0.12f, cr + 0.6f), rim);
                Spawn(PrimitiveType.Cylinder, root, "Crater Floor", c + Vector3.up * 0.08f,
                    new Vector3(cr, 0.10f, cr), regolith);
            }

            // a little lander and a flag for flavour
            Material metal = Mat(t, "Lander", new Color(0.78f, 0.74f, 0.40f), 0.6f, 0.7f);
            var lander = new GameObject("Lander");
            lander.transform.SetParent(root, true);
            Vector3 lp = new Vector3(13f, 0f, -12f);
            Spawn(PrimitiveType.Cube, lander.transform, "Body", lp + new Vector3(0f, 1.6f, 0f),
                new Vector3(2.4f, 1.4f, 2.4f), metal);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f * Mathf.Deg2Rad;
                var foot = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.6f;
                Spawn(PrimitiveType.Cylinder, lander.transform, "Leg", lp + foot + Vector3.up * 0.7f,
                    new Vector3(0.12f, 0.8f, 0.12f), metal,
                    new Vector3(20f, i * 90f, 0f));
            }
            Spawn(PrimitiveType.Cylinder, lander.transform, "Antenna", lp + new Vector3(0f, 3f, 0f),
                new Vector3(0.06f, 1f, 0.06f), metal);

            Material flagPole = Mat(t, "FlagPole", new Color(0.8f, 0.8f, 0.85f), 0.6f, 0.6f);
            Material flag = Mat(t, "Flag", new Color(0.85f, 0.20f, 0.20f), 0.1f, 0f,
                                new Color(0.7f, 0.15f, 0.15f) * 0.6f);
            Vector3 fp = new Vector3(-11f, 0f, -10f);
            Spawn(PrimitiveType.Cylinder, root, "Flag Pole", fp + new Vector3(0f, 1.5f, 0f),
                new Vector3(0.05f, 1.5f, 0.05f), flagPole);
            Spawn(PrimitiveType.Cube, root, "Flag", fp + new Vector3(0.7f, 2.6f, 0f),
                new Vector3(1.3f, 0.8f, 0.05f), flag);
        }

        // ================================================================= ATLANTIS

        static void BuildAtlantisProps(Transform root, ArenaTheme t)
        {
            // a cool caustic fill from above
            AddPoint(root, new Vector3(0f, 18f, 0f), new Color(0.4f, 0.8f, 0.95f), 60f, 2.5f);

            Material kelp = Mat(t, "Kelp", new Color(0.18f, 0.45f, 0.25f));
            for (int i = 0; i < 12; i++)
            {
                float ang = i / 12f * Mathf.PI * 2f;
                float rad = Random.Range(8f, 13f);
                var basePos = new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                var strand = new GameObject("Kelp");
                strand.transform.SetParent(root, true);
                Vector3 cur = basePos;
                float lean = Random.Range(8f, 18f);
                for (int s = 0; s < 6; s++)
                {
                    var seg = Spawn(PrimitiveType.Cylinder, strand.transform, "Blade",
                        cur + new Vector3(0f, 0.7f, 0f), new Vector3(0.18f, 0.7f, 0.18f), kelp,
                        new Vector3(Mathf.Sin(s * 1.1f) * lean, 0f, Mathf.Cos(s * 0.9f) * lean));
                    cur = seg.transform.position + new Vector3(0f, 0.7f, 0f);
                }
            }

            // coral clusters — stacked stretched spheres in warm reef colours
            Color[] coralCols =
            {
                new Color(0.95f, 0.45f, 0.55f), new Color(0.95f, 0.65f, 0.25f),
                new Color(0.65f, 0.40f, 0.85f),
            };
            for (int i = 0; i < 7; i++)
            {
                float ang = i / 7f * Mathf.PI * 2f + 0.5f;
                float rad = Random.Range(7f, 12f);
                var c = new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                Material cm = Mat(t, $"Coral{i % coralCols.Length}", coralCols[i % coralCols.Length]);
                var coral = new GameObject("Coral");
                coral.transform.SetParent(root, true);
                int arms = Random.Range(3, 6);
                for (int a = 0; a < arms; a++)
                {
                    float ya = a / (float)arms * 360f;
                    var dir = Quaternion.Euler(0f, ya, 0f) * Vector3.forward;
                    Spawn(PrimitiveType.Capsule, coral.transform, "Branch",
                        c + dir * 0.5f + Vector3.up * Random.Range(0.8f, 1.6f),
                        new Vector3(0.4f, Random.Range(0.9f, 1.6f), 0.4f), cm,
                        new Vector3(Random.Range(20f, 45f), ya, 0f));
                }
            }

            // rising bubble columns
            Material bubble = Mat(t, "Bubble", new Color(0.8f, 0.95f, 1f, 1f), 0.9f, 0f,
                                  new Color(0.6f, 0.85f, 0.95f) * 0.5f);
            for (int i = 0; i < 5; i++)
            {
                float ang = i / 5f * Mathf.PI * 2f + 1.1f;
                float rad = Random.Range(6f, 10f);
                var basePos = new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                var col = new GameObject("Bubbles");
                col.transform.SetParent(root, true);
                for (int b = 0; b < 8; b++)
                    Spawn(PrimitiveType.Sphere, col.transform, "Bubble",
                        basePos + new Vector3(Random.Range(-0.4f, 0.4f), b * 1.4f + Random.Range(0f, 0.5f), Random.Range(-0.4f, 0.4f)),
                        Vector3.one * Random.Range(0.2f, 0.5f), bubble);
            }

            // toppled sunken columns
            Material stone = Mat(t, "Ruin", new Color(0.55f, 0.62f, 0.58f));
            Vector3[] ruins = { new Vector3(-13f, 0f, 4f), new Vector3(12f, 0f, -5f), new Vector3(-9f, 0f, -13f) };
            for (int i = 0; i < ruins.Length; i++)
            {
                bool fallen = i % 2 == 1;
                Spawn(PrimitiveType.Cylinder, root, "Column",
                    ruins[i] + new Vector3(0f, fallen ? 0.6f : 2.2f, 0f),
                    new Vector3(0.7f, 2.2f, 0.7f), stone,
                    fallen ? new Vector3(90f, Random.Range(0f, 360f), 0f) : Vector3.zero);
            }
        }

        // ================================================================= SKY

        static void BuildSkyProps(Transform root, ArenaTheme t)
        {
            // puffy cloud islands the court appears to rest among
            Material cloud = Mat(t, "Cloud", new Color(0.99f, 0.99f, 1f), 0f);
            Vector3[] islands =
            {
                new Vector3(-22f, -3f, -10f), new Vector3(20f, -4f, 12f),
                new Vector3(0f, -5f, 24f), new Vector3(-18f, -2f, 16f),
                new Vector3(24f, -3f, -14f),
            };
            foreach (var pos in islands)
            {
                var island = new GameObject("Cloud Island");
                island.transform.SetParent(root, true);
                int blobs = Random.Range(4, 7);
                for (int b = 0; b < blobs; b++)
                {
                    float r = Random.Range(4f, 8f);
                    Spawn(PrimitiveType.Sphere, island.transform, "Puff",
                        pos + new Vector3(Random.Range(-5f, 5f), Random.Range(-1f, 1f), Random.Range(-4f, 4f)),
                        new Vector3(r, r * 0.6f, r), cloud);
                }
            }

            // hot-air balloons drifting at height
            Color[] balloonCols =
            {
                new Color(0.95f, 0.35f, 0.40f), new Color(0.35f, 0.65f, 0.95f),
                new Color(0.95f, 0.80f, 0.30f), new Color(0.55f, 0.85f, 0.55f),
            };
            Material rope = Mat(t, "Rope", new Color(0.5f, 0.4f, 0.3f));
            Material basket = Mat(t, "Basket", new Color(0.55f, 0.40f, 0.25f));
            Vector3[] balloonAt =
            {
                new Vector3(-16f, 12f, 8f), new Vector3(15f, 16f, -6f),
                new Vector3(6f, 20f, 18f), new Vector3(-10f, 14f, -16f),
            };
            for (int i = 0; i < balloonAt.Length; i++)
            {
                var b = new GameObject("Balloon");
                b.transform.SetParent(root, true);
                var p = balloonAt[i];
                Material bm = Mat(t, $"Balloon{i % balloonCols.Length}", balloonCols[i % balloonCols.Length],
                                  0.2f, 0f, balloonCols[i % balloonCols.Length] * 0.4f);
                Spawn(PrimitiveType.Sphere, b.transform, "Envelope", p, new Vector3(3.4f, 4.2f, 3.4f), bm);
                Spawn(PrimitiveType.Cube, b.transform, "Basket", p + new Vector3(0f, -3.4f, 0f),
                    new Vector3(0.8f, 0.8f, 0.8f), basket);
                Spawn(PrimitiveType.Cylinder, b.transform, "Rope", p + new Vector3(0f, -2.4f, 0f),
                    new Vector3(0.04f, 1.1f, 0.04f), rope);
            }

            // a rainbow arc built from bands of thin cubes
            Color[] bands =
            {
                new Color(0.90f, 0.20f, 0.20f), new Color(0.95f, 0.55f, 0.20f),
                new Color(0.95f, 0.90f, 0.25f), new Color(0.30f, 0.75f, 0.35f),
                new Color(0.25f, 0.50f, 0.90f), new Color(0.55f, 0.30f, 0.80f),
            };
            var rainbow = new GameObject("Rainbow");
            rainbow.transform.SetParent(root, true);
            Vector3 rbCenter = new Vector3(0f, -2f, 40f);
            for (int band = 0; band < bands.Length; band++)
            {
                float radius = 26f + band * 1.2f;
                Material bm = Mat(t, $"Rainbow{band}", bands[band], 0.1f, 0f, bands[band] * 0.6f);
                int seg = 26;
                for (int s = 0; s <= seg; s++)
                {
                    float ang = Mathf.PI * s / seg; // 0..pi, a half arc
                    var p = rbCenter + new Vector3(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius, 0f);
                    Spawn(PrimitiveType.Cube, rainbow.transform, "Band", p,
                        new Vector3(1.4f, 1.4f, 1.4f), bm,
                        new Vector3(0f, 0f, ang * Mathf.Rad2Deg));
                }
            }

            // a few distant birds (simple V shapes)
            Material bird = Mat(t, "Bird", new Color(0.15f, 0.15f, 0.18f));
            for (int i = 0; i < 6; i++)
            {
                var p = new Vector3(Random.Range(-25f, 25f), Random.Range(18f, 28f), Random.Range(20f, 45f));
                var v = new GameObject("Bird");
                v.transform.SetParent(root, true);
                Spawn(PrimitiveType.Cube, v.transform, "Wing", p + new Vector3(-0.6f, 0f, 0f),
                    new Vector3(1.3f, 0.1f, 0.4f), bird, new Vector3(0f, 0f, 22f));
                Spawn(PrimitiveType.Cube, v.transform, "Wing", p + new Vector3(0.6f, 0f, 0f),
                    new Vector3(1.3f, 0.1f, 0.4f), bird, new Vector3(0f, 0f, -22f));
            }
        }

        // ================================================================= GRAVEYARD

        static void BuildGraveyardProps(Transform root, ArenaTheme t)
        {
            // a big pale full moon low on the horizon
            Material moon = Mat(t, "Moon", new Color(0.85f, 0.88f, 0.92f), 0f, 0f,
                                new Color(0.80f, 0.84f, 0.92f) * 1.8f);
            Spawn(PrimitiveType.Sphere, root, "Moon", new Vector3(30f, 26f, 50f), Vector3.one * 14f, moon);

            // tombstones ringing the court, leaning at odd angles
            Material stone = Mat(t, "Tombstone", new Color(0.42f, 0.44f, 0.46f));
            for (int i = 0; i < 12; i++)
            {
                float ang = i / 12f * Mathf.PI * 2f;
                float rad = Random.Range(8f, 12f);
                var p = new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                var grave = new GameObject("Grave");
                grave.transform.SetParent(root, true);
                float tilt = Random.Range(-12f, 12f);
                Spawn(PrimitiveType.Cube, grave.transform, "Slab", p + new Vector3(0f, 0.9f, 0f),
                    new Vector3(1.1f, 1.8f, 0.25f), stone, new Vector3(tilt, Random.Range(0f, 360f), tilt));
                Spawn(PrimitiveType.Sphere, grave.transform, "Cap", p + new Vector3(0f, 1.8f, 0f),
                    new Vector3(1.1f, 0.6f, 0.25f), stone, new Vector3(tilt, 0f, tilt));
            }

            // bare dead trees
            Material bark = Mat(t, "DeadBark", new Color(0.16f, 0.13f, 0.11f));
            Vector3[] trees = { new Vector3(-14f, 0f, 13f), new Vector3(15f, 0f, 14f), new Vector3(-15f, 0f, -12f) };
            foreach (var tp in trees)
            {
                var tree = new GameObject("Dead Tree");
                tree.transform.SetParent(root, true);
                float h = Random.Range(4f, 6f);
                Spawn(PrimitiveType.Cylinder, tree.transform, "Trunk", tp + new Vector3(0f, h * 0.5f, 0f),
                    new Vector3(0.3f, h * 0.5f, 0.3f), bark);
                for (int b = 0; b < 5; b++)
                {
                    float ya = b * 72f + Random.Range(-15f, 15f);
                    var dir = Quaternion.Euler(0f, ya, 0f) * Vector3.forward;
                    Spawn(PrimitiveType.Cylinder, tree.transform, "Branch",
                        tp + new Vector3(0f, h * Random.Range(0.6f, 0.95f), 0f) + dir * 0.8f,
                        new Vector3(0.1f, Random.Range(0.8f, 1.4f), 0.1f), bark,
                        new Vector3(55f, ya, 0f));
                }
            }

            // jack-o'-lanterns with warm flicker lights
            Material pumpkin = Mat(t, "Pumpkin", new Color(0.95f, 0.45f, 0.10f), 0.3f, 0f,
                                   new Color(1f, 0.45f, 0.08f) * 2.2f);
            for (int i = 0; i < 6; i++)
            {
                float ang = i / 6f * Mathf.PI * 2f + 0.4f;
                float rad = Random.Range(6f, 9f);
                var p = new Vector3(Mathf.Cos(ang) * rad, 0.5f, Mathf.Sin(ang) * rad);
                Spawn(PrimitiveType.Sphere, root, "Jack-o'-Lantern", p, new Vector3(1f, 0.85f, 1f), pumpkin);
                AddPoint(root, p + Vector3.up * 0.3f, new Color(1f, 0.5f, 0.15f), 7f, 2.2f);
            }

            // a low iron fence of posts around the perimeter
            Material iron = Mat(t, "Fence", new Color(0.12f, 0.12f, 0.14f), 0.3f, 0.6f);
            int posts = 28;
            for (int i = 0; i < posts; i++)
            {
                float ang = i / (float)posts * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(ang) * 13.5f, 0.6f, Mathf.Sin(ang) * 13.5f);
                Spawn(PrimitiveType.Cylinder, root, "Fence Post", p, new Vector3(0.07f, 0.6f, 0.07f), iron);
            }
        }

        // ================================================================= NEON

        static void BuildNeonProps(Transform root, ArenaTheme t)
        {
            // a ring of dark skyscrapers, each studded with a few lit windows
            Material concrete = Mat(t, "Tower", new Color(0.07f, 0.07f, 0.11f), 0.5f);
            Color[] windowCols =
            {
                new Color(0.95f, 0.85f, 0.45f), new Color(0.30f, 0.85f, 0.95f),
                new Color(0.95f, 0.35f, 0.75f),
            };
            int towers = 16;
            for (int i = 0; i < towers; i++)
            {
                float ang = i / (float)towers * Mathf.PI * 2f;
                float rad = Random.Range(26f, 40f);
                float h = Random.Range(20f, 55f);
                var basePos = new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                float w = Random.Range(4f, 8f);
                Spawn(PrimitiveType.Cube, root, "Tower", basePos + new Vector3(0f, h * 0.5f, 0f),
                    new Vector3(w, h, w), concrete);

                Material win = Mat(t, $"Window{i % windowCols.Length}", windowCols[i % windowCols.Length],
                                   0.2f, 0f, windowCols[i % windowCols.Length] * 2.5f);
                int lit = Random.Range(2, 4);
                for (int k = 0; k < lit; k++)
                {
                    var face = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                    Spawn(PrimitiveType.Cube, root, "Window",
                        basePos + new Vector3(0f, Random.Range(4f, h - 2f), 0f) + face * (w * 0.5f + 0.05f),
                        new Vector3(1.2f, 1.2f, 0.1f), win, new Vector3(0f, Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg, 0f));
                }
            }

            // neon sign tubes on poles just outside the court
            Color[] neon =
            {
                new Color(0.95f, 0.15f, 0.55f), new Color(0.20f, 0.95f, 0.85f),
                new Color(0.65f, 0.25f, 0.95f), new Color(0.95f, 0.80f, 0.20f),
                new Color(0.30f, 0.95f, 0.40f),
            };
            Material pole = Mat(t, "SignPole", new Color(0.12f, 0.12f, 0.16f), 0.5f, 0.5f);
            for (int i = 0; i < 6; i++)
            {
                float ang = i / 6f * Mathf.PI * 2f + 0.3f;
                float rad = Random.Range(10f, 13f);
                var p = new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                Color c = neon[i % neon.Length];
                Material nm = Mat(t, $"Neon{i}", c, 0.3f, 0f, c * 3.5f);
                Spawn(PrimitiveType.Cylinder, root, "Sign Pole", p + new Vector3(0f, 1.8f, 0f),
                    new Vector3(0.08f, 1.8f, 0.08f), pole);
                Spawn(PrimitiveType.Cube, root, "Neon Tube", p + new Vector3(0f, 4f, 0f),
                    new Vector3(0.25f, 2.4f, 0.25f), nm, new Vector3(0f, ang * Mathf.Rad2Deg, 0f));
                AddPoint(root, p + new Vector3(0f, 4f, 0f), c, 9f, 1.6f);
            }

            // a few floating holographic billboards
            for (int i = 0; i < 3; i++)
            {
                Color c = neon[(i * 2) % neon.Length];
                Material hm = Mat(t, $"Holo{i}", new Color(c.r, c.g, c.b, 0.6f), 0.4f, 0f, c * 1.8f);
                float ang = i / 3f * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(ang) * 16f, Random.Range(8f, 13f), Mathf.Sin(ang) * 16f);
                Spawn(PrimitiveType.Cube, root, "Hologram", p, new Vector3(5f, 3f, 0.1f), hm,
                    new Vector3(0f, ang * Mathf.Rad2Deg + 90f, 0f));
            }

            // searchlights raking the sky
            Color[] beamCols = { new Color(0.4f, 0.8f, 1f), new Color(1f, 0.4f, 0.8f), new Color(0.7f, 1f, 0.5f) };
            for (int i = 0; i < 4; i++)
            {
                float ang = i / 4f * Mathf.PI * 2f + 0.6f;
                var p = new Vector3(Mathf.Cos(ang) * 14f, 0.2f, Mathf.Sin(ang) * 14f);
                var go = new GameObject("Searchlight");
                go.transform.SetParent(root, true);
                go.transform.position = p;
                go.transform.rotation = Quaternion.Euler(55f, ang * Mathf.Rad2Deg, 0f);
                var l = go.AddComponent<Light>();
                l.type = LightType.Spot;
                l.color = beamCols[i % beamCols.Length];
                l.spotAngle = 22f;
                l.range = 60f;
                l.intensity = 4f;
            }
        }

        // ----------------------------------------------------------------- primitive helpers

        internal static GameObject Spawn(PrimitiveType type, Transform parent, string name, Vector3 pos,
                                         Vector3 scale, Material mat, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            StripCollider(go);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            if (euler.HasValue) go.transform.eulerAngles = euler.Value;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        internal static Light AddPoint(Transform parent, Vector3 pos, Color color, float range, float intensity)
        {
            var go = new GameObject("Point Light");
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            return l;
        }

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

        // ----------------------------------------------------------------- material helpers

        internal static Material Mat(ArenaTheme t, string suffix, Color color, float smoothness = 0.1f,
                                     float metallic = 0f, Color? emission = null)
            => MakeLit($"{t.key}_{suffix}", color, smoothness, metallic, emission);

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

            // alpha < 1 → switch the URP/Lit material to transparent surface mode
            if (color.a < 1f)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }

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
                EnsureDir(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
