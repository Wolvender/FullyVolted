using FullyVolted.Atmosphere;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FullyVolted.EditorTools
{
    // The extra lighting that makes the scene feel real: a warm colour balance and a red warning beacon.
    // All stock URP, cheap enough for VR.
    internal static class LightingEffects
    {
        // The profile is kept once created (so inspector tweaks survive), which means newer effects have to be
        // added to an existing one too.
        public static void EnsureColorBalance(VolumeProfile profile)
        {
            if (profile.Has<WhiteBalance>())
                return;

            var balance = profile.Add<WhiteBalance>();
            balance.temperature.Override(7f);
            AssetDatabase.AddObjectToAsset(balance, profile);
            EditorUtility.SetDirty(profile);
        }

        public static void AddTowerBeacon(Transform tower, string folder)
        {
            var renderers = tower.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;

            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);

            var beacon = new GameObject("Warning Beacon");
            beacon.transform.SetParent(tower, true);
            beacon.transform.position = new Vector3(bounds.center.x, bounds.max.y + 0.15f, bounds.center.z);

            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = "Bulb";
            bulb.transform.SetParent(beacon.transform, false);
            bulb.transform.localScale = Vector3.one * 0.35f;
            Object.DestroyImmediate(bulb.GetComponent<Collider>());
            var bulbRenderer = bulb.GetComponent<MeshRenderer>();
            bulbRenderer.sharedMaterial = GetBeaconMaterial(folder);
            bulbRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var lightObject = new GameObject("Glow");
            lightObject.transform.SetParent(beacon.transform, false);
            var glow = lightObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.15f, 0.1f);
            glow.range = 30f;
            glow.intensity = 0f;
            glow.shadows = LightShadows.None;

            var blinking = beacon.AddComponent<BlinkingBeacon>();
            var serialized = new SerializedObject(blinking);
            serialized.FindProperty("bulb").objectReferenceValue = bulbRenderer;
            serialized.FindProperty("glow").objectReferenceValue = glow;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material GetBeaconMaterial(string folder)
        {
            var path = $"{folder}/Beacon.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(0.25f, 0.02f, 0.02f));
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
