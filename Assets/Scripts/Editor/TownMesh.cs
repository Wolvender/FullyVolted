using System.Collections.Generic;
using UnityEngine;

namespace FullyVolted.EditorTools
{
    // Flat-shaded low-poly geometry with one submesh per material, for the town. Every face is given the direction
    // it must face (outward), and its winding is flipped to match, so no face can come out inside-out.
    internal sealed class TownMesh
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Material> materials = new List<Material>();
        private readonly List<List<int>> triangles = new List<List<int>>();

        public Material[] Materials => materials.ToArray();
        public bool IsEmpty => vertices.Count == 0;

        public static Vector3 Polar(Vector3 center, float radius, float angleRadians, float y)
        {
            return new Vector3(center.x + Mathf.Cos(angleRadians) * radius, y, center.z + Mathf.Sin(angleRadians) * radius);
        }

        public void Triangle(Material material, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            // Unity draws the side where a -> b -> c runs clockwise; that side's normal is cross(b - a, c - a).
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f)
                (b, c) = (c, b);

            var list = TrianglesFor(material);
            var first = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            list.Add(first);
            list.Add(first + 1);
            list.Add(first + 2);
        }

        public void Quad(Material material, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            Triangle(material, a, b, c, outward);
            Triangle(material, a, c, d, outward);
        }

        // A box turned by `yaw` degrees around its own centre. Its local +Z is where the front faces.
        // The underside is left out (nobody sees it) unless asked for.
        public void Box(Material material, Vector3 center, Vector3 size, float yaw = 0f, bool bottom = false)
        {
            var rotation = Quaternion.AngleAxis(yaw, Vector3.up);
            var half = size * 0.5f;
            Vector3 P(float x, float y, float z) => center + rotation * new Vector3(x * half.x, y * half.y, z * half.z);
            Vector3 N(float x, float y, float z) => rotation * new Vector3(x, y, z);

            Quad(material, P(-1, -1, 1), P(-1, 1, 1), P(1, 1, 1), P(1, -1, 1), N(0, 0, 1));
            Quad(material, P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), P(-1, -1, -1), N(0, 0, -1));
            Quad(material, P(1, -1, 1), P(1, 1, 1), P(1, 1, -1), P(1, -1, -1), N(1, 0, 0));
            Quad(material, P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), P(-1, -1, 1), N(-1, 0, 0));
            Quad(material, P(-1, 1, 1), P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), N(0, 1, 0));
            if (bottom)
                Quad(material, P(-1, -1, -1), P(-1, -1, 1), P(1, -1, 1), P(1, -1, -1), N(0, -1, 0));
        }

        // A flat rectangle lying on the ground (facing up), `sizeX` by `sizeZ` before it is turned by `yaw`.
        public void Flat(Material material, Vector3 center, float sizeX, float sizeZ, float yaw = 0f)
        {
            var rotation = Quaternion.AngleAxis(yaw, Vector3.up);
            Vector3 P(float x, float z) => center + rotation * new Vector3(x * sizeX * 0.5f, 0f, z * sizeZ * 0.5f);
            Quad(material, P(-1, -1), P(-1, 1), P(1, 1), P(1, -1), Vector3.up);
        }

        // A flat ring (or a disc when `inner` is 0) lying on the ground.
        public void Ring(Material material, Vector3 center, float inner, float outer, float y, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                var angle0 = i * Mathf.PI * 2f / segments;
                var angle1 = (i + 1) * Mathf.PI * 2f / segments;
                var outer0 = Polar(center, outer, angle0, y);
                var outer1 = Polar(center, outer, angle1, y);
                if (inner <= 0f)
                    Triangle(material, new Vector3(center.x, y, center.z), outer0, outer1, Vector3.up);
                else
                    Quad(material, Polar(center, inner, angle0, y), outer0, outer1, Polar(center, inner, angle1, y), Vector3.up);
            }
        }

        // A round column standing on `baseCenter`, optionally tapering to `topRadius`, with a lid on top.
        public void Cylinder(Material material, Vector3 baseCenter, float bottomRadius, float topRadius, float height, int sides)
        {
            var top = baseCenter + Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                var angle0 = i * Mathf.PI * 2f / sides;
                var angle1 = (i + 1) * Mathf.PI * 2f / sides;
                var middle = new Vector3(Mathf.Cos((angle0 + angle1) * 0.5f), 0f, Mathf.Sin((angle0 + angle1) * 0.5f));
                Quad(material,
                    Polar(baseCenter, bottomRadius, angle0, baseCenter.y), Polar(top, topRadius, angle0, top.y),
                    Polar(top, topRadius, angle1, top.y), Polar(baseCenter, bottomRadius, angle1, baseCenter.y), middle);
                if (topRadius > 0.001f)
                    Triangle(material, top, Polar(top, topRadius, angle0, top.y), Polar(top, topRadius, angle1, top.y), Vector3.up);
            }
        }

        // A pitched roof with its ridge along local Z, sitting on wall tops at `center`. The roof hugs the walls and
        // overhangs them by `overhang` all round (the eaves dip below the wall tops by exactly the slope's amount).
        // The gable triangles are in `gable` (the wall colour); the roof is a thin sheet, so it also gets an underside.
        public void GableRoof(Material roof, Material underside, Material gable, Vector3 center, float width, float depth,
            float rise, float overhang, float yaw = 0f)
        {
            var rotation = Quaternion.AngleAxis(yaw, Vector3.up);
            Vector3 P(float x, float y, float z) => center + rotation * new Vector3(x, y, z);
            Vector3 N(float x, float y, float z) => rotation * new Vector3(x, y, z);

            var halfWidth = width * 0.5f;
            var eaveX = halfWidth + overhang;
            var eaveZ = depth * 0.5f + overhang;
            var drop = rise * overhang / halfWidth;

            var left = N(-rise, halfWidth, 0f);
            var right = N(rise, halfWidth, 0f);

            Quad(roof, P(-eaveX, -drop, -eaveZ), P(-eaveX, -drop, eaveZ), P(0, rise, eaveZ), P(0, rise, -eaveZ), left);
            Quad(roof, P(eaveX, -drop, eaveZ), P(eaveX, -drop, -eaveZ), P(0, rise, -eaveZ), P(0, rise, eaveZ), right);
            Quad(underside, P(-eaveX, -drop, -eaveZ), P(-eaveX, -drop, eaveZ), P(0, rise, eaveZ), P(0, rise, -eaveZ), -left);
            Quad(underside, P(eaveX, -drop, eaveZ), P(eaveX, -drop, -eaveZ), P(0, rise, -eaveZ), P(0, rise, eaveZ), -right);

            Triangle(gable, P(-halfWidth, 0, depth * 0.5f), P(halfWidth, 0, depth * 0.5f), P(0, rise, depth * 0.5f), N(0, 0, 1));
            Triangle(gable, P(halfWidth, 0, -depth * 0.5f), P(-halfWidth, 0, -depth * 0.5f), P(0, rise, -depth * 0.5f), N(0, 0, -1));
        }

        // Copies another mesh into this one, moved to `position` and turned by `yaw`. Lets a whole town be a handful
        // of big meshes (few draw calls) while each building is still modelled around its own middle.
        public void Append(TownMesh source, Vector3 position, float yaw)
        {
            var rotation = Quaternion.AngleAxis(yaw, Vector3.up);
            var offset = vertices.Count;
            foreach (var vertex in source.vertices)
                vertices.Add(position + rotation * vertex);

            for (int i = 0; i < source.materials.Count; i++)
            {
                var list = TrianglesFor(source.materials[i]);
                foreach (var index in source.triangles[i])
                    list.Add(offset + index);
            }
        }

        public Bounds LocalBounds()
        {
            if (vertices.Count == 0)
                return new Bounds();

            var bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (var vertex in vertices)
                bounds.Encapsulate(vertex);
            return bounds;
        }

        public Mesh ToMesh(string meshName)
        {
            var mesh = new Mesh { name = meshName };
            // The index format first: changing it afterwards wipes the submeshes. The merged town is far past the
            // 65,535 vertices a 16-bit mesh can address.
            if (vertices.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.subMeshCount = materials.Count;
            mesh.SetVertices(vertices);
            for (int i = 0; i < triangles.Count; i++)
                mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private List<int> TrianglesFor(Material material)
        {
            var index = materials.IndexOf(material);
            if (index < 0)
            {
                materials.Add(material);
                triangles.Add(new List<int>());
                index = materials.Count - 1;
            }

            return triangles[index];
        }
    }
}
