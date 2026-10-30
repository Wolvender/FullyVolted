using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FullyVolted.EditorTools
{
    // A town block around the tower, for looks only, laid out like a city grid: the tower stands on a square plaza
    // inside a low wall, a square street runs round the plaza, four cross streets run out and simply stop, and the
    // buildings stand in rows along the plaza's four sides and along the cross streets, facing the street.
    // Everything is generated in code. The buildings are merged into a few big meshes (few draw calls) saved in
    // Assets/Landscape/Town, with one invisible box collider per solid building.
    internal static class TownBuilder
    {
        // Distances from the middle of the tower, measured along the axes (a square, not a circle).
        private const float PlazaHalf = 19f;
        private const float RoadInner = TownLayout.RoadInner;
        private const float RoadOuter = TownLayout.RoadOuter;
        private const float WalkOuter = 28f;
        // The cross streets run to the edge of the forest and stop; one of them carries on as the exit road.
        private const float StreetEnd = TownLayout.StreetEnd;
        private const float RoadMiddle = TownLayout.RoadMiddle;

        private const float FrontOffset = 3.5f;
        private const float RowGapMinimum = 0.6f;
        private const float CornerRowGap = 1.0f;
        private const float CornerStart = WalkOuter + 2f;
        private const float MaxPadHalf = 11f;
        private const float WallGapHalf = 2.5f;

        private const float AsphaltY = 0.03f;
        private const float SidewalkY = 0.05f;
        private const float PadY = 0.06f;

        private enum Kind { House, Apartment, Barn, Substation, WaterTower }

        private readonly struct Entry
        {
            public readonly Kind Kind;
            public readonly float Width, Depth;
            // Which house style (colours, porch, garage...); ignored for the other kinds.
            public readonly int Variant;

            public Entry(Kind kind, float width, float depth, int variant = 0)
            {
                Kind = kind;
                Width = width;
                Depth = depth;
                Variant = variant;
            }
        }

        private readonly struct Placement
        {
            public readonly Entry Entry;
            public readonly TownBuildings.HouseSpec Spec;
            public readonly Vector3 Position;
            public readonly float Yaw;

            public Placement(Entry entry, TownBuildings.HouseSpec spec, Vector3 position, float yaw)
            {
                Entry = entry;
                Spec = spec;
                Position = position;
                Yaw = yaw;
            }
        }

        // One row per side of the plaza, listed left to right as seen from the plaza. Big buildings are mixed with
        // houses so no two heavy ones stand together.
        private static readonly (Vector3 outward, Entry[] row)[] Sides =
        {
            (Vector3.forward, new[] { new Entry(Kind.Apartment, 13f, 8.5f), new Entry(Kind.House, 8f, 6.5f, 0), new Entry(Kind.House, 9f, 6f, 2) }),
            (Vector3.back, new[] { new Entry(Kind.House, 7f, 8f, 1), new Entry(Kind.Barn, 14f, 9f), new Entry(Kind.House, 6f, 5.5f, 3) }),
            (Vector3.right, new[] { new Entry(Kind.Substation, 16f, 11f), new Entry(Kind.House, 8.5f, 6.5f, 4) }),
            (Vector3.left, new[] { new Entry(Kind.WaterTower, 8f, 8f), new Entry(Kind.House, 7.5f, 6f, 5) }),
        };

        // The four corner blocks between the cross streets. Row A faces the street running east-west (it starts at the
        // plaza's side and runs outward); row B faces the street running north-south, further out so the two never meet.
        private static readonly (float sx, float sz, Entry[] rowA, Entry[] rowB)[] Corners =
        {
            (1f, 1f, new[] { new Entry(Kind.House, 8f, 6.5f, 6), new Entry(Kind.House, 7f, 7f, 7) }, new[] { new Entry(Kind.Apartment, 11f, 7.5f) }),
            (-1f, 1f, new[] { new Entry(Kind.Apartment, 12f, 8f), new Entry(Kind.House, 8f, 6.5f, 3) }, new[] { new Entry(Kind.House, 7f, 6f, 5) }),
            (1f, -1f, new[] { new Entry(Kind.House, 9f, 6.5f, 2), new Entry(Kind.House, 7f, 6f, 4) }, new[] { new Entry(Kind.House, 7.5f, 7f, 1) }),
            (-1f, -1f, new[] { new Entry(Kind.House, 8f, 6f, 7), new Entry(Kind.Apartment, 11f, 7.5f) }, new[] { new Entry(Kind.House, 8f, 6.5f, 6) }),
        };

        public static void Create(Transform tower, string folder)
        {
            var townFolder = $"{folder}/Town";
            if (!AssetDatabase.IsValidFolder(townFolder))
                AssetDatabase.CreateFolder(folder, "Town");

            var palette = new TownPalette(townFolder);
            var random = new System.Random(20261030);

            var bounds = TowerBounds(tower);
            var center = new Vector3(bounds.center.x, 0f, bounds.center.z);
            var pad = new Vector2(Mathf.Min(bounds.extents.x, MaxPadHalf) + 1.5f, Mathf.Min(bounds.extents.z, MaxPadHalf) + 1.5f);
            // Wide enough that the spawn point at the ladder's foot, the helmet bench and the complete screen all
            // stand inside it.
            var wallHalf = Mathf.Clamp(Mathf.Max(pad.x, pad.y) + 2.5f, 7f, 15f);

            var root = new GameObject("Town").transform;

            var placements = new List<Placement>();
            foreach (var (outward, row) in Sides)
                PlaceRow(placements, palette, row, center, outward, null, 0f, true);
            foreach (var (sx, sz, rowA, rowB) in Corners)
            {
                PlaceRow(placements, palette, rowA, center, new Vector3(0f, 0f, sz), new Vector3(sx, 0f, 0f), CornerStart, false);
                var deepest = 0f;
                foreach (var entry in rowA)
                    deepest = Mathf.Max(deepest, entry.Depth);
                PlaceRow(placements, palette, rowB, center, new Vector3(sx, 0f, 0f), new Vector3(0f, 0f, sz),
                    WalkOuter + FrontOffset + deepest + 2f, false);
            }

            // All buildings go into two meshes: the solid parts (casting shadows) and the yards (paths, fences, cars).
            var buildings = new TownMesh();
            var yards = new TownMesh();
            var colliders = 0;
            foreach (var placement in placements)
            {
                var body = new TownMesh();
                var yard = new TownMesh();
                var solid = Build(placement.Entry, placement.Spec, body, yard, palette, random);

                buildings.Append(body, placement.Position, placement.Yaw);
                yards.Append(yard, placement.Position, placement.Yaw);
                if (solid)
                {
                    AddCollider(root, $"Town {placement.Entry.Kind} Collider", placement.Position, placement.Yaw, body.LocalBounds());
                    colliders++;
                }
            }

            var triangles = 0;
            triangles += AddPiece(root, "Town Ground", BuildGround(palette, center, pad), true, false, townFolder);
            triangles += AddPiece(root, "Town Exit Road", BuildExitRoad(palette, center), false, false, townFolder);
            triangles += AddPiece(root, "Town Props", BuildProps(palette, center), true, false, townFolder);
            triangles += AddPiece(root, "Town Wall", BuildWall(palette, center, wallHalf), true, true, townFolder);
            triangles += AddPiece(root, "Town Buildings", buildings, true, true, townFolder);
            triangles += AddPiece(root, "Town Yards", yards, true, false, townFolder);

            Debug.Log($"[FullyVolted] Town: {placements.Count} buildings round a {PlazaHalf * 2f:0}m plaza at ({center.x:0.0}, {center.z:0.0}), " +
                      $"tower wall {wallHalf * 2f:0.0}m, {colliders} colliders, about {triangles} triangles.");
        }

        // ---- laying out rows ----

        // Places a row of buildings that all face the way `outward` points back at (toward the plaza / the street).
        // `dir` is the direction the row runs in (null: along the row's own right, used for the plaza sides), and
        // `start` is where the row's first building begins along it. A centered row is spread evenly across the plaza.
        private static void PlaceRow(List<Placement> placements, TownPalette palette, Entry[] row, Vector3 center, Vector3 outward,
            Vector3? dir, float start, bool centered)
        {
            // Local +Z is the front, and the front looks back at the plaza or the street.
            var forward = -outward;
            var yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            var along = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            var direction = dir ?? along;
            // A building's local X runs with the row, or against it.
            var sign = Vector3.Dot(along, direction) >= 0f ? 1f : -1f;

            var specs = new TownBuildings.HouseSpec[row.Length];
            var reach = new (float min, float max)[row.Length];
            var total = 0f;
            for (int i = 0; i < row.Length; i++)
            {
                specs[i] = SpecFor(row[i], palette);
                var local = ExtentOf(row[i], specs[i]);
                reach[i] = sign > 0f ? local : (-local.max, -local.min);
                total += reach[i].max - reach[i].min;
            }

            var gap = centered ? Mathf.Clamp((PlazaHalf * 2f - total) / (row.Length + 1), RowGapMinimum, 6f) : CornerRowGap;
            var cursor = centered ? -(total + gap * (row.Length - 1)) / 2f : start;

            for (int i = 0; i < row.Length; i++)
            {
                var distance = WalkOuter + FrontOffset + row[i].Depth / 2f;
                var position = center + outward * distance + direction * (cursor - reach[i].min);
                cursor += reach[i].max - reach[i].min + gap;
                placements.Add(new Placement(row[i], specs[i], position, yaw));
            }
        }

        // ---- what each building looks like ----

        // House styles, picked by `variant` (wrapping round): colours, floors, and what's on the front yard.
        private static TownBuildings.HouseSpec SpecFor(Entry entry, TownPalette p)
        {
            if (entry.Kind != Kind.House)
                return null;

            var spec = new TownBuildings.HouseSpec { Width = entry.Width, Depth = entry.Depth, FrontOffset = FrontOffset };
            switch (entry.Variant % 8)
            {
                case 0:
                    spec.Floors = 2; spec.DoorSide = 1; spec.Wall = p.Cream; spec.Roof = p.RoofTerracotta;
                    spec.Door = p.DoorRed; spec.Shutters = p.DoorGreen; spec.Porch = true; spec.Car = true;
                    break;
                case 1:
                    spec.Floors = 1; spec.DoorSide = -1; spec.Wall = p.Yellow; spec.Roof = p.RoofBrown;
                    spec.Door = p.DoorBlue; spec.Garage = true; spec.Hedge = true;
                    break;
                case 2:
                    spec.Floors = 2; spec.DoorSide = -1; spec.Wall = p.Brick; spec.Roof = p.RoofSlate;
                    spec.Door = p.DoorGreen; spec.Shutters = p.TrimDark; spec.Porch = true; spec.Car = true;
                    break;
                case 3:
                    spec.Floors = 1; spec.DoorSide = 1; spec.Slope = 0.95f; spec.Wall = p.GreyBlue; spec.Roof = p.RoofTerracotta;
                    spec.Door = p.DoorRed; spec.Shutters = p.TrimWhite; spec.Car = true; spec.Hedge = true;
                    break;
                case 4:
                    spec.Floors = 2; spec.DoorSide = 1; spec.Wall = p.White; spec.Roof = p.RoofBrown;
                    spec.Door = p.DoorBlue; spec.Shutters = p.DoorGreen; spec.Wing = true;
                    break;
                case 5:
                    spec.Floors = 2; spec.DoorSide = -1; spec.Wall = p.Yellow; spec.Roof = p.RoofSlate;
                    spec.Door = p.DoorRed; spec.Shutters = p.TrimWhite; spec.Porch = true; spec.Hedge = true;
                    break;
                case 6:
                    spec.Floors = 2; spec.DoorSide = 1; spec.Wall = p.GreyBlue; spec.Roof = p.RoofSlate;
                    spec.Door = p.DoorGreen; spec.Shutters = p.TrimWhite; spec.Porch = true; spec.Garage = true;
                    break;
                default:
                    spec.Floors = 1; spec.DoorSide = -1; spec.Wall = p.Cream; spec.Roof = p.RoofBrown;
                    spec.Door = p.DoorRed; spec.Shutters = p.DoorGreen; spec.Hedge = true; spec.Car = true;
                    break;
            }

            return spec;
        }

        // How far left and right of its middle (local X) a building and its yard reach, for spacing the row.
        private static (float min, float max) ExtentOf(Entry entry, TownBuildings.HouseSpec spec)
        {
            var half = entry.Width / 2f;
            switch (entry.Kind)
            {
                case Kind.Apartment:
                    return (-half - 1f, half + 1f);
                case Kind.Barn:
                    return (-half - 0.5f, half + 0.5f);
                case Kind.Substation:
                    return (-half - 0.4f, half + 0.4f);
                case Kind.WaterTower:
                    return (-4.4f, 4.4f);
            }

            var min = -half - 1f;
            var max = half + 1f;
            if (spec != null && spec.Garage)
            {
                var reach = half + 3.9f;
                if (-spec.DoorSide > 0) max = reach; else min = -reach;
            }

            return (min, max);
        }

        // Returns true when the body should get a collider (the open-air kinds do not).
        private static bool Build(Entry entry, TownBuildings.HouseSpec spec, TownMesh body, TownMesh yard, TownPalette p, System.Random random)
        {
            switch (entry.Kind)
            {
                case Kind.Apartment:
                    TownBuildings.Apartment(body, yard, p, entry.Width, entry.Depth, FrontOffset);
                    return true;
                case Kind.Barn:
                    TownBuildings.Barn(body, yard, p, entry.Width, entry.Depth, FrontOffset);
                    return true;
                case Kind.Substation:
                    TownBuildings.Substation(body, p, entry.Width, entry.Depth);
                    return false;
                case Kind.WaterTower:
                    TownBuildings.WaterTower(body, p);
                    return false;
                default:
                    TownBuildings.House(body, yard, p, random, spec);
                    return true;
            }
        }

        // ---- the ground: plaza, tower pad, streets with their markings, sidewalks ----

        private static TownMesh BuildGround(TownPalette p, Vector3 center, Vector2 pad)
        {
            var mesh = new TownMesh();

            Rect(mesh, p.Gravel, center, -PlazaHalf, PlazaHalf, -PlazaHalf, PlazaHalf, AsphaltY);
            mesh.Flat(p.Path, new Vector3(center.x, PadY, center.z), pad.x * 2f, pad.y * 2f);

            foreach (var sx in new[] { -1f, 1f })
            {
                foreach (var sz in new[] { -1f, 1f })
                {
                    // The ring street, one straight piece per side...
                    Rect(mesh, p.Asphalt, center, -RoadOuter, RoadOuter, sz * RoadInner, sz * RoadOuter, AsphaltY);
                    Rect(mesh, p.Asphalt, center, sx * RoadInner, sx * RoadOuter, -RoadInner, RoadInner, AsphaltY);
                    // ...and the four cross streets running out past its corners, which just stop.
                    Rect(mesh, p.Asphalt, center, sx * RoadInner, sx * RoadOuter, sz * RoadOuter, sz * StreetEnd, AsphaltY);
                    Rect(mesh, p.Asphalt, center, sx * RoadOuter, sx * StreetEnd, sz * RoadInner, sz * RoadOuter, AsphaltY);

                    // Sidewalks: round the plaza, along the ring's outside, and along both sides of the cross streets.
                    Rect(mesh, p.Sidewalk, center, -RoadInner, RoadInner, sz * PlazaHalf, sz * RoadInner, SidewalkY);
                    Rect(mesh, p.Sidewalk, center, sx * PlazaHalf, sx * RoadInner, -PlazaHalf, PlazaHalf, SidewalkY);
                    Rect(mesh, p.Sidewalk, center, -RoadInner, RoadInner, sz * RoadOuter, sz * WalkOuter, SidewalkY);
                    Rect(mesh, p.Sidewalk, center, sx * RoadOuter, sx * WalkOuter, -RoadInner, RoadInner, SidewalkY);
                    Rect(mesh, p.Sidewalk, center, sx * PlazaHalf, sx * RoadInner, sz * RoadOuter, sz * StreetEnd, SidewalkY);
                    Rect(mesh, p.Sidewalk, center, sx * RoadOuter, sx * WalkOuter, sz * RoadOuter, sz * StreetEnd, SidewalkY);
                    Rect(mesh, p.Sidewalk, center, sx * RoadOuter, sx * StreetEnd, sz * PlazaHalf, sz * RoadInner, SidewalkY);
                    Rect(mesh, p.Sidewalk, center, sx * RoadOuter, sx * StreetEnd, sz * RoadOuter, sz * WalkOuter, SidewalkY);
                }
            }

            // Dashed centre lines: along each side of the ring (between the crossings) and out along the cross streets.
            for (float t = -RoadInner + 2f; t <= RoadInner - 2f; t += 4f)
            {
                foreach (var side in new[] { -1f, 1f })
                {
                    mesh.Flat(p.Paint, new Vector3(center.x + t, AsphaltY + 0.01f, center.z + side * RoadMiddle), 0.15f, 2.2f, 90f);
                    mesh.Flat(p.Paint, new Vector3(center.x + side * RoadMiddle, AsphaltY + 0.01f, center.z + t), 0.15f, 2.2f);
                }
            }

            for (float t = RoadOuter + 3f; t <= StreetEnd - 2f; t += 4f)
            {
                foreach (var a in new[] { -1f, 1f })
                {
                    foreach (var b in new[] { -1f, 1f })
                    {
                        mesh.Flat(p.Paint, new Vector3(center.x + a * RoadMiddle, AsphaltY + 0.01f, center.z + b * t), 0.15f, 2.2f);
                        mesh.Flat(p.Paint, new Vector3(center.x + b * t, AsphaltY + 0.01f, center.z + a * RoadMiddle), 0.15f, 2.2f, 90f);
                    }
                }
            }

            return mesh;
        }

        // ---- the one road out ----

        // The south cross street on the east side carries on past the town's edge, bending through the forest, laid
        // over the terrain (the ground rises into hills out there). Not batching-static: it is one long mesh.
        private static TownMesh BuildExitRoad(TownPalette p, Vector3 center)
        {
            const float step = 4f;
            const float lift = 0.03f;
            var mesh = new TownMesh();
            var middle = new Vector2(center.x, center.z);

            var terrain = Terrain.activeTerrain;
            float GroundAt(Vector2 point) => terrain != null
                ? terrain.SampleHeight(new Vector3(point.x, 0f, point.y)) + terrain.GetPosition().y
                : 0f;

            // One cross-section of the road at `distance`: its two edges and the level it sits at. The level is the
            // highest ground under the width, so a bump never pokes through the asphalt.
            void Section(float distance, float halfWidth, out Vector3 left, out Vector3 right)
            {
                var point = TownLayout.ExitPoint(middle, distance);
                var ahead = TownLayout.ExitPoint(middle, distance + 0.5f);
                var tangent = (ahead - point).normalized;
                var side = new Vector2(-tangent.y, tangent.x) * halfWidth;

                var leftPoint = point + side;
                var rightPoint = point - side;
                var level = Mathf.Max(GroundAt(point), Mathf.Max(GroundAt(leftPoint), GroundAt(rightPoint))) + lift;
                left = new Vector3(leftPoint.x, level, leftPoint.y);
                right = new Vector3(rightPoint.x, level, rightPoint.y);
            }

            var shoulderHalf = TownLayout.ExitRoadHalfWidth + TownLayout.ExitRoadShoulder;
            for (float distance = StreetEnd; distance < TownLayout.ExitRoadLength; distance += step)
            {
                Section(distance, shoulderHalf, out var shoulderLeft0, out var shoulderRight0);
                Section(distance + step, shoulderHalf, out var shoulderLeft1, out var shoulderRight1);
                Section(distance, TownLayout.ExitRoadHalfWidth, out var left0, out var right0);
                Section(distance + step, TownLayout.ExitRoadHalfWidth, out var left1, out var right1);

                // Gravel shoulder a hair lower than the asphalt, so the asphalt never fights it for the same pixels.
                mesh.Quad(p.Gravel, shoulderLeft0 - Vector3.up * 0.01f, shoulderLeft1 - Vector3.up * 0.01f,
                    shoulderRight1 - Vector3.up * 0.01f, shoulderRight0 - Vector3.up * 0.01f, Vector3.up);
                mesh.Quad(p.Asphalt, left0, left1, right1, right0, Vector3.up);

                // Dashed centre line: 3m of paint, 5m of gap.
                // Built from the asphalt's own edge points so it always sits on the road's surface.
                if (Mathf.Repeat(distance - StreetEnd, 8f) < step)
                {
                    Section(distance + 3f, TownLayout.ExitRoadHalfWidth, out var end, out var endOther);
                    var up = Vector3.up * 0.04f;
                    mesh.Quad(p.Paint,
                        Vector3.Lerp(left0, right0, 0.475f) + up, Vector3.Lerp(end, endOther, 0.475f) + up,
                        Vector3.Lerp(end, endOther, 0.525f) + up, Vector3.Lerp(left0, right0, 0.525f) + up, Vector3.up);
                }
            }

            return mesh;
        }

        // A flat rectangle from (x0, z0) to (x1, z1), given relative to `center`, in either order.
        private static void Rect(TownMesh mesh, Material material, Vector3 center, float x0, float x1, float z0, float z1, float y)
        {
            var xMin = Mathf.Min(x0, x1);
            var xMax = Mathf.Max(x0, x1);
            var zMin = Mathf.Min(z0, z1);
            var zMax = Mathf.Max(z0, z1);
            mesh.Flat(material, new Vector3(center.x + (xMin + xMax) / 2f, y, center.z + (zMin + zMax) / 2f), xMax - xMin, zMax - zMin);
        }

        // ---- street lamps, parked cars, benches, planters ----

        private static TownMesh BuildProps(TownPalette p, Vector3 center)
        {
            var mesh = new TownMesh();

            // Lamps along the outside sidewalk of the ring, leaning over the street...
            const float lampLine = (RoadOuter + WalkOuter) / 2f;
            foreach (var (outward, _) in Sides)
            {
                var tangent = Vector3.Cross(Vector3.up, outward);
                foreach (var t in new[] { -15f, -5f, 5f, 15f })
                    TownParts.StreetLamp(mesh, p, center + outward * lampLine + tangent * t, -outward);
            }

            // ...and along the cross streets, on the corner blocks' side.
            foreach (var sx in new[] { -1f, 1f })
            {
                foreach (var sz in new[] { -1f, 1f })
                {
                    foreach (var t in new[] { 34f, 46f })
                    {
                        TownParts.StreetLamp(mesh, p, center + new Vector3(sx * lampLine, 0f, sz * t), new Vector3(-sx, 0f, 0f));
                        TownParts.StreetLamp(mesh, p, center + new Vector3(sx * t, 0f, sz * lampLine), new Vector3(0f, 0f, -sz));
                    }
                }
            }

            // Cars parked along the ring street's outer curb.
            const float curb = RoadOuter - 1.1f;
            var colors = new[] { p.CarRed, p.CarBlue, p.CarWhite, p.CarGrey };
            var parked = new (float x, float z, float yaw)[]
            {
                (-9f, curb, 90f), (11f, curb, 90f), (4f, -curb, 90f), (-13f, -curb, 90f),
                (curb, -7f, 0f), (curb, 12f, 0f), (-curb, 8f, 0f), (-curb, -11f, 0f),
            };
            for (int i = 0; i < parked.Length; i++)
                TownParts.Car(mesh, p, colors[i % colors.Length], center + new Vector3(parked[i].x, AsphaltY + 0.02f, parked[i].z), parked[i].yaw);

            // Benches facing the tower and planters in the plaza's corners.
            foreach (var (outward, _) in Sides)
                TownParts.Bench(mesh, p, center + outward * 16.5f, -outward);
            foreach (var sx in new[] { -1f, 1f })
            {
                foreach (var sz in new[] { -1f, 1f })
                    TownParts.Planter(mesh, p, center + new Vector3(sx * 16.5f, 0f, sz * 16.5f));
            }

            return mesh;
        }

        // ---- the low wall round the tower ----

        // Decoration only (no collider), so it can never get in the way of the ladder or the complete screen.
        // Each side has a wide gap in the middle, like a gateway, with a high-voltage sign beside it.
        private static TownMesh BuildWall(TownPalette p, Vector3 center, float half)
        {
            const float height = 0.9f;
            const float thickness = 0.35f;

            var mesh = new TownMesh();
            for (int side = 0; side < 4; side++)
            {
                var yaw = side * 90f;
                var rotation = Quaternion.Euler(0f, yaw, 0f);
                Vector3 At(float x, float y, float z) => center + rotation * new Vector3(x, y, z);

                foreach (var sign in new[] { -1f, 1f })
                {
                    var length = half - WallGapHalf;
                    var middle = sign * (WallGapHalf + half) / 2f;
                    mesh.Box(p.Concrete, At(middle, height / 2f, half), new Vector3(length, height, thickness), yaw);
                    mesh.Box(p.TrimWhite, At(middle, height + 0.05f, half), new Vector3(length + 0.1f, 0.1f, thickness + 0.15f), yaw);

                    PillarAt(mesh, p, At(sign * WallGapHalf, 0f, half), yaw);
                    if (sign > 0f)
                        PillarAt(mesh, p, At(half, 0f, half), yaw);
                }

                // High-voltage sign on the right-hand piece, facing out.
                var signX = (WallGapHalf + half) / 2f;
                mesh.Box(p.Safety, At(signX, 0.55f, half + 0.2f), new Vector3(0.7f, 0.7f, 0.03f), yaw);
                mesh.Box(p.MetalDark, At(signX, 0.55f, half + 0.22f), new Vector3(0.45f, 0.07f, 0.01f), yaw);
                mesh.Box(p.MetalDark, At(signX, 0.55f, half + 0.22f), new Vector3(0.07f, 0.45f, 0.01f), yaw);
            }

            return mesh;
        }

        private static void PillarAt(TownMesh mesh, TownPalette p, Vector3 position, float yaw)
        {
            mesh.Box(p.Concrete, position + Vector3.up * 0.65f, new Vector3(0.6f, 1.3f, 0.6f), yaw);
            mesh.Box(p.TrimWhite, position + Vector3.up * 1.34f, new Vector3(0.75f, 0.08f, 0.75f), yaw);
        }

        // ---- assets and scene objects ----

        // Saves the mesh as an asset (replacing an older one) and puts it in the scene. Returns its triangle count.
        private static int AddPiece(Transform parent, string assetName, TownMesh source, bool batchingStatic, bool castShadows, string folder)
        {
            if (source.IsEmpty)
                return 0;

            var mesh = source.ToMesh(assetName);
            var path = $"{folder}/{assetName}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);

            var piece = new GameObject(assetName);
            piece.transform.SetParent(parent, false);
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = source.Materials;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

            if (batchingStatic)
                GameObjectUtility.SetStaticEditorFlags(piece, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);

            var count = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
                count += (int)mesh.GetIndexCount(i) / 3;
            return count;
        }

        // An invisible box around a building's solid part, so you can't walk through the walls.
        private static void AddCollider(Transform parent, string objectName, Vector3 position, float yaw, Bounds localBounds)
        {
            var holder = new GameObject(objectName);
            holder.transform.SetParent(parent, false);
            holder.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            var box = holder.AddComponent<BoxCollider>();
            box.center = localBounds.center;
            box.size = localBounds.size;
        }

        private static Bounds TowerBounds(Transform tower)
        {
            var renderers = tower.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning("[FullyVolted] The tower has no renderers; the town is centred on the tower position instead.");
                return new Bounds(ClimbSceneBuilder.TowerPosition, new Vector3(8f, 0f, 8f));
            }

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
