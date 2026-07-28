using System.IO;
using UnityEditor;
using UnityEngine;

namespace Volleyball.EditorTools
{
    /// <summary>
    /// Bakes the campaign's stylized pixel world map — the same code-generated-art spirit as
    /// <see cref="CharacterArt"/>. Continents are unions of hand-tuned ellipse blobs with a
    /// Perlin-wobbled coastline, drawn on a low-res grid and scaled up with chunky pixels
    /// (point filtering) so it matches the game's baked-sprite look. Region pins are placed
    /// by <see cref="RegionDef.mapSpot"/>, tuned to THESE continents — reshape a landmass
    /// and check the pins still sit on it (Volleyball → Rebake World Map Texture).
    /// </summary>
    public static class WorldMapArt
    {
        const string AssetPath = "Assets/Resources/UI/world_map.png";

        const int GridW = 192, GridH = 96; // the pixel-art grid (2:1, roughly equirectangular)
        const int CellPx = 5;              // baked PNG is 960x480

        // (centre u, centre v, radius u, radius v) — union of ellipses per landmass.
        static readonly Vector4[] Land =
        {
            // North America (+ Alaska, Mexico taper, Central America)
            new Vector4(0.155f, 0.760f, 0.090f, 0.105f),
            new Vector4(0.210f, 0.690f, 0.065f, 0.060f),
            new Vector4(0.115f, 0.690f, 0.050f, 0.050f),
            new Vector4(0.085f, 0.800f, 0.045f, 0.040f),
            new Vector4(0.195f, 0.600f, 0.025f, 0.050f),
            new Vector4(0.225f, 0.545f, 0.014f, 0.028f),
            // South America
            new Vector4(0.265f, 0.475f, 0.050f, 0.055f),
            new Vector4(0.290f, 0.410f, 0.050f, 0.070f),
            new Vector4(0.270f, 0.300f, 0.028f, 0.060f),
            new Vector4(0.258f, 0.215f, 0.016f, 0.045f),
            // Africa (+ Madagascar)
            new Vector4(0.505f, 0.615f, 0.075f, 0.050f),
            new Vector4(0.545f, 0.520f, 0.055f, 0.060f),
            new Vector4(0.555f, 0.415f, 0.037f, 0.055f),
            new Vector4(0.556f, 0.345f, 0.020f, 0.033f),
            new Vector4(0.617f, 0.375f, 0.012f, 0.026f),
            // Europe (+ Scandinavia, Iberia)
            new Vector4(0.505f, 0.755f, 0.050f, 0.045f),
            new Vector4(0.540f, 0.800f, 0.045f, 0.040f),
            new Vector4(0.545f, 0.865f, 0.028f, 0.045f),
            new Vector4(0.472f, 0.705f, 0.020f, 0.028f),
            // Asia (+ India, SE Asia, Arabia, Japan)
            new Vector4(0.670f, 0.790f, 0.130f, 0.090f),
            new Vector4(0.780f, 0.740f, 0.090f, 0.085f),
            new Vector4(0.600f, 0.750f, 0.060f, 0.060f),
            new Vector4(0.665f, 0.585f, 0.032f, 0.060f),
            new Vector4(0.735f, 0.565f, 0.026f, 0.050f),
            new Vector4(0.585f, 0.590f, 0.032f, 0.042f),
            new Vector4(0.850f, 0.710f, 0.012f, 0.035f),
            // Indonesia
            new Vector4(0.755f, 0.475f, 0.030f, 0.013f),
            new Vector4(0.795f, 0.450f, 0.022f, 0.011f),
            // Australia + New Zealand
            new Vector4(0.830f, 0.285f, 0.055f, 0.047f),
            new Vector4(0.905f, 0.185f, 0.010f, 0.022f),
        };

        // Ice sheets (drawn over land/sea in white): Greenland; Antarctica is the v<0.045 band.
        static readonly Vector4[] Ice =
        {
            new Vector4(0.345f, 0.865f, 0.040f, 0.050f),
        };

        // The Cloud Kingdom: white puffs out over the Pacific where the finals pin floats.
        static readonly Vector4[] Clouds =
        {
            new Vector4(0.065f, 0.570f, 0.025f, 0.012f),
            new Vector4(0.085f, 0.555f, 0.020f, 0.010f),
            new Vector4(0.075f, 0.588f, 0.018f, 0.009f),
        };

        static readonly Color SeaDeep = new Color(0.09f, 0.24f, 0.40f);
        static readonly Color SeaShallow = new Color(0.16f, 0.36f, 0.53f);
        static readonly Color LandGreen = new Color(0.40f, 0.58f, 0.32f);
        static readonly Color LandSand = new Color(0.72f, 0.64f, 0.40f);
        static readonly Color IceWhite = new Color(0.88f, 0.92f, 0.96f);
        static readonly Color CloudWhite = new Color(0.93f, 0.95f, 0.99f);

        [MenuItem("Volleyball/Rebake World Map Texture")]
        public static void Rebake()
        {
            string abs = AbsPath(AssetPath);
            if (File.Exists(abs)) File.Delete(abs);
            GetSprite();
            Debug.Log("[Volleyball] World map rebaked at " + AssetPath);
        }

        /// <summary>The world-map sprite, baking the PNG on first use (like the character art).</summary>
        public static Sprite GetSprite()
        {
            if (!File.Exists(AbsPath(AssetPath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AbsPath(AssetPath)));
                Texture2D tex = Bake();
                File.WriteAllBytes(AbsPath(AssetPath), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            }

            var imp = (TextureImporter)AssetImporter.GetAtPath(AssetPath);
            if (imp != null && (imp.textureType != TextureImporterType.Sprite
                                || imp.filterMode != FilterMode.Point))
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.filterMode = FilterMode.Point;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.mipmapEnabled = false;
                imp.spritePixelsPerUnit = 100f;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(AssetPath);
        }

        static Texture2D Bake()
        {
            // pass 1: masks on the low-res grid
            var land = new bool[GridW, GridH];
            var ice = new bool[GridW, GridH];
            var cloud = new bool[GridW, GridH];
            for (int y = 0; y < GridH; y++)
                for (int x = 0; x < GridW; x++)
                {
                    float u = (x + 0.5f) / GridW;
                    float v = (y + 0.5f) / GridH;
                    float wobble = (Mathf.PerlinNoise(u * 14.3f, v * 14.3f) - 0.5f) * 0.25f;

                    land[x, y] = Field(Land, u, v) + wobble > 0f;
                    ice[x, y] = Field(Ice, u, v) + wobble > 0f
                                || v < 0.045f + (Mathf.PerlinNoise(u * 21f, 0.7f) - 0.5f) * 0.02f;
                    cloud[x, y] = Field(Clouds, u, v) + wobble * 0.4f > 0f;
                }

            // pass 2: colours, with coast/shore detection from the masks
            var tex = new Texture2D(GridW * CellPx, GridH * CellPx, TextureFormat.RGBA32, false);
            for (int y = 0; y < GridH; y++)
                for (int x = 0; x < GridW; x++)
                {
                    float u = (x + 0.5f) / GridW;
                    float v = (y + 0.5f) / GridH;
                    Color c;

                    if (ice[x, y])
                    {
                        c = NextToOpenSea(land, ice, x, y) ? IceWhite * 0.82f : IceWhite;
                        c.a = 1f;
                    }
                    else if (land[x, y])
                    {
                        // sandy desert belt around the Sahara latitudes, greener elsewhere
                        float belt = Mathf.Clamp01(1f - Mathf.Abs(v - 0.60f) / 0.055f);
                        c = Color.Lerp(LandGreen, LandSand, belt * 0.6f);
                        float shade = (Mathf.PerlinNoise(u * 40f, v * 40f) - 0.5f) * 0.10f;
                        c = new Color(c.r + shade, c.g + shade, c.b + shade);
                        if (NextToOpenSea(land, ice, x, y)) c *= 0.62f; // coastline
                        c.a = 1f;
                    }
                    else if (cloud[x, y])
                    {
                        c = CloudWhite;
                    }
                    else
                    {
                        c = NextToShore(land, ice, x, y) ? SeaShallow : SeaDeep;
                    }

                    for (int py = 0; py < CellPx; py++)
                        for (int px = 0; px < CellPx; px++)
                            tex.SetPixel(x * CellPx + px, y * CellPx + py, c);
                }
            tex.Apply();
            return tex;
        }

        /// <summary>Union-of-ellipses field: positive inside some blob, negative outside all.</summary>
        static float Field(Vector4[] blobs, float u, float v)
        {
            float best = float.NegativeInfinity;
            foreach (Vector4 b in blobs)
            {
                float du = (u - b.x) / b.z;
                float dv = (v - b.y) / b.w;
                best = Mathf.Max(best, 1f - Mathf.Sqrt(du * du + dv * dv));
            }
            return best;
        }

        static bool NextToOpenSea(bool[,] land, bool[,] ice, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= GridW || ny >= GridH) continue;
                    if (!land[nx, ny] && !ice[nx, ny]) return true;
                }
            return false;
        }

        static bool NextToShore(bool[,] land, bool[,] ice, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= GridW || ny >= GridH) continue;
                    if (land[nx, ny] || ice[nx, ny]) return true;
                }
            return false;
        }

        static string AbsPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
