using System.Collections.Generic;
using UnityEngine;

namespace FullyVolted.EditorTools
{
    // One straight ladder of the tower.
    public readonly struct LadderSection
    {
        // World space, lowest first.
        public readonly Vector3[] Rungs;

        // World space, horizontal: along the rungs, and from the ladder toward the tower centre (onto the platform).
        public readonly Vector3 RungAxis;
        public readonly Vector3 TowardPlatform;
        public readonly float RungHalfLength;

        public Vector3 Bottom => Rungs[0];
        public Vector3 Top => Rungs[Rungs.Length - 1];

        public LadderSection(Vector3[] rungs, Vector3 rungAxis, Vector3 towardPlatform, float rungHalfLength)
        {
            Rungs = rungs;
            RungAxis = rungAxis;
            TowardPlatform = towardPlatform;
            RungHalfLength = rungHalfLength;
        }
    }

    // The training route up the tower: ladder after ladder, from the one that starts lowest to the one that
    // reaches highest. Ladder 1 ends level with the big first platform, which is the middle checkpoint.
    // Everything placed along the route (zones, anchors, switch panel, complete screen) is positioned from
    // this, so moving or swapping the tower doesn't leave them floating somewhere else.
    public readonly struct LadderRoute
    {
        // Two rungs in a row further apart than this horizontally belong to different ladders. Checked rung to
        // rung, not against the bottom rung: the tower tapers, so its long ladders lean in ~5cm with every rung.
        private const float SameLadderTolerance = 0.1f;

        // A vertical gap bigger than this between two rungs in line means two separate ladders.
        private const float MaxRungGap = 1f;

        // The next ladder up starts at most this far above the top of the one below it.
        private const float MaxHandOverHeight = 1.5f;

        // Lowest first.
        public readonly LadderSection[] Sections;

        public LadderSection First => Sections[0];
        public LadderSection Last => Sections[Sections.Length - 1];
        public Vector3 Bottom => First.Bottom;

        // The top rung of ladder 1 is level with the first platform's floor.
        public float MiddlePlatformHeight => First.Top.y;

        private LadderRoute(LadderSection[] sections)
        {
            Sections = sections;
        }

        // rungs are in the climbable mesh's local space, lowest first (as LadderRungFinder returns them).
        public static bool TryFind(Transform climbable, IReadOnlyList<LadderRungFinder.Rung> rungs, Vector3 towerCenter,
            out LadderRoute route)
        {
            route = default;
            var ladders = SplitIntoLadders(climbable, rungs, towerCenter);
            if (ladders.Count == 0)
                return false;

            // Walk down from the ladder that reaches highest: each step picks the nearest ladder that ends
            // just below where the current one starts. Walking up instead could wander onto a ladder that
            // stops short of the top.
            var highest = ladders[0];
            foreach (var ladder in ladders)
            {
                if (ladder.Top.y > highest.Top.y)
                    highest = ladder;
            }

            var chain = new List<LadderSection> { highest };
            while (TryFindLadderBelow(ladders, chain[chain.Count - 1], out var below))
                chain.Add(below);

            chain.Reverse();
            if (chain[0].Bottom.y > LowestRung(ladders) + SameLadderTolerance)
                Debug.LogWarning("[FullyVolted] The route to the top does not start at the lowest ladder; " +
                                 "the climb starts higher than expected.");

            route = new LadderRoute(chain.ToArray());
            return true;
        }

        private static List<LadderSection> SplitIntoLadders(Transform climbable, IReadOnlyList<LadderRungFinder.Rung> rungs,
            Vector3 towerCenter)
        {
            var ladders = new List<LadderSection>();
            var claimed = new bool[rungs.Count];

            for (int start = 0; start < rungs.Count; start++)
            {
                if (claimed[start])
                    continue;

                // Rungs come lowest first, so the first unclaimed one is the bottom of a new ladder.
                var points = new List<Vector3> { climbable.TransformPoint(rungs[start].Center) };
                claimed[start] = true;

                for (int i = start + 1; i < rungs.Count; i++)
                {
                    if (claimed[i])
                        continue;

                    var point = climbable.TransformPoint(rungs[i].Center);
                    var last = points[points.Count - 1];
                    var offset = point - last;
                    offset.y = 0f;
                    if (offset.magnitude >= SameLadderTolerance)
                        continue;

                    if (point.y - last.y > MaxRungGap)
                        break;

                    points.Add(point);
                    claimed[i] = true;
                }

                if (points.Count >= 2)
                    ladders.Add(CreateSection(climbable, rungs[start], points, towerCenter));
            }

            return ladders;
        }

        private static LadderSection CreateSection(Transform climbable, LadderRungFinder.Rung firstRung, List<Vector3> points,
            Vector3 towerCenter)
        {
            var span = climbable.TransformVector(firstRung.Rotation * Vector3.right * firstRung.Size.x);
            var axis = Vector3.ProjectOnPlane(span, Vector3.up).normalized;

            // The ladder sits on the platform's edge, so "onto the platform" is the side facing the tower centre.
            var toward = Vector3.Cross(axis, Vector3.up);
            if (Vector3.Dot(toward, towerCenter - points[0]) < 0f)
                toward = -toward;

            return new LadderSection(points.ToArray(), axis, toward, span.magnitude * 0.5f);
        }

        private static bool TryFindLadderBelow(List<LadderSection> ladders, LadderSection above, out LadderSection below)
        {
            below = default;
            var found = false;
            var nearest = float.MaxValue;

            foreach (var ladder in ladders)
            {
                var drop = above.Bottom.y - ladder.Top.y;
                if (drop <= 0f || drop > MaxHandOverHeight)
                    continue;

                var offset = above.Bottom - ladder.Top;
                offset.y = 0f;
                if (offset.magnitude >= nearest)
                    continue;

                nearest = offset.magnitude;
                below = ladder;
                found = true;
            }

            return found;
        }

        private static float LowestRung(List<LadderSection> ladders)
        {
            var lowest = float.MaxValue;
            foreach (var ladder in ladders)
                lowest = Mathf.Min(lowest, ladder.Bottom.y);

            return lowest;
        }
    }
}
