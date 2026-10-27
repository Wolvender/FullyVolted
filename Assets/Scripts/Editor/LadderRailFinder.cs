using System.Collections.Generic;
using UnityEngine;

namespace FullyVolted.EditorTools
{
    // Finds the side rails of the ladders that LadderRungFinder found. Rungs alone leave the player stuck at
    // the top of every ladder: the last rung is level with the platform floor, so there is nothing higher
    // to pull on. The rails carry on past it and bend over into a handrail, which is what a climber holds.
    //
    // A rail is a thin tube, modelled as rings of vertices only where it bends - one straight 8m stretch
    // has no vertices in between. So the rail is traced as a chain of rings (its centre line), and the
    // chain is cut into short boxes, so only the part near the hand lights up.
    public static class LadderRailFinder
    {
        public readonly struct RailSegment
        {
            // All in the mesh's local space. Rotation turns the box's Z axis along the rail.
            public readonly Vector3 Center;
            public readonly Quaternion Rotation;
            public readonly Vector3 Size;

            public RailSegment(Vector3 center, Quaternion rotation, Vector3 size)
            {
                Center = center;
                Rotation = rotation;
                Size = size;
            }
        }

        // A rail has to rise at least this far; a cage hoop touches rung ends too, but lies flat.
        private const float MinRailHeight = 0.5f;

        // Rails are low-poly tubes (under 100 vertices); skipping big pieces keeps the ring search cheap.
        private const int MaxRailVertices = 400;

        // Vertices this close together belong to the same cross-section ring of the tube.
        private const float RingRadius = 0.05f;

        // A rail runs through the ends of the rungs it holds. Two rungs make it a ladder rail.
        private const float RungEndTolerance = 0.05f;
        private const int MinRungsOnRail = 2;

        // Long straight stretches are cut up so the glow stays local to the hand, like on the rungs.
        private const float MaxSegmentLength = 0.5f;
        private const float MinSegmentLength = 0.02f;

        // Returns every rail segment. worldScale converts mesh units to metres, so the limits above are
        // real-world sizes.
        public static List<RailSegment> FindRails(Mesh mesh, IReadOnlyList<LadderRungFinder.Rung> rungs, float worldScale)
        {
            var rungEnds = GetRungEnds(rungs);
            var segments = new List<RailSegment>();

            foreach (var piece in MeshPieces.Split(mesh))
            {
                if (piece.Points.Count > MaxRailVertices || HeightOf(piece.Points) * worldScale < MinRailHeight)
                    continue;

                if (!TryTraceCenterLine(piece, RingRadius / worldScale, out var centerLine, out var thickness))
                    continue;

                if (CountRungEndsOnLine(centerLine, rungEnds, RungEndTolerance / worldScale) < MinRungsOnRail)
                    continue;

                AddSegments(segments, centerLine, thickness, worldScale);
            }

            return segments;
        }

        private static List<Vector3> GetRungEnds(IReadOnlyList<LadderRungFinder.Rung> rungs)
        {
            var ends = new List<Vector3>(rungs.Count * 2);
            foreach (var rung in rungs)
            {
                var halfLength = rung.Rotation * Vector3.right * (rung.Size.x * 0.5f);
                ends.Add(rung.Center - halfLength);
                ends.Add(rung.Center + halfLength);
            }

            return ends;
        }

        private static float HeightOf(List<Vector3> points)
        {
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            foreach (var p in points)
            {
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }

            return maxY - minY;
        }

        // Groups the vertices into rings, links rings that share a triangle, and accepts the piece only if
        // that forms one unbranched chain - a tube. Its centre line is the ring centres in chain order.
        private static bool TryTraceCenterLine(MeshPieces.Piece piece, float ringRadius, out List<Vector3> centerLine,
            out float thickness)
        {
            centerLine = null;
            thickness = 0f;

            var points = piece.Points;
            var ringOf = new int[points.Count];
            for (int i = 0; i < ringOf.Length; i++)
                ringOf[i] = i;

            for (int a = 0; a < points.Count; a++)
            {
                for (int b = a + 1; b < points.Count; b++)
                {
                    if ((points[a] - points[b]).sqrMagnitude < ringRadius * ringRadius)
                        MeshPieces.Union(ringOf, a, b);
                }
            }

            var neighbours = new Dictionary<int, HashSet<int>>();
            for (int i = 0; i < points.Count; i++)
            {
                var ring = MeshPieces.Find(ringOf, i);
                if (!neighbours.ContainsKey(ring))
                    neighbours[ring] = new HashSet<int>();
            }

            var triangles = piece.Triangles;
            for (int t = 0; t < triangles.Count; t += 3)
            {
                Link(neighbours, MeshPieces.Find(ringOf, triangles[t]), MeshPieces.Find(ringOf, triangles[t + 1]));
                Link(neighbours, MeshPieces.Find(ringOf, triangles[t + 1]), MeshPieces.Find(ringOf, triangles[t + 2]));
                Link(neighbours, MeshPieces.Find(ringOf, triangles[t]), MeshPieces.Find(ringOf, triangles[t + 2]));
            }

            if (!TryOrderChain(neighbours, out var chain))
                return false;

            var ringPoints = new Dictionary<int, List<Vector3>>();
            for (int i = 0; i < points.Count; i++)
            {
                var ring = MeshPieces.Find(ringOf, i);
                if (!ringPoints.TryGetValue(ring, out var list))
                {
                    list = new List<Vector3>();
                    ringPoints[ring] = list;
                }

                list.Add(points[i]);
            }

            centerLine = new List<Vector3>(chain.Count);
            foreach (var ring in chain)
            {
                var center = Vector3.zero;
                foreach (var p in ringPoints[ring])
                    center += p;
                center /= ringPoints[ring].Count;
                centerLine.Add(center);

                foreach (var p in ringPoints[ring])
                    thickness = Mathf.Max(thickness, (p - center).magnitude * 2f);
            }

            return true;
        }

        private static void Link(Dictionary<int, HashSet<int>> neighbours, int a, int b)
        {
            if (a == b)
                return;

            neighbours[a].Add(b);
            neighbours[b].Add(a);
        }

        // Walks from one end of the chain to the other. Fails on branches, loops or a single ring.
        private static bool TryOrderChain(Dictionary<int, HashSet<int>> neighbours, out List<int> chain)
        {
            chain = null;
            var start = -1;
            var endCount = 0;
            foreach (var pair in neighbours)
            {
                if (pair.Value.Count > 2)
                    return false;

                if (pair.Value.Count == 1)
                {
                    start = pair.Key;
                    endCount++;
                }
            }

            if (endCount != 2)
                return false;

            chain = new List<int> { start };
            var previous = -1;
            var current = start;
            while (true)
            {
                var next = -1;
                foreach (var neighbour in neighbours[current])
                {
                    if (neighbour != previous)
                        next = neighbour;
                }

                if (next == -1)
                    break;

                chain.Add(next);
                previous = current;
                current = next;
            }

            return chain.Count == neighbours.Count;
        }

        private static int CountRungEndsOnLine(List<Vector3> line, List<Vector3> rungEnds, float tolerance)
        {
            var count = 0;
            foreach (var end in rungEnds)
            {
                for (int i = 0; i < line.Count - 1; i++)
                {
                    if (DistanceToSegment(end, line[i], line[i + 1]) <= tolerance)
                    {
                        count++;
                        break;
                    }
                }
            }

            return count;
        }

        private static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            var t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(point, a + ab * t);
        }

        private static void AddSegments(List<RailSegment> segments, List<Vector3> line, float thickness, float worldScale)
        {
            var maxLength = MaxSegmentLength / worldScale;
            var minLength = MinSegmentLength / worldScale;

            for (int i = 0; i < line.Count - 1; i++)
            {
                var from = line[i];
                var span = line[i + 1] - from;
                var length = span.magnitude;
                if (length < minLength)
                    continue;

                // Not LookRotation: most rails point straight up, parallel to its default up vector.
                var rotation = Quaternion.FromToRotation(Vector3.forward, span / length);
                var count = Mathf.CeilToInt(length / maxLength);
                var step = length / count;

                for (int s = 0; s < count; s++)
                {
                    var center = from + span * ((s + 0.5f) / count);
                    segments.Add(new RailSegment(center, rotation, new Vector3(thickness, thickness, step)));
                }
            }
        }
    }
}
