using System.Collections.Generic;
using UnityEngine;

namespace FullyVolted.EditorTools
{
    // Splits one combined mesh into its loose, disconnected pieces. The tower pack ships each tower's frame
    // as a single mesh, so a single rung or rail only exists as one such piece inside it.
    public static class MeshPieces
    {
        public readonly struct Piece
        {
            // Mesh-local positions. Triangles index into Points, three per triangle.
            public readonly List<Vector3> Points;
            public readonly List<int> Triangles;

            public Piece(List<Vector3> points, List<int> triangles)
            {
                Points = points;
                Triangles = triangles;
            }
        }

        // Rounds positions to 1/10000 of a mesh unit when merging split vertices.
        private const float WeldPrecision = 10000f;

        // Groups the mesh's vertices into connected pieces (union-find over the triangles).
        public static List<Piece> Split(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;

            var welded = Weld(vertices);
            var parent = new int[vertices.Length];
            for (int i = 0; i < parent.Length; i++)
                parent[i] = i;

            for (int t = 0; t < triangles.Length; t += 3)
            {
                Union(parent, welded[triangles[t]], welded[triangles[t + 1]]);
                Union(parent, welded[triangles[t]], welded[triangles[t + 2]]);
            }

            var pieceOfRoot = new Dictionary<int, Piece>();
            var localIndex = new int[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                var root = Find(parent, welded[i]);
                if (!pieceOfRoot.TryGetValue(root, out var piece))
                {
                    piece = new Piece(new List<Vector3>(), new List<int>());
                    pieceOfRoot[root] = piece;
                }

                localIndex[i] = piece.Points.Count;
                piece.Points.Add(vertices[i]);
            }

            for (int t = 0; t < triangles.Length; t += 3)
            {
                var piece = pieceOfRoot[Find(parent, welded[triangles[t]])];
                piece.Triangles.Add(localIndex[triangles[t]]);
                piece.Triangles.Add(localIndex[triangles[t + 1]]);
                piece.Triangles.Add(localIndex[triangles[t + 2]]);
            }

            return new List<Piece>(pieceOfRoot.Values);
        }

        // Hard edges and UV seams store the same corner several times; without merging them a single
        // rung would count as many separate pieces.
        private static int[] Weld(Vector3[] vertices)
        {
            var firstAtPosition = new Dictionary<Vector3Int, int>();
            var welded = new int[vertices.Length];

            for (int i = 0; i < vertices.Length; i++)
            {
                var key = Vector3Int.RoundToInt(vertices[i] * WeldPrecision);
                if (!firstAtPosition.TryGetValue(key, out var first))
                {
                    first = i;
                    firstAtPosition[key] = i;
                }

                welded[i] = first;
            }

            return welded;
        }

        public static int Find(int[] parent, int index)
        {
            while (parent[index] != index)
            {
                parent[index] = parent[parent[index]];
                index = parent[index];
            }

            return index;
        }

        public static void Union(int[] parent, int a, int b)
        {
            var rootA = Find(parent, a);
            var rootB = Find(parent, b);
            if (rootA != rootB)
                parent[rootB] = rootA;
        }
    }
}
