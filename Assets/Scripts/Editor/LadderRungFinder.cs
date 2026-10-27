using System.Collections.Generic;
using UnityEngine;

namespace FullyVolted.EditorTools
{
    // Finds ladder rungs inside one combined mesh (see MeshPieces for why they are loose pieces). A rung
    // is a flat, horizontal piece roughly half a metre long and only a few centimetres wide - in ANY
    // horizontal direction: the caged ladder above the first platform has its rungs turned 45 degrees.
    public static class LadderRungFinder
    {
        public readonly struct Rung
        {
            // All in the mesh's local space. Rotation turns the box's X axis along the rung.
            public readonly Vector3 Center;
            public readonly Quaternion Rotation;
            public readonly Vector3 Size;

            public Rung(Vector3 center, Quaternion rotation, Vector3 size)
            {
                Center = center;
                Rotation = rotation;
                Size = size;
            }
        }

        private const float MinRungLength = 0.4f;
        private const float MaxRungLength = 0.6f;
        private const float MaxRungThickness = 0.05f;

        // Where ladder sections overlap, the model has two rungs in the same spot.
        private const float DuplicateDistance = 0.05f;

        // Returns every rung, lowest first. worldScale converts mesh units to metres, so the size
        // limits above are real-world sizes.
        public static List<Rung> FindRungs(Mesh mesh, float worldScale)
        {
            var rungs = new List<Rung>();
            var duplicateDistance = DuplicateDistance / worldScale;

            foreach (var piece in MeshPieces.Split(mesh))
            {
                if (TryMeasureRung(piece.Points, worldScale, out var rung) && !IsDuplicate(rungs, rung.Center, duplicateDistance))
                    rungs.Add(rung);
            }

            rungs.Sort((a, b) => a.Center.y.CompareTo(b.Center.y));
            return rungs;
        }

        // Measures the piece along its own long axis instead of the world axes. An axis-aligned box
        // around a 45-degree rung is a 0.41 x 0.41 square, which no length/width check would accept.
        private static bool TryMeasureRung(List<Vector3> points, float worldScale, out Rung rung)
        {
            rung = default;

            var minY = float.MaxValue;
            var maxY = float.MinValue;
            var mean = Vector2.zero;
            foreach (var p in points)
            {
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
                mean += new Vector2(p.x, p.z);
            }

            mean /= points.Count;
            if ((maxY - minY) * worldScale > MaxRungThickness)
                return false;

            // Direction of the piece's longest horizontal spread (principal axis of the XZ points).
            float xx = 0f, zz = 0f, xz = 0f;
            foreach (var p in points)
            {
                var dx = p.x - mean.x;
                var dz = p.z - mean.y;
                xx += dx * dx;
                zz += dz * dz;
                xz += dx * dz;
            }

            var angle = 0.5f * Mathf.Atan2(2f * xz, xx - zz);
            var major = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var minor = new Vector2(-major.y, major.x);

            float minA = float.MaxValue, maxA = float.MinValue, minB = float.MaxValue, maxB = float.MinValue;
            foreach (var p in points)
            {
                var offset = new Vector2(p.x, p.z) - mean;
                var a = Vector2.Dot(offset, major);
                var b = Vector2.Dot(offset, minor);
                minA = Mathf.Min(minA, a);
                maxA = Mathf.Max(maxA, a);
                minB = Mathf.Min(minB, b);
                maxB = Mathf.Max(maxB, b);
            }

            var length = maxA - minA;
            var width = maxB - minB;
            if (length * worldScale < MinRungLength || length * worldScale > MaxRungLength ||
                width * worldScale > MaxRungThickness)
                return false;

            var centerXZ = mean + major * ((minA + maxA) * 0.5f) + minor * ((minB + maxB) * 0.5f);
            var center = new Vector3(centerXZ.x, (minY + maxY) * 0.5f, centerXZ.y);

            // A Y rotation of t degrees points local X at (cos t, 0, -sin t), so t = atan2(-z, x).
            var yaw = Mathf.Atan2(-major.y, major.x) * Mathf.Rad2Deg;

            rung = new Rung(center, Quaternion.Euler(0f, yaw, 0f), new Vector3(length, maxY - minY, width));
            return true;
        }

        private static bool IsDuplicate(List<Rung> rungs, Vector3 center, float distance)
        {
            for (int i = 0; i < rungs.Count; i++)
            {
                if ((rungs[i].Center - center).sqrMagnitude < distance * distance)
                    return true;
            }

            return false;
        }
    }
}
