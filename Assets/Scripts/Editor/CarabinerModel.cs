using UnityEditor;
using UnityEngine;

namespace FullyVolted.EditorTools
{
    // The downloaded "Security Hook" as the carabiner's look. Everything here is Editor-only scene building:
    // the grab, clip and holster behaviour stays on the wrapper object that LanyardBuilder adds components to.
    internal static class CarabinerModel
    {
        private const string Folder = "Assets/Models/SecurityHook";
        private const string ModelPath = Folder + "/SecurityHook.obj";
        private const string MaterialPath = "Assets/Materials/Carabiner Hook.mat";

        // The model is ~31cm tall at its native scale; a real snap hook on a lanyard is closer to this.
        private const float HookLength = 0.18f;

        // Returns a wrapper named `objectName` with the model inside and a box collider around it, or null
        // when the model is not in the project (LanyardBuilder then falls back to the plain cube).
        public static GameObject Create(string objectName)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[FullyVolted] No model at '{ModelPath}'; carabiners fall back to cubes.");
                return null;
            }

            var wrapper = new GameObject(objectName);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, wrapper.transform);

            var material = GetMaterial();
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = material;

            var size = ModelFit.FitCentered(model, HookLength);
            wrapper.AddComponent<BoxCollider>().size = size;
            return wrapper;
        }

        private static Material GetMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
                return existing;

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0.8f);
            material.SetFloat("_Smoothness", 0.55f);

            AssignTexture(material, "_BaseMap", "hook_color.png", false);
            AssignTexture(material, "_BumpMap", "hook_normals.png", true);
            AssignTexture(material, "_OcclusionMap", "hook_ambocc.png", false);

            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        // Textures imported on a normal-map import type are decoded correctly; anything else would look flat.
        private static void AssignTexture(Material material, string property, string fileName, bool isNormalMap)
        {
            var path = $"{Folder}/{fileName}";

            if (isNormalMap && AssetImporter.GetAtPath(path) is TextureImporter importer &&
                importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
                return;

            material.SetTexture(property, texture);
            if (isNormalMap)
                material.EnableKeyword("_NORMALMAP");
            if (property == "_OcclusionMap")
                material.EnableKeyword("_OCCLUSIONMAP");
        }
    }
}
