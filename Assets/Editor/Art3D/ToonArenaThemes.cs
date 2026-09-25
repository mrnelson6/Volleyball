using System;
using UnityEngine;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// The regional arenas converted to the 3D toon style: per-biome lighting plus a prop layout
    /// over the generated meshes in Assets/Art/Arenas/&lt;folder&gt; (Tools/blender/arena_gen.py).
    /// <see cref="ThemedLevelBuilder"/> builds these instead of the primitive-based
    /// <see cref="ThemedArenaDecorator"/> theme with the same scene key, so scene names — and with
    /// them CourtEnvironment's regional quirks and the campaign's region scenes — are unchanged.
    ///
    /// Layout rules: court centred at the origin; the broadcast camera sits at +X looking toward -X,
    /// so tall dressing goes on the far (-X) side and the flanks; props stay on the flat sand pad
    /// (|x| &lt; 16, |z| &lt; 22) and clear of the court + margin (DecorColliders makes them solid).
    /// </summary>
    public static class ToonArenaThemes
    {
        public class Theme
        {
            public string key;       // scene name, matches ThemedArenaDecorator's theme key
            public string folder;    // Assets/Art/Arenas/<folder>
            public bool water;       // has a prop_ocean (sea / river on the far side)
            public ToonEnvironment.Preset lighting;
            public Action<Placer> dress;
        }

        /// <summary>Places a theme's props with its materials.</summary>
        public class Placer
        {
            public string dir;
            public Material props, ground;
            public Transform root;

            public GameObject Put(string name, float x, float z, float yaw = 0f, float scale = 1f)
                => ToonArtKit.PropFrom(dir, name, new Vector3(x, 0f, z), yaw, scale, props, root);

            /// <summary>Deterministic sprinkle of a small prop in a ring band around the court.</summary>
            public void Scatter(string name, int count, int seed, float minScale = 0.8f, float maxScale = 1.3f)
            {
                var rng = new System.Random(seed);
                int placed = 0, tries = 0;
                while (placed < count && tries++ < count * 20)
                {
                    float x = (float)(rng.NextDouble() * 30.0 - 15.0);
                    float z = (float)(rng.NextDouble() * 42.0 - 21.0);
                    if (Mathf.Abs(x) < 6.5f && Mathf.Abs(z) < 10.5f) continue; // keep the court clear
                    if (x > 5f) continue;                                    // and the camera's side (foreground)
                    float s = Mathf.Lerp(minScale, maxScale, (float)rng.NextDouble());
                    Put(name, x, z, (float)(rng.NextDouble() * 360.0), s);
                    placed++;
                }
            }
        }

        public static Theme Find(string key) => Array.Find(All, t => t.key == key);

        public static readonly Theme[] All =
        {
            new Theme
            {
                key = "SavannaArena", folder = "Savanna",
                lighting = new ToonEnvironment.Preset
                {
                    sunColor = new Color(1.00f, 0.90f, 0.72f), sunIntensity = 1.2f, sunEuler = new Vector3(40f, -50f, 0f),
                    ambientSky = new Color(0.75f, 0.72f, 0.66f), ambientEquator = new Color(0.78f, 0.68f, 0.52f),
                    ambientGround = new Color(0.55f, 0.45f, 0.30f),
                    skyTint = new Color(0.62f, 0.56f, 0.46f), skyGround = new Color(0.72f, 0.62f, 0.46f), skyExposure = 1.3f,
                    fogColor = new Color(0.92f, 0.82f, 0.64f),
                },
                dress = p =>
                {
                    p.Put("acacia", -12f, -14f, 20f, 1.1f);
                    p.Put("acacia_b", -14f, 9f, 150f, 1.2f);
                    p.Put("acacia", 11f, 18f, 260f, 0.9f);
                    p.Put("acacia_b", -9f, 19f, 60f, 0.8f);
                    p.Put("baobab", -15f, -2f, 0f, 1.1f);
                    p.Put("termite_mound", -8f, 13f, 0f, 1.0f);
                    p.Put("termite_mound", 9f, -15f, 90f, 1.3f);
                    p.Put("termite_mound", -9.5f, -16f, 200f, 0.8f);
                    p.Put("waterhole", -10.5f, 3.5f, 90f, 1.0f);
                    p.Put("rock_a", -13f, -8f, 0f, 1.6f);
                    p.Put("rock_b", 12f, -18f, 40f, 1.4f);
                    p.Scatter("grass_tuft", 38, 11);
                },
            },
            new Theme
            {
                key = "AmazonArena", folder = "Amazon", water = true,
                lighting = new ToonEnvironment.Preset
                {
                    sunColor = new Color(0.95f, 1.00f, 0.86f), sunIntensity = 1.05f, sunEuler = new Vector3(58f, -30f, 0f),
                    ambientSky = new Color(0.50f, 0.68f, 0.55f), ambientEquator = new Color(0.45f, 0.58f, 0.44f),
                    ambientGround = new Color(0.28f, 0.34f, 0.22f),
                    skyTint = new Color(0.40f, 0.62f, 0.52f), skyGround = new Color(0.40f, 0.52f, 0.40f), skyExposure = 1.1f,
                    fogColor = new Color(0.55f, 0.72f, 0.58f), fogStart = 25f, fogEnd = 130f,
                },
                dress = p =>
                {
                    p.Put("jungle_tree", -13f, -13f, 0f, 1.0f);
                    p.Put("jungle_tree_b", -14.5f, 5f, 70f, 1.1f);
                    p.Put("jungle_tree", -11f, 17f, 140f, 0.9f);
                    p.Put("jungle_tree_b", 12f, -18f, 200f, 0.9f);
                    p.Put("jungle_tree", 13f, 17f, 300f, 1.0f);
                    p.Put("fallen_log", -8.5f, -11f, 35f, 1.0f);
                    p.Put("flower_bush", -8f, 12f, 0f, 1.2f);
                    p.Put("flower_bush", 9f, -13f, 90f, 1.0f);
                    p.Put("rock_a", -12f, -5f, 0f, 1.3f);
                    p.Scatter("fern", 18, 21, 0.9f, 1.5f);
                    p.Scatter("bigleaf", 16, 23, 0.9f, 1.4f);
                },
            },
            new Theme
            {
                key = "SaharaArena", folder = "Sahara",
                lighting = new ToonEnvironment.Preset
                {
                    sunColor = new Color(1.00f, 0.92f, 0.78f), sunIntensity = 1.3f, sunEuler = new Vector3(50f, -40f, 0f),
                    ambientSky = new Color(0.70f, 0.74f, 0.88f), ambientEquator = new Color(0.86f, 0.73f, 0.58f),
                    ambientGround = new Color(0.72f, 0.54f, 0.36f),
                    skyTint = new Color(0.55f, 0.62f, 0.80f), skyGround = new Color(0.80f, 0.66f, 0.50f), skyExposure = 1.35f,
                    fogColor = new Color(0.95f, 0.86f, 0.72f),
                },
                dress = p =>
                {
                    p.Put("oasis", -11f, 11f, 20f, 1.0f);
                    p.Put("date_palm", -13.5f, 13.5f, 0f, 1.0f);
                    p.Put("date_palm", -8.5f, 14.5f, 120f, 0.85f);
                    p.Put("date_palm", -12f, 7.5f, 240f, 1.1f);
                    p.Put("tent", -10f, -12f, 30f, 1.0f);
                    p.Put("tent", -14f, -5f, 70f, 0.9f);
                    p.Put("rock_outcrop", -15f, 2f, 0f, 1.1f);
                    p.Put("rock_outcrop", 11f, -16f, 45f, 0.9f);
                    p.Put("rock_b", 12f, 17f, 10f, 1.5f);
                    foreach (var sx in new[] { -1f, 1f })
                        foreach (var sz in new[] { -1f, 1f })
                            p.Put("lantern_post", sx * 6.2f, sz * 10f);
                },
            },
            new Theme
            {
                key = "ArcticArena", folder = "Arctic", water = true,
                lighting = new ToonEnvironment.Preset
                {
                    sunColor = new Color(0.96f, 0.97f, 1.00f), sunIntensity = 1.05f, sunEuler = new Vector3(28f, -55f, 0f),
                    ambientSky = new Color(0.70f, 0.80f, 0.95f), ambientEquator = new Color(0.74f, 0.80f, 0.90f),
                    ambientGround = new Color(0.66f, 0.72f, 0.84f),
                    skyTint = new Color(0.50f, 0.60f, 0.76f), skyGround = new Color(0.72f, 0.78f, 0.88f), skyExposure = 1.2f,
                    fogColor = new Color(0.85f, 0.90f, 0.97f),
                },
                dress = p =>
                {
                    p.Put("pine_snow", -12f, -14f, 0f, 1.1f);
                    p.Put("pine_snow_b", -14f, -6f, 40f, 1.2f);
                    p.Put("pine_snow", -11f, 16f, 80f, 1.0f);
                    p.Put("pine_snow_b", 12f, -17f, 120f, 1.0f);
                    p.Put("pine_snow", 12f, 16f, 200f, 0.9f);
                    p.Put("pine_snow_b", -9f, 19f, 10f, 0.8f);
                    p.Put("igloo", -10.5f, 5f, 90f, 1.0f);
                    p.Put("ice_blocks", -8f, -12f, 0f, 1.0f);
                    p.Put("ice_blocks", 9f, 13f, 60f, 1.2f);
                    p.Put("snowman", -7.5f, 11.5f, 110f, 1.0f);
                    p.Put("rock_a", -13f, 10f, 0f, 1.2f);
                    p.Put("floes", 0f, 0f);
                },
            },
        };
    }

    /// <summary>Builds a <see cref="ToonArenaThemes.Theme"/>'s environment into the open scene
    /// (lighting, broadcast camera, terrain/backdrop/water/court, props, solid dressing).</summary>
    public static class ToonArenaDecorator
    {
        public static void Build(ToonArenaThemes.Theme t)
        {
            var root = new GameObject(t.key + " Toon Decor").transform;
            ToonArtKit.BuildLighting(root, t.lighting);
            ToonArtKit.BuildBroadcastCamera();

            string dir = $"{ToonArtKit.ArenaDir}/{t.folder}";
            var (props, ground) = ToonArtKit.ArenaMaterials(t.folder);
            ToonArtKit.PropFrom(dir, "terrain", Vector3.zero, 0f, 1f, ground, root, castShadows: false);
            ToonArtKit.PropFrom(dir, "backdrop", Vector3.zero, 0f, 1f, props, root, castShadows: false);
            ToonArtKit.PropFrom(dir, "court", Vector3.zero, 0f, 1f, ground, root);
            if (t.water) ToonArtKit.PropFrom(dir, "ocean", Vector3.zero, 0f, 1f, ground, root, castShadows: false);

            t.dress(new ToonArenaThemes.Placer { dir = dir, props = props, ground = ground, root = root });
            int solids = DecorColliders.ApplyTo(root);
            Debug.Log($"[Volleyball] Toon {t.key} dressed; {solids} props made solid.");
        }
    }
}
