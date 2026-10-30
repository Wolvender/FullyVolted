using UnityEngine;

namespace FullyVolted.EditorTools
{
    // The small details the buildings are dressed with: windows, doors, chimneys, cars, lamps, fences. Each one adds
    // boxes to a TownMesh. `normal` is the way a wall faces, and the part is turned to match.
    internal static class TownParts
    {
        public const float FloorHeight = 2.8f;

        private static float YawOf(Vector3 normal)
        {
            return Mathf.Atan2(normal.x, normal.z) * Mathf.Rad2Deg;
        }

        // A framed window with a cross bar and a sill, standing out of the wall at `point` (the window's centre).
        public static void Window(TownMesh mesh, TownPalette p, Vector3 point, Vector3 normal, float width, float height, Material shutters)
        {
            var yaw = YawOf(normal);
            var side = Vector3.Cross(Vector3.up, normal);

            mesh.Box(p.TrimWhite, point + normal * 0.04f, new Vector3(width + 0.2f, height + 0.2f, 0.08f), yaw);
            mesh.Box(p.Glass, point + normal * 0.085f, new Vector3(width, height, 0.04f), yaw);
            mesh.Box(p.TrimWhite, point + normal * 0.11f, new Vector3(0.05f, height, 0.03f), yaw);
            mesh.Box(p.TrimWhite, point + normal * 0.11f, new Vector3(width, 0.05f, 0.03f), yaw);
            mesh.Box(p.Foundation, point + normal * 0.1f - Vector3.up * (height / 2f + 0.12f),
                new Vector3(width + 0.35f, 0.08f, 0.24f), yaw);

            if (shutters == null)
                return;

            for (int sign = -1; sign <= 1; sign += 2)
                mesh.Box(shutters, point + side * (sign * (width / 2f + 0.28f)) + normal * 0.05f,
                    new Vector3(0.36f, height + 0.1f, 0.05f), yaw);
        }

        // Rows of windows along one wall. `origin` is the middle of the wall's bottom edge; `doorOffset` (along the
        // wall, NaN for none) keeps the ground floor window clear of the door.
        public static void Facade(TownMesh mesh, TownPalette p, Vector3 origin, Vector3 normal, float wallWidth, int floors,
            float doorOffset, Material shutters, float windowWidth = 1.1f, float windowHeight = 1.3f)
        {
            var tangent = Vector3.Cross(Vector3.up, normal);
            var count = Mathf.Max(1, Mathf.FloorToInt((wallWidth - 1.2f) / 2.6f));
            var spacing = wallWidth / count;

            for (int k = 0; k < count; k++)
            {
                var offset = -wallWidth / 2f + spacing * (k + 0.5f);
                for (int floor = 0; floor < floors; floor++)
                {
                    if (floor == 0 && !float.IsNaN(doorOffset) && Mathf.Abs(offset - doorOffset) < 1.5f)
                        continue;

                    var point = origin + tangent * offset + Vector3.up * (floor * FloorHeight + 1.6f);
                    Window(mesh, p, point, normal, windowWidth, windowHeight, shutters);
                }
            }
        }

        // A door in the front wall (normal +Z) with its frame, handle, step and canopy. `wallZ` is the wall's position.
        public static void Door(TownMesh mesh, TownPalette p, Material leaf, float x, float wallZ, bool porchPosts)
        {
            mesh.Box(p.Concrete, new Vector3(x, 0.11f, wallZ + 0.4f), new Vector3(1.9f, 0.22f, 0.8f));
            mesh.Box(p.TrimWhite, new Vector3(x, 1.32f, wallZ + 0.04f), new Vector3(1.25f, 2.2f, 0.08f));
            mesh.Box(leaf, new Vector3(x, 1.28f, wallZ + 0.09f), new Vector3(0.95f, 2.05f, 0.05f));
            mesh.Box(p.MetalLight, new Vector3(x + 0.32f, 1.2f, wallZ + 0.13f), new Vector3(0.05f, 0.05f, 0.07f));
            mesh.Box(p.TrimDark, new Vector3(x, 2.55f, wallZ + 0.5f), new Vector3(1.9f, 0.1f, 1.0f));

            if (!porchPosts)
                return;

            for (int sign = -1; sign <= 1; sign += 2)
                mesh.Box(p.TrimWhite, new Vector3(x + sign * 0.85f, 1.38f, wallZ + 0.92f), new Vector3(0.1f, 2.32f, 0.1f));
        }

        // A brick chimney standing through the roof, with a cap.
        public static void Chimney(TownMesh mesh, TownPalette p, Vector3 position, float bottom, float top)
        {
            mesh.Box(p.Brick, new Vector3(position.x, (bottom + top) / 2f, position.z), new Vector3(0.6f, top - bottom, 0.6f));
            mesh.Box(p.Concrete, new Vector3(position.x, top + 0.04f, position.z), new Vector3(0.8f, 0.08f, 0.8f));
        }

        // A parked car. Its long axis is local Z, turned by `yaw`; `center` is on the ground under its middle.
        public static void Car(TownMesh mesh, TownPalette p, Material body, Vector3 center, float yaw)
        {
            var rotation = Quaternion.AngleAxis(yaw, Vector3.up);
            Vector3 At(float x, float y, float z) => center + rotation * new Vector3(x, y, z);

            mesh.Box(body, At(0, 0.62f, 0), new Vector3(1.8f, 0.62f, 4.2f), yaw);
            mesh.Box(p.Glass, At(0, 1.12f, -0.2f), new Vector3(1.6f, 0.5f, 2.3f), yaw);
            mesh.Box(body, At(0, 1.4f, -0.2f), new Vector3(1.62f, 0.06f, 2.1f), yaw);

            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                    mesh.Box(p.Tyre, At(x * 0.88f, 0.32f, z * 1.3f), new Vector3(0.22f, 0.64f, 0.64f), yaw);

                mesh.Box(p.Lamp, At(x * 0.6f, 0.7f, 2.11f), new Vector3(0.35f, 0.15f, 0.04f), yaw);
                mesh.Box(p.CarRed, At(x * 0.6f, 0.7f, -2.11f), new Vector3(0.35f, 0.15f, 0.04f), yaw);
            }
        }

        // A street lamp at `position` whose arm reaches toward `lean` (a horizontal direction).
        public static void StreetLamp(TownMesh mesh, TownPalette p, Vector3 position, Vector3 lean)
        {
            const float poleHeight = 4.8f;
            var yaw = YawOf(lean);

            mesh.Box(p.Concrete, position + Vector3.up * 0.15f, new Vector3(0.4f, 0.3f, 0.4f), yaw);
            mesh.Cylinder(p.MetalDark, position + Vector3.up * 0.3f, 0.09f, 0.06f, poleHeight, 6);
            mesh.Box(p.MetalDark, position + Vector3.up * (poleHeight + 0.28f) + lean * 0.6f, new Vector3(0.07f, 0.07f, 1.3f), yaw);
            mesh.Box(p.MetalLight, position + Vector3.up * (poleHeight + 0.2f) + lean * 1.25f, new Vector3(0.32f, 0.1f, 0.62f), yaw);
            mesh.Box(p.Lamp, position + Vector3.up * (poleHeight + 0.14f) + lean * 1.25f, new Vector3(0.24f, 0.04f, 0.5f), yaw);
        }

        // A park bench on the ground at `position`, its seat facing `facing` (a horizontal direction).
        public static void Bench(TownMesh mesh, TownPalette p, Vector3 position, Vector3 facing)
        {
            var yaw = YawOf(facing);
            var rotation = Quaternion.AngleAxis(yaw, Vector3.up);
            Vector3 At(float x, float y, float z) => position + rotation * new Vector3(x, y, z);

            mesh.Box(p.TrimDark, At(0f, 0.46f, 0f), new Vector3(1.7f, 0.07f, 0.45f), yaw);
            mesh.Box(p.TrimDark, At(0f, 0.8f, -0.2f), new Vector3(1.7f, 0.45f, 0.05f), yaw);
            foreach (var x in new[] { -0.75f, 0.75f })
                mesh.Box(p.MetalDark, At(x, 0.22f, 0f), new Vector3(0.08f, 0.44f, 0.4f), yaw);
        }

        // A concrete planter box with a clipped bush in it.
        public static void Planter(TownMesh mesh, TownPalette p, Vector3 position)
        {
            mesh.Box(p.Concrete, position + Vector3.up * 0.3f, new Vector3(1.8f, 0.6f, 1.8f));
            mesh.Box(p.Hedge, position + Vector3.up * 0.8f, new Vector3(1.6f, 0.6f, 1.6f));
        }

        // A hedge or a picket fence along local X from x0 to x1 at depth z.
        public static void YardEdge(TownMesh mesh, TownPalette p, bool hedge, float x0, float x1, float z)
        {
            var length = x1 - x0;
            if (length <= 0.1f)
                return;

            var middle = (x0 + x1) / 2f;
            if (hedge)
            {
                mesh.Box(p.Hedge, new Vector3(middle, 0.42f, z), new Vector3(length, 0.84f, 0.7f));
                return;
            }

            mesh.Box(p.FenceWood, new Vector3(middle, 0.5f, z), new Vector3(length, 0.78f, 0.04f));
            mesh.Box(p.TrimWhite, new Vector3(middle, 0.92f, z), new Vector3(length, 0.06f, 0.07f));
            var posts = Mathf.Max(2, Mathf.CeilToInt(length / 1.8f) + 1);
            for (int i = 0; i < posts; i++)
                mesh.Box(p.TrimWhite, new Vector3(Mathf.Lerp(x0, x1, i / (posts - 1f)), 0.5f, z), new Vector3(0.1f, 1.0f, 0.1f));
        }
    }
}
