using System.Collections.Generic;
using UnityEngine;

namespace FullyVolted.EditorTools
{
    // The nine kinds of building. Each is modelled around its own middle, with its front facing local +Z and its wall
    // bases on y = 0. `body` is the solid part (it gets a collider); `yard` is everything around it (paths, fences,
    // cars) which you can walk through.
    internal static class TownBuildings
    {
        private const float Floor = TownParts.FloorHeight;

        internal sealed class HouseSpec
        {
            public float Width, Depth, FrontOffset;
            public int Floors = 2;
            public float Slope = 0.75f;
            public int DoorSide = 1;
            public Material Wall, Roof, Door, Shutters;
            public bool Chimney = true, Porch, Garage, Wing, Car, Hedge;
        }

        // ---- houses ----

        public static void House(TownMesh body, TownMesh yard, TownPalette p, System.Random random, HouseSpec s)
        {
            var w = s.Width;
            var d = s.Depth;
            var h = s.Floors * Floor;
            var front = d / 2f;
            var doorX = s.DoorSide * w * 0.17f;
            var garageSide = -s.DoorSide;

            body.Box(p.Foundation, new Vector3(0f, 0.22f, 0f), new Vector3(w + 0.12f, 0.44f, d + 0.12f));
            body.Box(s.Wall, new Vector3(0f, h / 2f, 0f), new Vector3(w, h, d));

            // The ridge runs along the longer side.
            var alongX = w >= d;
            var rise = (alongX ? d : w) / 2f * s.Slope;
            if (alongX)
                body.GableRoof(s.Roof, p.TrimDark, s.Wall, new Vector3(0f, h, 0f), d, w, rise, 0.45f, 90f);
            else
                body.GableRoof(s.Roof, p.TrimDark, s.Wall, new Vector3(0f, h, 0f), w, d, rise, 0.45f);

            if (s.Chimney)
            {
                var chimney = alongX ? new Vector3(w * 0.25f, 0f, 0.9f) : new Vector3(0.9f, 0f, d * 0.25f);
                TownParts.Chimney(body, p, chimney, h + rise * 0.2f, h + rise + 1.1f);
            }

            TownParts.Door(body, p, s.Door, doorX, front, s.Porch);
            TownParts.Facade(body, p, new Vector3(0f, 0f, front), Vector3.forward, w, s.Floors, doorX, s.Shutters);
            TownParts.Facade(body, p, new Vector3(0f, 0f, -front), Vector3.back, w, s.Floors, float.NaN, s.Shutters);
            if (!(s.Garage && garageSide > 0))
                TownParts.Facade(body, p, new Vector3(w / 2f, 0f, 0f), Vector3.right, d, s.Floors, float.NaN, s.Shutters);
            if (!(s.Garage && garageSide < 0))
                TownParts.Facade(body, p, new Vector3(-w / 2f, 0f, 0f), Vector3.left, d, s.Floors, float.NaN, s.Shutters);

            var garageX = 0f;
            if (s.Garage)
            {
                garageX = garageSide * (w / 2f + 1.7f);
                var garageDepth = Mathf.Max(4.5f, d * 0.75f);
                var garageZ = front - garageDepth / 2f;
                body.Box(s.Wall, new Vector3(garageX, 1.35f, garageZ), new Vector3(3.4f, 2.7f, garageDepth));
                body.Box(p.RoofFlat, new Vector3(garageX, 2.78f, garageZ), new Vector3(3.8f, 0.16f, garageDepth + 0.4f));
                body.Box(p.TrimWhite, new Vector3(garageX, 1.05f, front + 0.03f), new Vector3(2.6f, 2.1f, 0.06f));
                body.Box(p.Concrete, new Vector3(garageX, 0.7f, front + 0.065f), new Vector3(2.6f, 0.05f, 0.02f));
                body.Box(p.Concrete, new Vector3(garageX, 1.4f, front + 0.065f), new Vector3(2.6f, 0.05f, 0.02f));
            }

            if (s.Wing)
            {
                const float wingWidth = 5f;
                const float wingDepth = 5.5f;
                var wingX = w / 2f - wingWidth / 2f;
                var wingZ = -(d / 2f + wingDepth / 2f - 0.5f);
                body.Box(p.Foundation, new Vector3(wingX, 0.22f, wingZ), new Vector3(wingWidth + 0.12f, 0.44f, wingDepth + 0.12f));
                body.Box(s.Wall, new Vector3(wingX, h / 2f, wingZ), new Vector3(wingWidth, h, wingDepth));
                body.GableRoof(s.Roof, p.TrimDark, s.Wall, new Vector3(wingX, h, wingZ), wingWidth, wingDepth,
                    wingWidth / 2f * s.Slope, 0.45f);
                TownParts.Facade(body, p, new Vector3(wingX + wingWidth / 2f, 0f, wingZ), Vector3.right, wingDepth, s.Floors, float.NaN, s.Shutters);
                TownParts.Facade(body, p, new Vector3(wingX, 0f, wingZ - wingDepth / 2f), Vector3.back, wingWidth, s.Floors, float.NaN, s.Shutters);
                TownParts.Facade(body, p, new Vector3(wingX - wingWidth / 2f, 0f, wingZ), Vector3.left, wingDepth, s.Floors, float.NaN, s.Shutters);
            }

            Yard(yard, p, random, s, doorX, s.Garage ? garageX : float.NaN);
        }

        // Path to the door, a hedge or picket fence along the sidewalk with gaps for the path, the driveway and the
        // parked car, and the car itself.
        private static void Yard(TownMesh yard, TownPalette p, System.Random random, HouseSpec s, float doorX, float garageX)
        {
            var front = s.Depth / 2f;
            var fenceZ = front + s.FrontOffset - 0.45f;
            var gaps = new List<(float center, float half)> { (doorX, 0.8f) };

            var from = front + 0.8f;
            var to = front + s.FrontOffset + 0.1f;
            yard.Flat(p.Path, new Vector3(doorX, 0.045f, (from + to) / 2f), 1.2f, to - from);

            var xMin = -s.Width / 2f - 1.5f;
            var xMax = s.Width / 2f + 1.5f;

            if (!float.IsNaN(garageX))
            {
                gaps.Add((garageX, 1.8f));
                yard.Flat(p.Concrete, new Vector3(garageX, 0.045f, front + (s.FrontOffset + 0.2f) / 2f), 3.2f, s.FrontOffset + 0.2f);
                xMin = Mathf.Min(xMin, garageX - 2.2f);
                xMax = Mathf.Max(xMax, garageX + 2.2f);
            }
            else if (s.Car)
            {
                var carX = doorX - s.DoorSide * 3.6f;
                var carZ = front + s.FrontOffset * 0.5f;
                yard.Flat(p.Sidewalk, new Vector3(carX, 0.045f, carZ), 4.8f, 2.4f);
                var paint = new[] { p.CarRed, p.CarBlue, p.CarWhite, p.CarGrey };
                TownParts.Car(yard, p, paint[random.Next(paint.Length)], new Vector3(carX, 0.05f, carZ), 90f);
                gaps.Add((carX, 2.5f));
            }

            gaps.Sort((a, b) => a.center.CompareTo(b.center));
            var cursor = xMin;
            foreach (var gap in gaps)
            {
                TownParts.YardEdge(yard, p, s.Hedge, cursor, gap.center - gap.half, fenceZ);
                cursor = gap.center + gap.half;
            }

            TownParts.YardEdge(yard, p, s.Hedge, cursor, xMax, fenceZ);
        }

        // ---- apartment block ----

        public static void Apartment(TownMesh body, TownMesh yard, TownPalette p, float w, float d, float frontOffset)
        {
            const int floors = 3;
            var h = floors * Floor;
            var front = d / 2f;

            body.Box(p.Foundation, new Vector3(0f, 0.22f, 0f), new Vector3(w + 0.12f, 0.44f, d + 0.12f));
            body.Box(p.Concrete, new Vector3(0f, h / 2f, 0f), new Vector3(w, h, d));
            body.Box(p.RoofFlat, new Vector3(0f, h + 0.04f, 0f), new Vector3(w - 0.1f, 0.08f, d - 0.1f));

            // Parapet round the flat roof.
            body.Box(p.TrimWhite, new Vector3(0f, h + 0.3f, front - 0.1f), new Vector3(w + 0.2f, 0.6f, 0.2f));
            body.Box(p.TrimWhite, new Vector3(0f, h + 0.3f, -front + 0.1f), new Vector3(w + 0.2f, 0.6f, 0.2f));
            body.Box(p.TrimWhite, new Vector3(w / 2f - 0.1f, h + 0.3f, 0f), new Vector3(0.2f, 0.6f, d));
            body.Box(p.TrimWhite, new Vector3(-w / 2f + 0.1f, h + 0.3f, 0f), new Vector3(0.2f, 0.6f, d));

            // Stairwell and air-conditioning units on the roof.
            body.Box(p.Concrete, new Vector3(-w * 0.25f, h + 1.3f, 0f), new Vector3(3f, 2.6f, 2.6f));
            body.Box(p.RoofFlat, new Vector3(-w * 0.25f, h + 2.68f, 0f), new Vector3(3.3f, 0.16f, 2.9f));
            for (int i = 0; i < 3; i++)
                body.Box(p.MetalLight, new Vector3(w * 0.12f + i * 1.5f, h + 0.55f, -1.2f), new Vector3(1.0f, 0.9f, 0.8f));

            TownParts.Door(body, p, p.Glass, 0f, front, true);
            TownParts.Facade(body, p, new Vector3(0f, 0f, front), Vector3.forward, w, floors, 0f, null);
            TownParts.Facade(body, p, new Vector3(0f, 0f, -front), Vector3.back, w, floors, float.NaN, null);
            TownParts.Facade(body, p, new Vector3(w / 2f, 0f, 0f), Vector3.right, d, floors, float.NaN, null);
            TownParts.Facade(body, p, new Vector3(-w / 2f, 0f, 0f), Vector3.left, d, floors, float.NaN, null);

            // Balconies under the second and third storey windows next to the entrance.
            var count = Mathf.Max(1, Mathf.FloorToInt((w - 1.2f) / 2.6f));
            var spacing = w / count;
            var balconyColumns = count >= 4 ? new[] { 1, count - 2 } : new int[0];
            foreach (var column in balconyColumns)
            {
                var x = -w / 2f + spacing * (column + 0.5f);
                for (int floor = 1; floor < floors; floor++)
                {
                    var y = floor * Floor;
                    body.Box(p.Concrete, new Vector3(x, y + 0.08f, front + 0.55f), new Vector3(2.4f, 0.16f, 1.1f));
                    body.Box(p.Glass, new Vector3(x, y + 0.7f, front + 1.07f), new Vector3(2.4f, 0.9f, 0.04f));
                    body.Box(p.Glass, new Vector3(x - 1.18f, y + 0.7f, front + 0.55f), new Vector3(0.04f, 0.9f, 1.0f));
                    body.Box(p.Glass, new Vector3(x + 1.18f, y + 0.7f, front + 0.55f), new Vector3(0.04f, 0.9f, 1.0f));
                    body.Box(p.MetalDark, new Vector3(x, y + 1.17f, front + 1.07f), new Vector3(2.4f, 0.05f, 0.06f));
                }
            }

            // Paved forecourt, a hedge along the sidewalk, bicycles' worth of bollards at the entrance.
            yard.Flat(p.Sidewalk, new Vector3(0f, 0.045f, front + (frontOffset + 0.2f) / 2f), w * 0.5f, frontOffset + 0.2f);
            TownParts.YardEdge(yard, p, true, -w / 2f - 1f, -w * 0.25f - 1f, front + frontOffset - 0.45f);
            TownParts.YardEdge(yard, p, true, w * 0.25f + 1f, w / 2f + 1f, front + frontOffset - 0.45f);
            for (int sign = -1; sign <= 1; sign += 2)
                yard.Box(p.MetalDark, new Vector3(sign * 1.6f, 0.45f, front + 1.6f), new Vector3(0.12f, 0.9f, 0.12f));
        }

        // ---- barn / warehouse ----

        public static void Barn(TownMesh body, TownMesh yard, TownPalette p, float w, float d, float frontOffset)
        {
            const float h = 5f;
            var front = d / 2f;
            var rise = d / 2f * 0.45f;

            body.Box(p.Foundation, new Vector3(0f, 0.22f, 0f), new Vector3(w + 0.12f, 0.44f, d + 0.12f));
            body.Box(p.Sheet, new Vector3(0f, h / 2f, 0f), new Vector3(w, h, d));
            body.GableRoof(p.RoofFlat, p.TrimDark, p.Sheet, new Vector3(0f, h, 0f), d, w, rise, 0.4f, 90f);
            body.Box(p.MetalLight, new Vector3(0f, h + rise + 0.05f, 0f), new Vector3(w + 0.8f, 0.12f, 0.35f));

            // Corrugated sheet look: thin vertical ribs along both long walls.
            for (float x = -w / 2f + 0.5f; x <= w / 2f - 0.49f; x += 1.0f)
            {
                if (Mathf.Abs(x) > 2.5f)
                    body.Box(p.MetalLight, new Vector3(x, (h + 0.44f) / 2f, front + 0.04f), new Vector3(0.1f, h - 0.44f, 0.08f));
                body.Box(p.MetalLight, new Vector3(x, (h + 0.44f) / 2f, -front - 0.04f), new Vector3(0.1f, h - 0.44f, 0.08f));
            }

            // The big sliding door on its rail.
            body.Box(p.TrimDark, new Vector3(0f, 2.2f, front + 0.06f), new Vector3(4.4f, 3.8f, 0.12f));
            body.Box(p.RoofBrown, new Vector3(0f, 2.15f, front + 0.12f), new Vector3(4.0f, 3.6f, 0.1f));
            foreach (var y in new[] { 0.9f, 2.0f, 3.1f })
                body.Box(p.TrimDark, new Vector3(0f, y + 0.25f, front + 0.19f), new Vector3(4.0f, 0.1f, 0.04f));
            body.Box(p.MetalDark, new Vector3(0f, 4.3f, front + 0.2f), new Vector3(8.0f, 0.12f, 0.12f));

            foreach (var x in new[] { -w * 0.33f, w * 0.33f })
            {
                TownParts.Window(body, p, new Vector3(x, 3.5f, front), Vector3.forward, 1.0f, 0.7f, null);
                TownParts.Window(body, p, new Vector3(x, 3.5f, -front), Vector3.back, 1.0f, 0.7f, null);
            }

            TownParts.Window(body, p, new Vector3(w / 2f, h + rise * 0.4f, 0f), Vector3.right, 0.9f, 0.9f, null);
            TownParts.Window(body, p, new Vector3(-w / 2f, h + rise * 0.4f, 0f), Vector3.left, 0.9f, 0.9f, null);

            yard.Flat(p.Gravel, new Vector3(0f, 0.045f, front + (frontOffset + 0.2f) / 2f), w * 0.8f, frontOffset + 0.2f);
            // Hay bales and a stack of crates beside the door.
            for (int i = 0; i < 3; i++)
                yard.Box(p.Yellow, new Vector3(w * 0.3f + i * 1.3f, 0.45f, front + 1.2f), new Vector3(1.2f, 0.9f, 0.9f));
            yard.Box(p.Yellow, new Vector3(w * 0.3f + 0.65f, 1.35f, front + 1.2f), new Vector3(1.2f, 0.9f, 0.9f));
            yard.Box(p.RoofBrown, new Vector3(-w * 0.3f, 0.35f, front + 1.0f), new Vector3(1.0f, 0.7f, 1.0f));
            yard.Box(p.RoofBrown, new Vector3(-w * 0.3f + 1.1f, 0.35f, front + 1.0f), new Vector3(1.0f, 0.7f, 1.0f));
            yard.Box(p.RoofBrown, new Vector3(-w * 0.3f + 0.55f, 1.05f, front + 1.0f), new Vector3(1.0f, 0.7f, 1.0f));
        }

        // ---- electrical substation (everything inside a fence; no collider) ----

        public static void Substation(TownMesh mesh, TownPalette p, float w, float d)
        {
            var hx = w / 2f;
            var hz = d / 2f;
            mesh.Box(p.Concrete, new Vector3(0f, 0.06f, 0f), new Vector3(w, 0.12f, d));

            // Fence: back, both sides, and the front with a gap for the open gate.
            FenceX(mesh, p, -hx, hx, -hz);
            FenceZ(mesh, p, -hz, hz, -hx);
            FenceZ(mesh, p, -hz, hz, hx);
            FenceX(mesh, p, -hx, -1.8f, hz);
            FenceX(mesh, p, 1.8f, hx, hz);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                mesh.Box(p.Safety, new Vector3(sign * 1.8f, 1.1f, hz), new Vector3(0.18f, 2.2f, 0.18f));
                mesh.Box(p.MetalLight, new Vector3(sign * 1.8f, 0.95f, hz - 0.8f), new Vector3(0.04f, 1.7f, 1.6f));
            }

            // Warning sign on the fence.
            mesh.Box(p.Safety, new Vector3(-3.4f, 1.3f, hz + 0.05f), new Vector3(0.9f, 0.9f, 0.03f));
            mesh.Box(p.MetalDark, new Vector3(-3.4f, 1.3f, hz + 0.075f), new Vector3(0.6f, 0.08f, 0.01f));
            mesh.Box(p.MetalDark, new Vector3(-3.4f, 1.3f, hz + 0.075f), new Vector3(0.08f, 0.55f, 0.01f));

            // Two transformers: tank, radiator panels and three bushings on top.
            foreach (var x in new[] { -3.5f, 3.5f })
            {
                mesh.Box(p.Transformer, new Vector3(x, 1.22f, -1f), new Vector3(2.6f, 2.2f, 1.8f));
                mesh.Box(p.MetalLight, new Vector3(x - 1.55f, 1.2f, -1f), new Vector3(0.35f, 1.8f, 1.5f));
                mesh.Box(p.MetalLight, new Vector3(x + 1.55f, 1.2f, -1f), new Vector3(0.35f, 1.8f, 1.5f));
                for (int k = 0; k < 3; k++)
                {
                    var bushingX = x - 0.8f + k * 0.8f;
                    mesh.Cylinder(p.Ceramic, new Vector3(bushingX, 2.32f, -1f), 0.14f, 0.08f, 0.9f, 6);
                    mesh.Box(p.MetalDark, new Vector3(bushingX, 3.28f, -1f), new Vector3(0.2f, 0.12f, 0.2f));
                }

                // The line from the gantry insulator, down and across to the middle bushing.
                mesh.Box(p.MetalDark, new Vector3(x, 4.15f, -3.8f), new Vector3(0.03f, 1.7f, 0.03f));
                mesh.Box(p.MetalDark, new Vector3(x, 3.3f, -2.4f), new Vector3(0.03f, 0.03f, 2.8f));
            }

            // Steel gantry with hanging insulators.
            foreach (var x in new[] { -6.2f, 6.2f })
                mesh.Box(p.MetalLight, new Vector3(x, 3.1f, -3.8f), new Vector3(0.3f, 6.0f, 0.3f));
            mesh.Box(p.MetalLight, new Vector3(0f, 5.9f, -3.8f), new Vector3(12.6f, 0.3f, 0.3f));
            foreach (var x in new[] { -3.5f, 0f, 3.5f })
                mesh.Cylinder(p.Ceramic, new Vector3(x, 5.0f, -3.8f), 0.09f, 0.09f, 0.85f, 6);

            // Control building.
            mesh.Box(p.Concrete, new Vector3(-5.3f, 1.5f, 2.4f), new Vector3(4f, 3f, 3f));
            mesh.Box(p.RoofFlat, new Vector3(-5.3f, 3.08f, 2.4f), new Vector3(4.3f, 0.16f, 3.3f));
            TownParts.Door(mesh, p, p.DoorBlue, -5.3f, 3.9f, false);
            TownParts.Window(mesh, p, new Vector3(-6.5f, 1.7f, 3.9f), Vector3.forward, 0.9f, 0.7f, null);
        }

        private static void FenceX(TownMesh mesh, TownPalette p, float x0, float x1, float z)
        {
            var length = x1 - x0;
            var middle = (x0 + x1) / 2f;
            mesh.Box(p.MetalLight, new Vector3(middle, 0.95f, z), new Vector3(length, 1.7f, 0.03f));
            mesh.Box(p.MetalDark, new Vector3(middle, 1.82f, z), new Vector3(length, 0.06f, 0.06f));
            mesh.Box(p.MetalDark, new Vector3(middle, 1.97f, z), new Vector3(length, 0.14f, 0.1f));
            for (float x = x0; x <= x1 + 0.01f; x += 2f)
                mesh.Box(p.MetalDark, new Vector3(x, 1.0f, z), new Vector3(0.12f, 2.0f, 0.12f));
        }

        private static void FenceZ(TownMesh mesh, TownPalette p, float z0, float z1, float x)
        {
            var length = z1 - z0;
            var middle = (z0 + z1) / 2f;
            mesh.Box(p.MetalLight, new Vector3(x, 0.95f, middle), new Vector3(0.03f, 1.7f, length));
            mesh.Box(p.MetalDark, new Vector3(x, 1.82f, middle), new Vector3(0.06f, 0.06f, length));
            mesh.Box(p.MetalDark, new Vector3(x, 1.97f, middle), new Vector3(0.1f, 0.14f, length));
            for (float z = z0; z <= z1 + 0.01f; z += 2f)
                mesh.Box(p.MetalDark, new Vector3(x, 1.0f, z), new Vector3(0.12f, 2.0f, 0.12f));
        }

        // ---- water tower (a landmark you can see from the top of the mast) ----

        public static void WaterTower(TownMesh mesh, TownPalette p)
        {
            const float deck = 9f;

            foreach (var sx in new[] { -2f, 2f })
            {
                foreach (var sz in new[] { -2f, 2f })
                {
                    mesh.Box(p.Concrete, new Vector3(sx, 0.15f, sz), new Vector3(0.8f, 0.3f, 0.8f));
                    mesh.Cylinder(p.MetalDark, new Vector3(sx, 0.3f, sz), 0.22f, 0.18f, deck - 0.3f, 6);
                }
            }

            foreach (var y in new[] { 3.0f, 6.0f })
            {
                foreach (var side in new[] { -2f, 2f })
                {
                    mesh.Box(p.MetalDark, new Vector3(0f, y, side), new Vector3(4.2f, 0.12f, 0.12f));
                    mesh.Box(p.MetalDark, new Vector3(side, y, 0f), new Vector3(0.12f, 0.12f, 4.2f));
                }
            }

            mesh.Box(p.MetalDark, new Vector3(0f, deck + 0.075f, 0f), new Vector3(7.0f, 0.15f, 7.0f), 0f, true);
            foreach (var side in new[] { -3.4f, 3.4f })
            {
                mesh.Box(p.MetalDark, new Vector3(0f, deck + 1.1f, side), new Vector3(6.8f, 0.05f, 0.05f));
                mesh.Box(p.MetalDark, new Vector3(side, deck + 1.1f, 0f), new Vector3(0.05f, 0.05f, 6.8f));
                foreach (var other in new[] { -3.4f, 3.4f })
                    mesh.Box(p.MetalDark, new Vector3(side, deck + 0.65f, other), new Vector3(0.06f, 1.0f, 0.06f));
            }

            mesh.Cylinder(p.GreyBlue, new Vector3(0f, deck + 0.15f, 0f), 3.0f, 3.0f, 3.4f, 14);
            foreach (var y in new[] { 1.0f, 2.3f })
                mesh.Cylinder(p.MetalDark, new Vector3(0f, deck + 0.15f + y, 0f), 3.05f, 3.05f, 0.12f, 14);
            mesh.Cylinder(p.RoofSlate, new Vector3(0f, deck + 3.55f, 0f), 3.2f, 0.1f, 1.5f, 14);
            mesh.Box(p.MetalDark, new Vector3(0f, deck + 5.3f, 0f), new Vector3(0.1f, 0.5f, 0.1f));

            // Ladder up the front.
            foreach (var x in new[] { -0.22f, 0.22f })
                mesh.Box(p.MetalLight, new Vector3(x, deck / 2f, 2.3f), new Vector3(0.05f, deck, 0.05f));
            for (float y = 0.5f; y < deck; y += 0.5f)
                mesh.Box(p.MetalLight, new Vector3(0f, y, 2.3f), new Vector3(0.44f, 0.04f, 0.04f));
        }
    }
}
