using UnityEngine;

namespace FullyVolted.EditorTools
{
    // What the town builder and the forest scatterer must agree on: where the streets are, where the forest begins,
    // and the one road that leads out through the trees. All distances are from the middle of the tower.
    internal static class TownLayout
    {
        public const float RoadInner = 20.5f;
        public const float RoadOuter = 26.5f;
        public const float RoadMiddle = (RoadInner + RoadOuter) / 2f;

        // The cross streets stop here, just short of the trees. Past TownEdge (measured along either axis, so the
        // town is a square) the forest begins.
        public const float StreetEnd = 64f;
        public const float TownEdge = 66f;

        // The exit road is the south cross street on the east side of the tower, carried on through the forest.
        public const float ExitRoadHalfWidth = 3f;
        public const float ExitRoadShoulder = 1.2f;
        public const float ExitRoadLength = 250f;

        // After SwayStart the road bends into a long S-curve instead of running dead straight.
        private const float SwayStart = 80f;
        private const float SwayRamp = 90f;
        private const float SwayAmplitude = 26f;
        private const float SwayWavelength = 38f;

        // The road's centre line at `distance` metres south of the tower's middle. `center` is (x, z).
        public static Vector2 ExitPoint(Vector2 center, float distance)
        {
            var ramp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SwayStart, SwayStart + SwayRamp, distance));
            var lateral = SwayAmplitude * ramp * Mathf.Sin((distance - SwayStart) / SwayWavelength);
            return new Vector2(center.x + RoadMiddle + lateral, center.y - distance);
        }

        // Points along the road's centre line, for measuring how far something is from it.
        public static Vector2[] ExitRoute(Vector2 center, float spacing = 3f)
        {
            var count = Mathf.CeilToInt((ExitRoadLength - StreetEnd + 12f) / spacing) + 1;
            var route = new Vector2[count];
            for (int i = 0; i < count; i++)
                route[i] = ExitPoint(center, StreetEnd - 12f + i * spacing);
            return route;
        }

        public static float DistanceToRoute(Vector2[] route, Vector2 point)
        {
            var best = float.MaxValue;
            foreach (var routePoint in route)
                best = Mathf.Min(best, (routePoint - point).sqrMagnitude);
            return Mathf.Sqrt(best);
        }
    }
}
