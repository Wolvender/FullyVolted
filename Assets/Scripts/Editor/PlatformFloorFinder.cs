using UnityEngine;

namespace FullyVolted.EditorTools
{
    // Finds the floor under a point by looking for the highest flat, upward-facing triangle of a mesh below it.
    // Needed for the top deck: the last ladder comes up through a hatch and its top rung sits ~1.4m above the
    // deck, so unlike the first platform the floor height can't be read off the ladder.
    public static class PlatformFloorFinder
    {
        // Normals steeper than this are walls or slopes, not floor.
        private const float MinFloorNormalY = 0.95f;

        public static bool TryFindFloorBelow(Mesh mesh, Transform meshTransform, Vector3 worldPoint, float maxDrop,
            out float floorHeight)
        {
            floorHeight = float.MinValue;
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var toWorld = meshTransform.localToWorldMatrix;
            var lowestAllowed = worldPoint.y - maxDrop;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = toWorld.MultiplyPoint3x4(vertices[triangles[i]]);
                var b = toWorld.MultiplyPoint3x4(vertices[triangles[i + 1]]);
                var c = toWorld.MultiplyPoint3x4(vertices[triangles[i + 2]]);

                var normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-12f || Mathf.Abs(normal.normalized.y) < MinFloorNormalY)
                    continue;

                var height = (a.y + b.y + c.y) / 3f;
                if (height > worldPoint.y || height < lowestAllowed || height <= floorHeight)
                    continue;

                if (ContainsXZ(a, b, c, worldPoint))
                    floorHeight = height;
            }

            return floorHeight > float.MinValue;
        }

        private static bool ContainsXZ(Vector3 a, Vector3 b, Vector3 c, Vector3 p)
        {
            var d1 = SignXZ(p, a, b);
            var d2 = SignXZ(p, b, c);
            var d3 = SignXZ(p, c, a);
            var hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            var hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }

        private static float SignXZ(Vector3 p, Vector3 a, Vector3 b)
        {
            return (p.x - b.x) * (a.z - b.z) - (a.x - b.x) * (p.z - b.z);
        }
    }
}
