using UnityEditor;
using UnityEngine;

namespace FullyVolted.EditorTools
{
    // Downloaded packs usually ship Built-in "Standard" materials, which render magenta under URP.
    // This makes a URP/Lit copy (once, in Assets/Materials/Converted) so the scene builder never depends on
    // someone running Unity's material converter by hand. Opaque materials only.
    internal static class UrpMaterials
    {
        private const string Folder = "Assets/Materials/Converted";

        public static Material FromStandard(Material source)
        {
            if (source == null || source.shader == null || source.shader.name != "Standard")
                return source;

            if (source.HasProperty("_Mode") && source.GetFloat("_Mode") != 0f)
                Debug.LogWarning($"[FullyVolted] '{source.name}' is not opaque; the URP copy will be opaque.");

            var path = $"{Folder}/{source.name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Materials", "Converted");

            var converted = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = source.name };
            converted.SetColor("_BaseColor", source.GetColor("_Color"));
            converted.SetFloat("_Metallic", source.GetFloat("_Metallic"));
            converted.SetFloat("_Smoothness", source.GetFloat("_Glossiness"));

            Copy(source, converted, "_MainTex", "_BaseMap");
            if (Copy(source, converted, "_BumpMap", "_BumpMap"))
            {
                converted.SetFloat("_BumpScale", source.GetFloat("_BumpScale"));
                converted.EnableKeyword("_NORMALMAP");
            }

            if (Copy(source, converted, "_MetallicGlossMap", "_MetallicGlossMap"))
            {
                converted.SetFloat("_Smoothness", source.GetFloat("_GlossMapScale"));
                converted.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            if (Copy(source, converted, "_OcclusionMap", "_OcclusionMap"))
            {
                converted.SetFloat("_OcclusionStrength", source.GetFloat("_OcclusionStrength"));
                converted.EnableKeyword("_OCCLUSIONMAP");
            }

            AssetDatabase.CreateAsset(converted, path);
            return converted;
        }

        // Replaces every Standard material on the renderers under `root` with its URP copy.
        public static void ConvertAll(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = FromStandard(materials[i]);
                renderer.sharedMaterials = materials;
            }
        }

        private static bool Copy(Material source, Material target, string from, string to)
        {
            if (!source.HasProperty(from))
                return false;

            var texture = source.GetTexture(from);
            if (texture == null)
                return false;

            target.SetTexture(to, texture);
            target.SetTextureScale(to, source.GetTextureScale(from));
            target.SetTextureOffset(to, source.GetTextureOffset(from));
            return true;
        }
    }
}
