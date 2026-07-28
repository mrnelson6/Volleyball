using UnityEngine;
using static Volleyball.EditorTools.ThemedArenaDecorator;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// The world-tour regional courts, one per campaign region in <see cref="RegionRoster"/>.
    /// Same cosmetic-only contract as the fantasy themes: colliders stripped, everything under
    /// one decor root, gameplay untouched. The gameplay half of a region (gravity, wind, drag,
    /// ambience) lives in <see cref="EnvironmentProfile"/> on the runtime region table — theme
    /// keys here must match each region's <c>sceneName</c> so the built scene picks it up.
    /// </summary>
    public static class RegionalArenaThemes
    {
        public static ArenaTheme[] All() => new[]
        {
            new ArenaTheme
            {
                key = "SavannaArena", displayName = "Sunny Savanna",
                blurb = "Dry golden grassland, acacia trees and termite towers under a huge warm sky.",
                proceduralSky = true, skyTint = new Color(0.90f, 0.75f, 0.55f),
                skyGround = new Color(0.82f, 0.68f, 0.45f), atmosphere = 0.9f, exposure = 1.3f,
                sunColor = new Color(1f, 0.90f, 0.70f), sunIntensity = 1.2f,
                sunEuler = new Vector3(55f, -35f, 0f),
                ambient = new Color(0.55f, 0.48f, 0.38f),
                fog = true, fogColor = new Color(0.82f, 0.72f, 0.55f), fogDensity = 0.004f,
                floorColor = new Color(0.76f, 0.62f, 0.38f), floorSmoothness = 0.05f,
                standColor = new Color(0.55f, 0.45f, 0.32f),
                crowdColors = new[]
                {
                    new Color(0.85f, 0.65f, 0.30f), new Color(0.60f, 0.45f, 0.25f),
                    new Color(0.90f, 0.80f, 0.55f), new Color(0.45f, 0.50f, 0.30f),
                },
                netPostColor = new Color(0.35f, 0.26f, 0.16f),
                props = BuildSavannaProps,
            },
            new ArenaTheme
            {
                key = "AmazonArena", displayName = "Amazon Rainforest",
                blurb = "A clearing deep under the canopy: giant trunks, hanging vines, thick green light.",
                proceduralSky = false, flatBackground = new Color(0.02f, 0.08f, 0.04f),
                sunColor = new Color(0.75f, 0.95f, 0.65f), sunIntensity = 0.8f,
                sunEuler = new Vector3(65f, 15f, 0f), shadows = LightShadows.Soft,
                ambient = new Color(0.10f, 0.20f, 0.12f),
                fog = true, fogColor = new Color(0.06f, 0.18f, 0.10f), fogDensity = 0.02f,
                floorColor = new Color(0.16f, 0.28f, 0.14f), floorSmoothness = 0.15f,
                standColor = new Color(0.25f, 0.30f, 0.20f),
                crowdColors = new[]
                {
                    new Color(0.95f, 0.60f, 0.20f), new Color(0.30f, 0.80f, 0.45f),
                    new Color(0.90f, 0.30f, 0.35f), new Color(0.30f, 0.65f, 0.90f),
                },
                netPostColor = new Color(0.30f, 0.40f, 0.25f),
                props = BuildAmazonProps,
            },
            new ArenaTheme
            {
                key = "OutbackArena", displayName = "Australian Outback",
                blurb = "Red dirt and a great monolith on the horizon, windmill creaking in the gusts.",
                proceduralSky = true, skyTint = new Color(0.85f, 0.55f, 0.35f),
                skyGround = new Color(0.80f, 0.45f, 0.30f), atmosphere = 1.1f, exposure = 1.35f,
                sunColor = new Color(1f, 0.75f, 0.50f), sunIntensity = 1.15f,
                sunEuler = new Vector3(35f, -50f, 0f),
                ambient = new Color(0.45f, 0.32f, 0.25f),
                fog = true, fogColor = new Color(0.75f, 0.50f, 0.35f), fogDensity = 0.003f,
                floorColor = new Color(0.72f, 0.38f, 0.22f), floorSmoothness = 0.05f,
                standColor = new Color(0.45f, 0.30f, 0.22f),
                crowdColors = new[]
                {
                    new Color(0.85f, 0.70f, 0.40f), new Color(0.40f, 0.50f, 0.35f),
                    new Color(0.90f, 0.55f, 0.25f), new Color(0.70f, 0.65f, 0.55f),
                },
                netPostColor = new Color(0.30f, 0.20f, 0.14f),
                props = BuildOutbackProps,
            },
            new ArenaTheme
            {
                key = "HimalayaArena", displayName = "Himalayan Peaks",
                blurb = "The roof of the world: snow summits all around and prayer flags in the thin wind.",
                proceduralSky = true, skyTint = new Color(0.65f, 0.78f, 0.95f),
                skyGround = new Color(0.90f, 0.93f, 0.98f), atmosphere = 0.5f, exposure = 1.5f,
                sunColor = new Color(1f, 0.98f, 0.95f), sunIntensity = 1.4f,
                sunEuler = new Vector3(40f, -20f, 0f),
                ambient = new Color(0.45f, 0.52f, 0.62f),
                fog = true, fogColor = new Color(0.80f, 0.86f, 0.95f), fogDensity = 0.006f,
                floorColor = new Color(0.92f, 0.94f, 0.98f), floorSmoothness = 0.3f,
                standColor = new Color(0.55f, 0.58f, 0.66f),
                crowdColors = new[]
                {
                    new Color(0.90f, 0.30f, 0.25f), new Color(0.25f, 0.55f, 0.90f),
                    new Color(0.95f, 0.75f, 0.20f), new Color(0.35f, 0.75f, 0.55f),
                },
                netPostColor = new Color(0.35f, 0.38f, 0.45f),
                props = BuildHimalayaProps,
            },
            new ArenaTheme
            {
                key = "ForestArena", displayName = "Black Forest",
                blurb = "A mossy clearing ringed by old pines, mushrooms glowing in the drizzle-dim light.",
                proceduralSky = false, flatBackground = new Color(0.04f, 0.07f, 0.05f),
                sunColor = new Color(0.70f, 0.85f, 0.60f), sunIntensity = 0.65f,
                sunEuler = new Vector3(55f, 25f, 0f), shadows = LightShadows.Soft,
                ambient = new Color(0.12f, 0.18f, 0.12f),
                fog = true, fogColor = new Color(0.10f, 0.16f, 0.10f), fogDensity = 0.015f,
                floorColor = new Color(0.13f, 0.22f, 0.12f), floorSmoothness = 0.2f,
                standColor = new Color(0.25f, 0.20f, 0.15f),
                crowdColors = new[]
                {
                    new Color(0.55f, 0.65f, 0.35f), new Color(0.75f, 0.55f, 0.30f),
                    new Color(0.45f, 0.40f, 0.50f), new Color(0.80f, 0.75f, 0.60f),
                },
                netPostColor = new Color(0.20f, 0.16f, 0.10f),
                props = BuildForestProps,
            },
            new ArenaTheme
            {
                key = "SaharaArena", displayName = "Sahara Dunes",
                blurb = "An oasis court in a sea of dunes, palms and old sandstone ruins baking in the heat.",
                proceduralSky = true, skyTint = new Color(0.95f, 0.80f, 0.55f),
                skyGround = new Color(0.90f, 0.75f, 0.50f), atmosphere = 1.2f, exposure = 1.4f,
                sunColor = new Color(1f, 0.85f, 0.60f), sunIntensity = 1.3f,
                sunEuler = new Vector3(60f, -30f, 0f),
                ambient = new Color(0.60f, 0.50f, 0.35f),
                fog = true, fogColor = new Color(0.88f, 0.75f, 0.52f), fogDensity = 0.005f,
                floorColor = new Color(0.88f, 0.72f, 0.45f), floorSmoothness = 0.05f,
                standColor = new Color(0.70f, 0.58f, 0.40f),
                crowdColors = new[]
                {
                    new Color(0.95f, 0.90f, 0.80f), new Color(0.40f, 0.55f, 0.75f),
                    new Color(0.85f, 0.45f, 0.25f), new Color(0.60f, 0.30f, 0.45f),
                },
                netPostColor = new Color(0.50f, 0.40f, 0.28f),
                props = BuildSaharaProps,
            },
            new ArenaTheme
            {
                key = "RockiesArena", displayName = "Rocky Mountains",
                blurb = "An alpine meadow court below snow-capped granite, pines and a timber lodge.",
                proceduralSky = true, skyTint = new Color(0.55f, 0.70f, 0.90f),
                skyGround = new Color(0.55f, 0.60f, 0.55f), atmosphere = 0.8f, exposure = 1.3f,
                sunColor = new Color(1f, 0.95f, 0.85f), sunIntensity = 1.1f,
                sunEuler = new Vector3(48f, -40f, 0f),
                ambient = new Color(0.42f, 0.48f, 0.52f),
                fog = true, fogColor = new Color(0.70f, 0.78f, 0.85f), fogDensity = 0.005f,
                floorColor = new Color(0.35f, 0.42f, 0.30f), floorSmoothness = 0.1f,
                standColor = new Color(0.40f, 0.32f, 0.24f),
                crowdColors = new[]
                {
                    new Color(0.80f, 0.30f, 0.25f), new Color(0.30f, 0.45f, 0.60f),
                    new Color(0.85f, 0.75f, 0.50f), new Color(0.35f, 0.55f, 0.35f),
                },
                netPostColor = new Color(0.30f, 0.24f, 0.18f),
                props = BuildRockiesProps,
            },
            new ArenaTheme
            {
                key = "ArcticArena", displayName = "Polar Ice",
                blurb = "A court on the frozen sea: ice floes, an igloo, and the aurora overhead.",
                proceduralSky = false, flatBackground = new Color(0.01f, 0.03f, 0.08f),
                sunColor = new Color(0.75f, 0.85f, 1f), sunIntensity = 0.8f,
                sunEuler = new Vector3(12f, -30f, 0f), shadows = LightShadows.Hard,
                ambient = new Color(0.15f, 0.20f, 0.30f),
                fog = true, fogColor = new Color(0.10f, 0.16f, 0.26f), fogDensity = 0.012f,
                floorColor = new Color(0.75f, 0.85f, 0.92f), floorSmoothness = 0.85f, floorMetallic = 0.1f,
                standColor = new Color(0.65f, 0.75f, 0.85f),
                crowdColors = new[]
                {
                    new Color(0.90f, 0.35f, 0.30f), new Color(0.30f, 0.60f, 0.90f),
                    new Color(0.95f, 0.80f, 0.30f), new Color(0.70f, 0.40f, 0.75f),
                },
                netPostColor = new Color(0.55f, 0.65f, 0.75f),
                props = BuildArcticProps,
            },
        };

        // ================================================================= SAVANNA

        static void BuildSavannaProps(Transform root, ArenaTheme t)
        {
            Material trunk = Mat(t, "Trunk", new Color(0.35f, 0.24f, 0.14f));
            Material canopy = Mat(t, "Canopy", new Color(0.38f, 0.44f, 0.18f));
            Material mound = Mat(t, "Mound", new Color(0.62f, 0.40f, 0.22f));
            Material rock = Mat(t, "Rock", new Color(0.55f, 0.50f, 0.44f));

            // flat-topped acacia trees around the horizon
            Vector3[] trees =
            {
                new Vector3(-19f, 0f, -14f), new Vector3(18f, 0f, 15f),
                new Vector3(-15f, 0f, 18f),  new Vector3(22f, 0f, -10f),
                new Vector3(-26f, 0f, 4f),
            };
            for (int i = 0; i < trees.Length; i++)
            {
                float h = 3.2f + (i % 3) * 0.6f;
                Vector3 p = trees[i];
                Spawn(PrimitiveType.Cylinder, root, "Acacia Trunk",
                      p + new Vector3(0f, h * 0.5f, 0f), new Vector3(0.35f, h * 0.5f, 0.35f), trunk,
                      new Vector3(0f, 0f, (i % 2 == 0 ? 6f : -6f)));
                Spawn(PrimitiveType.Sphere, root, "Acacia Canopy",
                      p + new Vector3(0f, h + 0.5f, 0f), new Vector3(5.2f, 1.2f, 5.2f), canopy);
            }

            // termite mounds: tapering stacked spires
            Vector3[] mounds = { new Vector3(13f, 0f, -13f), new Vector3(-13f, 0f, -16f), new Vector3(16f, 0f, 11f) };
            foreach (var p in mounds)
                for (int s = 0; s < 3; s++)
                    Spawn(PrimitiveType.Sphere, root, "Termite Mound",
                          p + new Vector3(0f, 0.6f + s * 0.8f, 0f),
                          new Vector3(1.8f - s * 0.5f, 1.4f, 1.8f - s * 0.5f), mound);

            foreach (var p in new[] { new Vector3(-11f, 0f, 13f), new Vector3(24f, 0f, 4f) })
                Spawn(PrimitiveType.Sphere, root, "Rock", p + new Vector3(0f, 0.4f, 0f),
                      new Vector3(1.8f, 0.9f, 1.4f), rock);
        }

        // ================================================================= AMAZON

        static void BuildAmazonProps(Transform root, ArenaTheme t)
        {
            Material trunk = Mat(t, "Trunk", new Color(0.24f, 0.17f, 0.10f));
            Material canopy = Mat(t, "Canopy", new Color(0.06f, 0.16f, 0.07f));
            Material vine = Mat(t, "Vine", new Color(0.20f, 0.32f, 0.12f));
            Material leaf = Mat(t, "Leaf", new Color(0.12f, 0.30f, 0.12f));
            Material river = Mat(t, "River", new Color(0.10f, 0.25f, 0.28f), 0.95f);

            // a ring of giant trunks whose canopies close overhead
            for (int i = 0; i < 10; i++)
            {
                float ang = i / 10f * Mathf.PI * 2f + 0.3f;
                float r = 19f + (i % 3) * 3f;
                var p = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                float h = 9f + (i % 4);
                Spawn(PrimitiveType.Cylinder, root, "Kapok Trunk",
                      p + new Vector3(0f, h * 0.5f, 0f), new Vector3(0.9f, h * 0.5f, 0.9f), trunk);
                Spawn(PrimitiveType.Sphere, root, "Kapok Canopy",
                      p + new Vector3(0f, h + 1.5f, 0f), new Vector3(8f, 3.2f, 8f), canopy);

                // a vine dangling from every second canopy
                if (i % 2 == 0)
                    Spawn(PrimitiveType.Cylinder, root, "Vine",
                          p + new Vector3(1.6f, h - 2.5f, 0f), new Vector3(0.08f, 2.5f, 0.08f), vine,
                          new Vector3(4f, 0f, 5f));
            }

            // big understory leaves leaning toward the court
            foreach (var p in new[] { new Vector3(-12f, 0f, -13f), new Vector3(13f, 0f, 12f), new Vector3(-14f, 0f, 11f) })
                Spawn(PrimitiveType.Sphere, root, "Giant Leaf",
                      p + new Vector3(0f, 1.2f, 0f), new Vector3(0.4f, 2.6f, 1.8f), leaf,
                      new Vector3(0f, 30f, 20f));

            // a still dark river sliding past one end
            Spawn(PrimitiveType.Cube, root, "River", new Vector3(0f, -0.04f, -22f),
                  new Vector3(60f, 0.05f, 6f), river);
        }

        // ================================================================= OUTBACK

        static void BuildOutbackProps(Transform root, ArenaTheme t)
        {
            Material rock = Mat(t, "Monolith", new Color(0.62f, 0.28f, 0.16f));
            Material steel = Mat(t, "Steel", new Color(0.55f, 0.55f, 0.58f), 0.5f, 0.7f);
            Material scrub = Mat(t, "Scrub", new Color(0.45f, 0.42f, 0.25f));

            // the great monolith on the horizon
            Spawn(PrimitiveType.Cube, root, "Monolith", new Vector3(-26f, 3f, 20f),
                  new Vector3(16f, 6f, 5f), rock, new Vector3(0f, 18f, 0f));

            // outcrops nearer the court
            foreach (var p in new[] { new Vector3(15f, 0f, -14f), new Vector3(19f, 0f, 12f) })
            {
                Spawn(PrimitiveType.Sphere, root, "Outcrop", p + new Vector3(0f, 0.8f, 0f),
                      new Vector3(3.2f, 1.6f, 2.2f), rock);
                Spawn(PrimitiveType.Sphere, root, "Outcrop", p + new Vector3(1.6f, 0.5f, 0.8f),
                      new Vector3(1.8f, 1.0f, 1.4f), rock);
            }

            // a windpump windmill: pole, rotor hub and four blades
            var basePos = new Vector3(-14f, 0f, -15f);
            Spawn(PrimitiveType.Cylinder, root, "Windmill Pole",
                  basePos + new Vector3(0f, 3.5f, 0f), new Vector3(0.18f, 3.5f, 0.18f), steel);
            Spawn(PrimitiveType.Sphere, root, "Windmill Hub",
                  basePos + new Vector3(0f, 7f, -0.3f), new Vector3(0.5f, 0.5f, 0.5f), steel);
            for (int b = 0; b < 4; b++)
                Spawn(PrimitiveType.Cube, root, "Windmill Blade",
                      basePos + new Vector3(0f, 7f, -0.35f), new Vector3(0.15f, 2.6f, 0.05f), steel,
                      new Vector3(0f, 0f, 45f + b * 90f));

            // tumbleweed scrub
            foreach (var p in new[] { new Vector3(11f, 0f, 14f), new Vector3(-12f, 0f, 12f),
                                      new Vector3(14f, 0f, -11f), new Vector3(-16f, 0f, -11f) })
                Spawn(PrimitiveType.Sphere, root, "Scrub", p + new Vector3(0f, 0.5f, 0f),
                      new Vector3(1.1f, 1.0f, 1.1f), scrub);
        }

        // ================================================================= HIMALAYA

        static void BuildHimalayaProps(Transform root, ArenaTheme t)
        {
            Material granite = Mat(t, "Granite", new Color(0.45f, 0.48f, 0.55f));
            Material snow = Mat(t, "Snow", new Color(0.95f, 0.96f, 1f), 0.3f);
            Material pole = Mat(t, "Pole", new Color(0.30f, 0.24f, 0.18f));
            Color[] flagCols =
            {
                new Color(0.25f, 0.50f, 0.90f), Color.white, new Color(0.90f, 0.25f, 0.20f),
                new Color(0.30f, 0.70f, 0.40f), new Color(0.95f, 0.80f, 0.25f),
            };

            // ringing summits: stepped granite tiers with snow caps
            for (int i = 0; i < 6; i++)
            {
                float ang = i / 6f * Mathf.PI * 2f + 0.5f;
                float r = 30f + (i % 2) * 8f;
                var p = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                float h = 10f + (i % 3) * 4f;
                for (int s = 0; s < 3; s++)
                {
                    float w = 10f - s * 3f;
                    Spawn(PrimitiveType.Cylinder, root, "Peak Tier",
                          p + new Vector3(0f, h * (0.2f + s * 0.28f), 0f),
                          new Vector3(w, h * 0.3f, w), s == 2 ? snow : granite);
                }
            }

            // two prayer-flag lines swooping between poles behind the court
            for (int line = 0; line < 2; line++)
            {
                float z = line == 0 ? -13f : 13f;
                var a = new Vector3(-9f, 0f, z);
                var b = new Vector3(9f, 0f, z);
                Spawn(PrimitiveType.Cylinder, root, "Flag Pole", a + new Vector3(0f, 1.9f, 0f),
                      new Vector3(0.09f, 1.9f, 0.09f), pole);
                Spawn(PrimitiveType.Cylinder, root, "Flag Pole", b + new Vector3(0f, 1.9f, 0f),
                      new Vector3(0.09f, 1.9f, 0.09f), pole);
                for (int f = 0; f < 11; f++)
                {
                    float u = (f + 0.5f) / 11f;
                    float sag = 0.8f * Mathf.Sin(u * Mathf.PI);
                    Vector3 p = Vector3.Lerp(a, b, u) + new Vector3(0f, 3.7f - sag, 0f);
                    Material fm = Mat(t, $"Flag{f % flagCols.Length}", flagCols[f % flagCols.Length]);
                    Spawn(PrimitiveType.Cube, root, "Prayer Flag", p,
                          new Vector3(0.45f, 0.35f, 0.03f), fm, new Vector3(0f, 0f, 8f * Mathf.Sin(f * 2.1f)));
                }
            }
        }

        // ================================================================= BLACK FOREST

        static void BuildForestProps(Transform root, ArenaTheme t)
        {
            Material trunk = Mat(t, "Trunk", new Color(0.22f, 0.15f, 0.10f));
            Material needles = Mat(t, "Needles", new Color(0.07f, 0.16f, 0.09f));
            Material stem = Mat(t, "Stem", new Color(0.85f, 0.80f, 0.70f));
            Material cap = Mat(t, "Cap", new Color(0.75f, 0.20f, 0.15f), 0.3f, 0f,
                               new Color(0.45f, 0.10f, 0.08f));
            Material moss = Mat(t, "Moss", new Color(0.20f, 0.30f, 0.16f));

            // a ring of old pines: trunk + three needle tiers
            for (int i = 0; i < 11; i++)
            {
                float ang = i / 11f * Mathf.PI * 2f;
                float r = 16f + (i % 4) * 3f;
                var p = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                float h = 5f + (i % 3) * 1.5f;
                Spawn(PrimitiveType.Cylinder, root, "Pine Trunk",
                      p + new Vector3(0f, h * 0.4f, 0f), new Vector3(0.28f, h * 0.4f, 0.28f), trunk);
                for (int s = 0; s < 3; s++)
                {
                    float w = 3.0f - s * 0.8f;
                    Spawn(PrimitiveType.Cylinder, root, "Pine Tier",
                          p + new Vector3(0f, h * 0.55f + s * 1.2f, 0f),
                          new Vector3(w, 0.55f, w), needles);
                }
            }

            // a fairy ring of glowing red-cap mushrooms
            var ringC = new Vector3(12f, 0f, -12f);
            for (int m = 0; m < 5; m++)
            {
                float ang = m / 5f * Mathf.PI * 2f;
                var p = ringC + new Vector3(Mathf.Cos(ang) * 1.8f, 0f, Mathf.Sin(ang) * 1.8f);
                Spawn(PrimitiveType.Cylinder, root, "Mushroom Stem",
                      p + new Vector3(0f, 0.3f, 0f), new Vector3(0.16f, 0.3f, 0.16f), stem);
                Spawn(PrimitiveType.Sphere, root, "Mushroom Cap",
                      p + new Vector3(0f, 0.65f, 0f), new Vector3(0.8f, 0.45f, 0.8f), cap);
            }
            AddPoint(root, ringC + new Vector3(0f, 1.2f, 0f), new Color(1f, 0.45f, 0.35f), 6f, 1.2f);

            // mossy boulders
            foreach (var p in new[] { new Vector3(-13f, 0f, 12f), new Vector3(-11f, 0f, -14f) })
                Spawn(PrimitiveType.Sphere, root, "Boulder", p + new Vector3(0f, 0.6f, 0f),
                      new Vector3(2.2f, 1.2f, 1.8f), moss);
        }

        // ================================================================= SAHARA

        static void BuildSaharaProps(Transform root, ArenaTheme t)
        {
            Material dune = Mat(t, "Dune", new Color(0.92f, 0.76f, 0.48f), 0.05f);
            Material water = Mat(t, "Water", new Color(0.25f, 0.55f, 0.60f), 0.95f);
            Material palmTrunk = Mat(t, "PalmTrunk", new Color(0.48f, 0.36f, 0.22f));
            Material frond = Mat(t, "Frond", new Color(0.25f, 0.45f, 0.18f));
            Material sandstone = Mat(t, "Sandstone", new Color(0.80f, 0.66f, 0.44f));

            // rolling dunes to the horizon
            for (int i = 0; i < 5; i++)
            {
                float ang = i / 5f * Mathf.PI * 2f + 0.9f;
                float r = 28f + (i % 2) * 10f;
                var p = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                Spawn(PrimitiveType.Sphere, root, "Dune", p,
                      new Vector3(20f, 3.2f + (i % 3), 11f), dune, new Vector3(0f, ang * 40f, 0f));
            }

            // the oasis pool and its palms
            var oasis = new Vector3(16f, 0f, -15f);
            Spawn(PrimitiveType.Cylinder, root, "Oasis Pool", oasis + new Vector3(0f, 0.02f, 0f),
                  new Vector3(4.2f, 0.03f, 3.2f), water);
            for (int pIdx = 0; pIdx < 2; pIdx++)
            {
                var basePos = oasis + new Vector3(pIdx == 0 ? -3.4f : 3.0f, 0f, pIdx == 0 ? 1.8f : -2.2f);
                float lean = pIdx == 0 ? 10f : -8f;
                Spawn(PrimitiveType.Cylinder, root, "Palm Trunk",
                      basePos + new Vector3(0f, 2.1f, 0f), new Vector3(0.22f, 2.1f, 0.22f), palmTrunk,
                      new Vector3(0f, 0f, lean));
                for (int f = 0; f < 5; f++)
                    Spawn(PrimitiveType.Cube, root, "Palm Frond",
                          basePos + new Vector3(-lean * 0.07f, 4.3f, 0f),
                          new Vector3(2.4f, 0.08f, 0.5f), frond,
                          new Vector3(0f, f * 72f, 18f));
            }

            // half-buried sandstone ruin
            var ruin = new Vector3(-17f, 0f, 14f);
            for (int c = 0; c < 3; c++)
                Spawn(PrimitiveType.Cylinder, root, "Ruin Column",
                      ruin + new Vector3(c * 2.2f, 1.2f, 0f), new Vector3(0.5f, 1.2f + (c % 2) * 0.6f, 0.5f),
                      sandstone);
            Spawn(PrimitiveType.Cylinder, root, "Fallen Column",
                  ruin + new Vector3(2f, 0.5f, 3f), new Vector3(0.5f, 1.8f, 0.5f), sandstone,
                  new Vector3(90f, 30f, 0f));
        }

        // ================================================================= ROCKIES

        static void BuildRockiesProps(Transform root, ArenaTheme t)
        {
            Material granite = Mat(t, "Granite", new Color(0.42f, 0.42f, 0.45f));
            Material snow = Mat(t, "Snowcap", new Color(0.95f, 0.96f, 1f), 0.3f);
            Material trunk = Mat(t, "Trunk", new Color(0.28f, 0.18f, 0.10f));
            Material needles = Mat(t, "Needles", new Color(0.10f, 0.22f, 0.12f));
            Material timber = Mat(t, "Timber", new Color(0.42f, 0.28f, 0.16f));
            Material roof = Mat(t, "Roof", new Color(0.30f, 0.18f, 0.12f));
            Material fire = Mat(t, "Fire", new Color(1f, 0.55f, 0.15f), 0.2f, 0f,
                                new Color(1f, 0.45f, 0.10f) * 3f);

            // granite ranges with snow caps
            for (int i = 0; i < 5; i++)
            {
                float ang = i / 5f * Mathf.PI * 2f + 1.2f;
                float r = 32f + (i % 2) * 6f;
                var p = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                float h = 12f + (i % 3) * 4f;
                for (int s = 0; s < 3; s++)
                {
                    float w = 11f - s * 3.4f;
                    Spawn(PrimitiveType.Cylinder, root, "Range Tier",
                          p + new Vector3(0f, h * (0.2f + s * 0.28f), 0f),
                          new Vector3(w, h * 0.3f, w), s == 2 ? snow : granite);
                }
            }

            // scattered pines
            for (int i = 0; i < 8; i++)
            {
                float ang = i / 8f * Mathf.PI * 2f + 0.4f;
                float r = 17f + (i % 3) * 4f;
                var p = new Vector3(Mathf.Cos(ang) * r, 0f, Mathf.Sin(ang) * r);
                float h = 4.5f + (i % 3);
                Spawn(PrimitiveType.Cylinder, root, "Pine Trunk",
                      p + new Vector3(0f, h * 0.4f, 0f), new Vector3(0.26f, h * 0.4f, 0.26f), trunk);
                for (int s = 0; s < 3; s++)
                    Spawn(PrimitiveType.Cylinder, root, "Pine Tier",
                          p + new Vector3(0f, h * 0.55f + s * 1.1f, 0f),
                          new Vector3(2.6f - s * 0.7f, 0.5f, 2.6f - s * 0.7f), needles);
            }

            // the timber lodge with a warm doorway light
            var lodge = new Vector3(-18f, 0f, 15f);
            Spawn(PrimitiveType.Cube, root, "Lodge", lodge + new Vector3(0f, 1.4f, 0f),
                  new Vector3(6f, 2.8f, 4.5f), timber, new Vector3(0f, 25f, 0f));
            Spawn(PrimitiveType.Cube, root, "Lodge Roof", lodge + new Vector3(0f, 3.4f, 0f),
                  new Vector3(4.9f, 4.9f, 4.9f), roof, new Vector3(0f, 25f, 45f));
            AddPoint(root, lodge + new Vector3(2.5f, 1.5f, 1.5f), new Color(1f, 0.75f, 0.45f), 9f, 1.6f);

            // campfire by the court
            var fireP = new Vector3(13f, 0f, -12f);
            Spawn(PrimitiveType.Sphere, root, "Campfire", fireP + new Vector3(0f, 0.25f, 0f),
                  new Vector3(0.6f, 0.5f, 0.6f), fire);
            AddPoint(root, fireP + new Vector3(0f, 0.8f, 0f), new Color(1f, 0.55f, 0.25f), 7f, 1.8f);
        }

        // ================================================================= ARCTIC

        static void BuildArcticProps(Transform root, ArenaTheme t)
        {
            Material ice = Mat(t, "Ice", new Color(0.88f, 0.94f, 1f), 0.7f);
            Material berg = Mat(t, "Berg", new Color(0.72f, 0.84f, 0.95f), 0.6f);
            Material snow = Mat(t, "Snow", new Color(0.95f, 0.97f, 1f), 0.3f);

            // drifting floes on the frozen sea
            for (int i = 0; i < 7; i++)
            {
                float ang = i / 7f * Mathf.PI * 2f + 0.8f;
                float r = 16f + (i % 3) * 6f;
                var p = new Vector3(Mathf.Cos(ang) * r, 0.06f, Mathf.Sin(ang) * r);
                Spawn(PrimitiveType.Cylinder, root, "Ice Floe", p,
                      new Vector3(3f + (i % 3) * 1.5f, 0.12f, 2.5f + (i % 2) * 1.5f), ice,
                      new Vector3(0f, i * 47f, 0f));
            }

            // jagged icebergs on the horizon
            foreach (var p in new[] { new Vector3(-28f, 0f, 18f), new Vector3(26f, 0f, -20f), new Vector3(30f, 0f, 14f) })
            {
                Spawn(PrimitiveType.Cube, root, "Iceberg", p + new Vector3(0f, 3f, 0f),
                      new Vector3(6f, 7f, 5f), berg, new Vector3(12f, 30f, 8f));
                Spawn(PrimitiveType.Cube, root, "Iceberg", p + new Vector3(2.5f, 1.8f, -1.5f),
                      new Vector3(4f, 4.5f, 3.5f), berg, new Vector3(-8f, 60f, -10f));
            }

            // the igloo with its glowing doorway
            var igloo = new Vector3(15f, 0f, 13f);
            Spawn(PrimitiveType.Sphere, root, "Igloo", igloo,
                  new Vector3(4.2f, 2.6f, 4.2f), snow);
            Spawn(PrimitiveType.Cylinder, root, "Igloo Door", igloo + new Vector3(-2.2f, 0.5f, 0f),
                  new Vector3(0.9f, 1.1f, 0.9f), snow, new Vector3(0f, 0f, 90f));
            AddPoint(root, igloo + new Vector3(-2.8f, 0.7f, 0f), new Color(1f, 0.75f, 0.45f), 6f, 1.4f);

            // the aurora: tall translucent emissive ribbons hanging in the far sky
            Color[] auroraCols =
            {
                new Color(0.25f, 1f, 0.55f), new Color(0.30f, 0.85f, 0.95f), new Color(0.70f, 0.40f, 0.95f),
            };
            for (int i = 0; i < 3; i++)
            {
                Color c = auroraCols[i];
                Material am = Mat(t, $"Aurora{i}", new Color(c.r, c.g, c.b, 0.35f), 0.2f, 0f, c * 2.2f);
                Spawn(PrimitiveType.Cube, root, "Aurora Ribbon",
                      new Vector3(-20f + i * 20f, 26f + i * 3f, 42f + i * 6f),
                      new Vector3(26f, 9f, 0.1f), am, new Vector3(8f, -12f + i * 14f, 4f));
            }
        }
    }
}
