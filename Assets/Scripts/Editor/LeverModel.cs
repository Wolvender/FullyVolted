using System.Linq;
using FullyVolted.Feedback;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace FullyVolted.EditorTools
{
    // The downloaded power lever as the "Switch Off" control at the top. Editor-only scene building: grabbing it
    // is stock XRI (the procedure listens to the interactable), and LeverPull plays the model's own pull animation.
    internal static class LeverModel
    {
        private const string Folder = "Assets/Models/PowerLever";
        private const string ModelPath = Folder + "/LeverSwitch.fbx";
        private const string MaterialPath = "Assets/Materials/Power Lever.mat";

        // The lever box is about as wide as a person's hand span times two.
        private const float Size = 0.5f;
        private const float ColliderPadding = 0.08f;

        // Returns the interactable on a wrapper named `objectName`, or null when the model is not in the project
        // (the caller then falls back to the plain cube). `rotation` looks the way the player walks up to it.
        public static XRBaseInteractable Create(string objectName, Vector3 position, Quaternion rotation, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[FullyVolted] No model at '{ModelPath}'; {objectName} falls back to a cube.");
                return null;
            }

            EnsureGenericRig();

            // Built at the origin with no rotation, so the renderer bounds are the model's own.
            var wrapper = new GameObject(objectName);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, wrapper.transform);

            var material = GetMaterial();
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = material;

            FaceTheFront(model);

            var colliderSize = ModelFit.FitCentered(model, Size);
            wrapper.AddComponent<BoxCollider>().size = colliderSize + Vector3.one * ColliderPadding;

            var interactable = wrapper.AddComponent<XRSimpleInteractable>();
            AddPull(wrapper, model, interactable);

            wrapper.transform.SetPositionAndRotation(position, rotation);
            wrapper.transform.SetParent(parent, true);
            return interactable;
        }

        // The lever's pull-down animation drives bones, which needs an Animator on the model.
        private static void EnsureGenericRig()
        {
            if (AssetImporter.GetAtPath(ModelPath) is ModelImporter importer &&
                importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.SaveAndReimport();
            }
        }

        // Turns the model so the side the handle sticks out of looks at the player (local -Z; the wrapper's
        // forward is the way the player walks up). The back plate is the thinnest part, the handle is on the front.
        private static void FaceTheFront(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            var plate = renderers.FirstOrDefault(renderer => renderer.name == "Panel");
            var handle = renderers.FirstOrDefault(renderer => renderer.name == "Handle");
            if (plate == null || handle == null)
            {
                Debug.LogWarning("[FullyVolted] The lever model has no 'Panel' and 'Handle' parts; its facing is left as imported.");
                return;
            }

            var size = plate.bounds.size;
            var axis = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
            var towardHandle = handle.bounds.center[axis] - plate.bounds.center[axis];
            if (Mathf.Approximately(towardHandle, 0f))
                towardHandle = 1f;

            var front = Vector3.zero;
            front[axis] = Mathf.Sign(towardHandle);
            model.transform.rotation = Quaternion.FromToRotation(front, Vector3.back);
        }

        private static void AddPull(GameObject wrapper, GameObject model, XRBaseInteractable interactable)
        {
            if (!model.TryGetComponent<Animator>(out var animator))
                animator = model.AddComponent<Animator>();

            // The clip lives inside the FBX; the "__preview__" entry is Unity's own copy for the importer window.
            var clip = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .FirstOrDefault(candidate => !candidate.name.StartsWith("__preview__"));
            if (clip == null)
                Debug.LogWarning("[FullyVolted] No animation in the lever model; it will not move when grabbed.");

            var pull = wrapper.AddComponent<LeverPull>();
            var serialized = new SerializedObject(pull);
            serialized.FindProperty("lever").objectReferenceValue = interactable;
            serialized.FindProperty("animator").objectReferenceValue = animator;
            serialized.FindProperty("pullClip").objectReferenceValue = clip;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material GetMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
                return existing;

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0.5f);
            material.SetFloat("_Smoothness", 0.45f);

            AssignTexture(material, "_BaseMap", "lever_basecolor.png", false);
            AssignTexture(material, "_BumpMap", "lever_normal.png", true);

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
        }
    }
}
