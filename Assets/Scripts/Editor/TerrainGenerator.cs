using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FullyVolted.EditorTools
{
    // Builds the ground: a large Unity Terrain with hills that rise toward the rim (so the edge of the world
    // hides in the fog), a flat work area in the middle for the tower, and three procedurally made ground
    // textures (grass, dirt, rock) painted by height and slope. No downloaded assets.
    internal static class TerrainGenerator
    {
        private const float Size = 1000f;
        private const float Height = 130f;
        private const int HeightmapResolution = 513;
        private const int AlphamapResolution = 512;
        private const int TextureSize = 512;

        // The terrain is lowered by this much so the flat middle sits exactly at world y = 0, where the tower is.
        internal const float FlatHeight = 6f;
        // Flat inside this radius, rolling hills from here out to HillsFullRadius.
        // Wide enough for the town round the tower (it reaches ~95m from the world origin at its far corners).
        private const float FlatRadius = 100f;
        private const float HillsFullRadius = 300f;
        private const float HillHeight = 58f;
        // Hills climb again toward the rim, hiding the end of the terrain.
        private const float RimStartRadius = 330f;
        private const float RimHeight = 48f;

        public static Terrain Create(string folder)
        {
            var textureFolder = EnsureFolder(folder, "Textures");

            var layers = new[]
            {
                GetLayer(textureFolder, "Grass", new Color(0.18f, 0.30f, 0.10f), new Color(0.38f, 0.52f, 0.20f), 6f, false),
                GetLayer(textureFolder, "Dirt", new Color(0.28f, 0.22f, 0.15f), new Color(0.50f, 0.42f, 0.30f), 5f, false),
                GetLayer(textureFolder, "Rock", new Color(0.26f, 0.26f, 0.26f), new Color(0.58f, 0.57f, 0.55f), 9f, true),
                // Sun-dried grass: patches of it among the green break up the big meadows.
                GetLayer(textureFolder, "Dry Grass", new Color(0.34f, 0.33f, 0.14f), new Color(0.66f, 0.60f, 0.30f), 7f, false),
            };

            // Rewritten in place when it exists: the landscape scene and the climb scene share this one asset,
            // and deleting it would change its GUID and leave the other scene pointing at nothing.
            var dataPath = $"{folder}/Landscape Terrain.asset";
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
            var isNew = data == null;
            if (isNew)
                data = new TerrainData();

            data.heightmapResolution = HeightmapResolution;
            data.alphamapResolution = AlphamapResolution;
            data.size = new Vector3(Size, Height, Size);
            data.SetHeights(0, 0, MakeHeights());
            data.terrainLayers = layers;
            data.SetAlphamaps(0, 0, MakeSplat(data));

            if (isNew)
                AssetDatabase.CreateAsset(data, dataPath);
            else
                EditorUtility.SetDirty(data);

            var terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "Terrain";
            terrainObject.transform.position = new Vector3(-Size / 2f, -FlatHeight, -Size / 2f);
            GameObjectUtility.SetStaticEditorFlags(terrainObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic);

            var terrain = terrainObject.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 8f;
            // The terrain never needs to throw shadows: nothing stands below it.
            terrain.shadowCastingMode = ShadowCastingMode.Off;

            VegetationBuilder.Create(terrain, folder);
            return terrain;
        }

        private static float[,] MakeHeights()
        {
            var heights = new float[HeightmapResolution, HeightmapResolution];
            var flat = FlatHeight / Height;

            for (int y = 0; y < HeightmapResolution; y++)
            {
                for (int x = 0; x < HeightmapResolution; x++)
                {
                    var nx = x / (HeightmapResolution - 1f);
                    var ny = y / (HeightmapResolution - 1f);
                    var distance = DistanceFromCenter(nx, ny);

                    var hills = Mathf.Pow(HillNoise(nx, ny), 1.4f);
                    var hillBlend = Smooth(FlatRadius, HillsFullRadius, distance);
                    var rimBlend = Smooth(RimStartRadius, Size / 2f, distance);

                    heights[y, x] = flat + hillBlend * hills * (HillHeight / Height) + rimBlend * (RimHeight / Height);
                }
            }

            return heights;
        }

        // Grass everywhere, in two kinds (fresh and sun-dried) that drift into each other in irregular patches;
        // bare dirt around the work area and in scattered bald spots; rock on steep slopes.
        private static float[,,] MakeSplat(TerrainData data)
        {
            var maps = new float[AlphamapResolution, AlphamapResolution, 4];

            for (int y = 0; y < AlphamapResolution; y++)
            {
                for (int x = 0; x < AlphamapResolution; x++)
                {
                    var nx = x / (AlphamapResolution - 1f);
                    var ny = y / (AlphamapResolution - 1f);

                    var rock = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(22f, 38f, data.GetSteepness(nx, ny)));

                    var patches = Mathf.PerlinNoise(nx * 14f + 7.3f, ny * 14f + 2.1f);
                    var dirt = (1f - Smooth(16f, 48f, DistanceFromCenter(nx, ny))) * 0.95f +
                               Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.68f, 0.82f, patches)) * 0.6f;
                    dirt = Mathf.Clamp01(dirt) * (1f - rock);

                    // Three scales of noise, so the dry patches have large areas, medium clumps and small speckles.
                    var dryNoise = Mathf.PerlinNoise(nx * 4f + 40.5f, ny * 4f + 13.2f) * 0.5f +
                                   Mathf.PerlinNoise(nx * 11f + 5.7f, ny * 11f + 71.3f) * 0.3f +
                                   Mathf.PerlinNoise(nx * 40f + 22.9f, ny * 40f + 3.8f) * 0.2f;
                    var dry = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, 0.66f, dryNoise));

                    var open = Mathf.Max(0f, 1f - rock - dirt);
                    var grass = open * (1f - dry);
                    var dryGrass = open * dry;
                    var total = grass + dryGrass + dirt + rock;

                    maps[y, x, 0] = grass / total;
                    maps[y, x, 1] = dirt / total;
                    maps[y, x, 2] = rock / total;
                    maps[y, x, 3] = dryGrass / total;
                }
            }

            return maps;
        }

        private static float HillNoise(float nx, float ny)
        {
            var sum = 0f;
            var amplitude = 0.5f;
            var total = 0f;
            var frequency = 3f;
            for (int octave = 0; octave < 5; octave++)
            {
                sum += amplitude * Mathf.PerlinNoise(nx * frequency + 11.7f * octave, ny * frequency + 4.2f * octave);
                total += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }

            return sum / total;
        }

        private static float DistanceFromCenter(float nx, float ny)
        {
            var wx = (nx - 0.5f) * Size;
            var wz = (ny - 0.5f) * Size;
            return Mathf.Sqrt(wx * wx + wz * wz);
        }

        private static float Smooth(float from, float to, float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));
        }

        // ---- ground textures ----

        private static TerrainLayer GetLayer(string folder, string layerName, Color dark, Color light, float tileSize, bool ridged)
        {
            var layerPath = $"{folder}/{layerName}.terrainlayer";
            var existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (existing != null)
                return existing;

            var layer = new TerrainLayer
            {
                diffuseTexture = GetTexture(folder, layerName, dark, light, ridged),
                tileSize = new Vector2(tileSize, tileSize),
                smoothness = 0f,
                metallic = 0f
            };
            AssetDatabase.CreateAsset(layer, layerPath);
            return layer;
        }

        private static Texture2D GetTexture(string folder, string textureName, Color dark, Color light, bool ridged)
        {
            var path = $"{folder}/{textureName}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
                return existing;

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            var pixels = new Color[TextureSize * TextureSize];
            var seed = textureName.GetHashCode() & 0xFFFF;

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    var u = x / (float)TextureSize;
                    var v = y / (float)TextureSize;

                    var broad = Fbm(u, v, 4, 5, seed);
                    var fine = Fbm(u, v, 48, 2, seed + 99);
                    var value = ridged ? 1f - Mathf.Abs(broad * 2f - 1f) : broad;
                    value = Mathf.Clamp01(value * 0.8f + fine * 0.35f - 0.1f);

                    pixels[y * TextureSize + x] = Color.Lerp(dark, light, value);
                }
            }

            texture.SetPixels(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.anisoLevel = 8;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // Value noise whose lattice wraps around, so the finished texture tiles without seams.
        internal static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
        {
            var sum = 0f;
            var amplitude = 0.5f;
            var total = 0f;
            var period = basePeriod;
            for (int octave = 0; octave < octaves; octave++)
            {
                sum += amplitude * PeriodicNoise(u * period, v * period, period, seed + octave * 31);
                total += amplitude;
                amplitude *= 0.5f;
                period *= 2;
            }

            return sum / total;
        }

        private static float PeriodicNoise(float x, float y, int period, int seed)
        {
            var x0 = Mathf.FloorToInt(x);
            var y0 = Mathf.FloorToInt(y);
            var fx = Mathf.SmoothStep(0f, 1f, x - x0);
            var fy = Mathf.SmoothStep(0f, 1f, y - y0);

            var xa = Wrap(x0, period);
            var xb = Wrap(x0 + 1, period);
            var ya = Wrap(y0, period);
            var yb = Wrap(y0 + 1, period);

            return Mathf.Lerp(
                Mathf.Lerp(Hash(xa, ya, seed), Hash(xb, ya, seed), fx),
                Mathf.Lerp(Hash(xa, yb, seed), Hash(xb, yb, seed), fx),
                fy);
        }

        private static int Wrap(int value, int period)
        {
            return ((value % period) + period) % period;
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1103515245u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        private static string EnsureFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
            return path;
        }
    }
}
