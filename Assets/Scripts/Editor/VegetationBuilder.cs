using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FullyVolted.EditorTools
{
    // Trees and bushes for the terrain, made entirely in code: low-poly, flat-shaded models (pine, oak, birch,
    // bush) saved as prefabs, then scattered with Unity's own terrain tree system, which draws them instanced.
    // A dense forest closes in right at the edge of the town (a square round the tower, see TownLayout), thins out
    // toward the horizon, and leaves a clear corridor for the one road that leads out of the town.
    internal static class VegetationBuilder
    {
        private const int MaxTrees = 11000;
        private const int MaxBushes = 3500;
        private const int Attempts = 400000;

        // Where trees may stand: inside this radius of the world origin (the terrain's rim hides the rest).
        private const float OuterRadius = 340f;
        private const float MaxSlopeDegrees = 30f;

        // The forest fades in over this many metres past the town's edge, and thins to a third of its density
        // between FullDensityUntil and the horizon.
        private const float EdgeFade = 8f;
        private const float FullDensityUntil = 120f;
        private const float FarDensity = 0.3f;

        // Kept clear of trees on either side of the road's centre line (asphalt, shoulder, a verge), then fading in.
        private const float RoadClearance = TownLayout.ExitRoadHalfWidth + TownLayout.ExitRoadShoulder + 2f;
        private const float RoadFade = 5f;

        private const float TreeDistance = 240f;
        private const float BillboardDistance = 120f;

        private enum Kind { Pine = 0, Oak = 1, Birch = 2, Bush = 3 }

        public static void Create(Terrain terrain, string folder)
        {
            var vegetationFolder = $"{folder}/Vegetation";
            if (!AssetDatabase.IsValidFolder(vegetationFolder))
                AssetDatabase.CreateFolder(folder, "Vegetation");

            var bark = GetMaterial(vegetationFolder, "Bark", new Color(0.30f, 0.21f, 0.14f));
            var birchBark = GetMaterial(vegetationFolder, "Birch Bark", new Color(0.82f, 0.81f, 0.76f));
            var pineLeaf = GetMaterial(vegetationFolder, "Pine Needles", new Color(0.10f, 0.27f, 0.15f));
            var oakLeaf = GetMaterial(vegetationFolder, "Oak Leaves", new Color(0.27f, 0.44f, 0.13f));
            var birchLeaf = GetMaterial(vegetationFolder, "Birch Leaves", new Color(0.46f, 0.55f, 0.16f));
            var bushLeaf = GetMaterial(vegetationFolder, "Bush Leaves", new Color(0.22f, 0.38f, 0.14f));

            var prototypes = new[]
            {
                new TreePrototype { prefab = SavePrefab(vegetationFolder, "Pine", MakePine(), bark, pineLeaf, true) },
                new TreePrototype { prefab = SavePrefab(vegetationFolder, "Oak", MakeOak(), bark, oakLeaf, true) },
                new TreePrototype { prefab = SavePrefab(vegetationFolder, "Birch", MakeBirch(), birchBark, birchLeaf, true) },
                new TreePrototype { prefab = SavePrefab(vegetationFolder, "Bush", MakeBush(), bark, bushLeaf, false) },
            };

            var data = terrain.terrainData;
            data.treePrototypes = prototypes;
            data.SetTreeInstances(Scatter(data), true);
            EditorUtility.SetDirty(data);

            terrain.treeDistance = TreeDistance;
            terrain.treeBillboardDistance = BillboardDistance;
        }

        // ---- placement ----

        private static TreeInstance[] Scatter(TerrainData data)
        {
            var random = new System.Random(20261027);
            var instances = new List<TreeInstance>();
            int trees = 0, bushes = 0;
            var size = data.size.x;

            var tower = ClimbSceneBuilder.TowerPosition;
            var towerXZ = new Vector2(tower.x, tower.z);
            var route = TownLayout.ExitRoute(towerXZ);

            for (int attempt = 0; attempt < Attempts && (trees < MaxTrees || bushes < MaxBushes); attempt++)
            {
                // Uniform over the terrain, kept inside the rim; the density below does the shaping.
                var x = (Next(random) * 2f - 1f) * OuterRadius;
                var z = (Next(random) * 2f - 1f) * OuterRadius;
                if (x * x + z * z > OuterRadius * OuterRadius)
                    continue;

                var isBush = Next(random) < 0.28f;
                if (isBush ? bushes >= MaxBushes : trees >= MaxTrees)
                    continue;

                var nx = x / size + 0.5f;
                var nz = z / size + 0.5f;
                var density = ForestDensity(x, z, towerXZ, route, Mathf.PerlinNoise(nx * 9f + 3.1f, nz * 9f + 8.4f));
                if (Next(random) > (isBush ? density * 0.7f : density))
                    continue;

                if (data.GetSteepness(nx, nz) > MaxSlopeDegrees)
                    continue;

                var worldHeight = data.GetInterpolatedHeight(nx, nz) - TerrainGenerator.FlatHeight;
                var kind = isBush ? Kind.Bush : PickTree(random, worldHeight);

                var scale = kind == Kind.Bush ? Mathf.Lerp(0.7f, 1.4f, Next(random)) : Mathf.Lerp(0.75f, 1.35f, Next(random));
                instances.Add(new TreeInstance
                {
                    position = new Vector3(nx, 0f, nz),
                    prototypeIndex = (int)kind,
                    heightScale = scale,
                    widthScale = scale * Mathf.Lerp(0.9f, 1.1f, Next(random)),
                    rotation = Next(random) * Mathf.PI * 2f,
                    // Both default to transparent black, which draws the tree black.
                    color = Color.white,
                    lightmapColor = Color.white
                });

                if (isBush) bushes++; else trees++;
            }

            Debug.Log($"[FullyVolted] Vegetation: {trees} trees and {bushes} bushes.");
            return instances.ToArray();
        }

        // 0 = nothing grows here, 1 = as thick as it gets.
        private static float ForestDensity(float x, float z, Vector2 tower, Vector2[] route, float noise)
        {
            // The town is a square, so measure along the axes, not as a circle.
            var fromTown = Mathf.Max(Mathf.Abs(x - tower.x), Mathf.Abs(z - tower.y));
            var outsideTown = Smooth(TownLayout.TownEdge, TownLayout.TownEdge + EdgeFade, fromTown);
            if (outsideTown <= 0f)
                return 0f;

            var offRoad = Smooth(RoadClearance, RoadClearance + RoadFade, TownLayout.DistanceToRoute(route, new Vector2(x, z)));
            var thinning = Mathf.Lerp(1f, FarDensity, Smooth(FullDensityUntil, OuterRadius, fromTown));

            // A little unevenness so it is not a perfect carpet, but no meadows: this is a thick forest.
            return outsideTown * offRoad * thinning * Mathf.Lerp(0.75f, 1f, noise);
        }

        // Mostly conifers and oaks with some birch, and only pine on the high ground.
        private static Kind PickTree(System.Random random, float worldHeight)
        {
            var roll = Next(random);
            if (worldHeight > 18f)
                return roll < 0.7f ? Kind.Pine : Kind.Oak;

            if (roll < 0.40f) return Kind.Pine;
            return roll < 0.78f ? Kind.Oak : Kind.Birch;
        }

        private static float Next(System.Random random)
        {
            return (float)random.NextDouble();
        }

        private static float Smooth(float from, float to, float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));
        }

        // ---- models (submesh 0 = trunk, submesh 1 = leaves) ----

        private static Mesh MakePine()
        {
            var random = new System.Random(1);
            var mesh = new MeshBuilder();
            mesh.Frustum(0, 6, 0.28f, 0.2f, 2.2f, Vector3.zero, 0f);

            for (int layer = 0; layer < 4; layer++)
            {
                var baseRadius = 2.3f - layer * 0.45f;
                mesh.Cone(1, 8, baseRadius, 3.2f, new Vector3(0f, 1.6f + layer * 2f, 0f), layer * 0.4f, random, 0.15f);
            }

            return mesh.ToMesh("Pine");
        }

        private static Mesh MakeOak()
        {
            var random = new System.Random(2);
            var mesh = new MeshBuilder();
            mesh.Frustum(0, 6, 0.35f, 0.22f, 2.6f, Vector3.zero, 0f);
            mesh.Blob(1, new Vector3(0f, 4.2f, 0f), new Vector3(2.6f, 2.2f, 2.6f), random);
            mesh.Blob(1, new Vector3(1.3f, 3.7f, 0.6f), new Vector3(1.8f, 1.5f, 1.8f), random);
            mesh.Blob(1, new Vector3(-1.2f, 3.9f, -0.8f), new Vector3(1.7f, 1.5f, 1.7f), random);
            return mesh.ToMesh("Oak");
        }

        private static Mesh MakeBirch()
        {
            var random = new System.Random(3);
            var mesh = new MeshBuilder();
            mesh.Frustum(0, 5, 0.18f, 0.12f, 4.4f, Vector3.zero, 0f);
            mesh.Blob(1, new Vector3(0f, 5.5f, 0f), new Vector3(1.4f, 2.0f, 1.4f), random);
            mesh.Blob(1, new Vector3(0.5f, 4.4f, 0.3f), new Vector3(1.1f, 1.4f, 1.1f), random);
            return mesh.ToMesh("Birch");
        }

        private static Mesh MakeBush()
        {
            var random = new System.Random(4);
            var mesh = new MeshBuilder();
            mesh.Blob(1, new Vector3(0f, 0.55f, 0f), new Vector3(0.9f, 0.65f, 0.9f), random);
            mesh.Blob(1, new Vector3(0.6f, 0.4f, 0.3f), new Vector3(0.6f, 0.45f, 0.6f), random);
            mesh.Blob(1, new Vector3(-0.5f, 0.45f, -0.3f), new Vector3(0.55f, 0.45f, 0.55f), random);
            return mesh.ToMesh("Bush");
        }

        // ---- assets ----

        private static GameObject SavePrefab(string folder, string prefabName, Mesh mesh, Material trunk, Material leaves, bool castShadows)
        {
            var meshPath = $"{folder}/{prefabName}.asset";
            var prefabPath = $"{folder}/{prefabName}.prefab";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.DeleteAsset(prefabPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var model = new GameObject(prefabName);
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = model.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { trunk, leaves };
            // Hundreds of small bushes throwing shadows would cost far more than they add.
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

            var prefab = PrefabUtility.SaveAsPrefabAsset(model, prefabPath);
            Object.DestroyImmediate(model);
            return prefab;
        }

        // Reused when it exists, so a colour tweaked in the editor survives a rebuild.
        private static Material GetMaterial(string folder, string materialName, Color color)
        {
            var path = $"{folder}/{materialName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.05f);
            material.SetFloat("_Metallic", 0f);
            // Terrain trees are drawn in batches of identical instances.
            material.enableInstancing = true;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // Flat-shaded triangles: every triangle gets its own vertices, so RecalculateNormals gives hard facets.
        // Winding is clockwise seen from outside, which is Unity's front face.
        private sealed class MeshBuilder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int>[] triangles = { new List<int>(), new List<int>() };

            public void Triangle(int submesh, Vector3 a, Vector3 b, Vector3 c)
            {
                var first = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                triangles[submesh].Add(first);
                triangles[submesh].Add(first + 1);
                triangles[submesh].Add(first + 2);
            }

            private void Quad(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Triangle(submesh, a, b, c);
                Triangle(submesh, a, c, d);
            }

            // A tapered column standing on `origin`.
            public void Frustum(int submesh, int sides, float bottomRadius, float topRadius, float height, Vector3 origin, float twist)
            {
                for (int i = 0; i < sides; i++)
                {
                    var angle0 = twist + i * Mathf.PI * 2f / sides;
                    var angle1 = twist + (i + 1) * Mathf.PI * 2f / sides;
                    Quad(submesh,
                        origin + Ring(angle0, bottomRadius),
                        origin + Ring(angle0, topRadius) + Vector3.up * height,
                        origin + Ring(angle1, topRadius) + Vector3.up * height,
                        origin + Ring(angle1, bottomRadius));
                }
            }

            // A cone standing on `origin`, with the base ring's vertices nudged so no two layers look the same.
            public void Cone(int submesh, int sides, float radius, float height, Vector3 origin, float twist, System.Random random, float jitter)
            {
                var tip = origin + Vector3.up * height;
                var rim = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    var wobble = 1f + (((float)random.NextDouble() * 2f) - 1f) * jitter;
                    rim[i] = origin + Ring(twist + i * Mathf.PI * 2f / sides, radius * wobble);
                }

                for (int i = 0; i < sides; i++)
                    Triangle(submesh, rim[i], tip, rim[(i + 1) % sides]);
            }

            // A lumpy ellipsoid: latitude rings of vertices pushed in and out at random.
            public void Blob(int submesh, Vector3 center, Vector3 radii, System.Random random)
            {
                const int rings = 4;
                const int longitudes = 7;

                // Ring 0 and `rings` are the poles; the shared jitter wraps around so the seam closes.
                var points = new Vector3[rings + 1, longitudes];
                for (int ring = 0; ring <= rings; ring++)
                {
                    var phi = Mathf.PI * ring / rings;
                    for (int lon = 0; lon < longitudes; lon++)
                    {
                        var theta = lon * Mathf.PI * 2f / longitudes;
                        var lump = ring == 0 || ring == rings ? 1f : Mathf.Lerp(0.82f, 1.15f, (float)random.NextDouble());
                        var direction = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                        points[ring, lon] = center + Vector3.Scale(direction, radii) * lump;
                    }
                }

                for (int lon = 0; lon < longitudes; lon++)
                {
                    var next = (lon + 1) % longitudes;

                    Triangle(submesh, points[1, lon], points[0, lon], points[1, next]);
                    for (int ring = 1; ring < rings - 1; ring++)
                        Quad(submesh, points[ring + 1, lon], points[ring, lon], points[ring, next], points[ring + 1, next]);
                    Triangle(submesh, points[rings - 1, lon], points[rings - 1, next], points[rings, lon]);
                }
            }

            public Mesh ToMesh(string meshName)
            {
                var mesh = new Mesh { name = meshName, subMeshCount = 2 };
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles[0], 0);
                mesh.SetTriangles(triangles[1], 1);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }

            private static Vector3 Ring(float angle, float radius)
            {
                return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }
        }
    }
}
