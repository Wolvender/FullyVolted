using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FullyVolted.EditorTools
{
    // The world around the tower: sun, sky, fog, post-processing and the terrain. Stock Unity and URP only.
    // Shared, so the test landscape scene and (later) the climb scene get the exact same look.
    internal static class EnvironmentBuilder
    {
        public const string Folder = "Assets/Landscape";

        // The haze colour doubles as the sky's ground colour, so the horizon melts into the fog.
        private static readonly Color HazeColor = new Color(0.70f, 0.78f, 0.86f);
        private const float FogDensity = 0.0065f;

        public static void Create()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "Landscape");

            var sun = CreateSun();
            ApplySky(sun);
            ApplyFog();
            CreatePostProcessing();
            TerrainGenerator.Create(Folder);
            WindBuilder.CreateWisps(Folder);
        }

        // The rig's camera must draw the sky and run post-processing, or none of this shows.
        public static void PrepareCamera(Camera camera)
        {
            if (camera == null)
                return;

            camera.clearFlags = CameraClearFlags.Skybox;
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, 1500f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        private static Light CreateSun()
        {
            var sunObject = new GameObject("Sun");
            // Low and from the side, like late afternoon: long slanting shadows, and the sun sits low in the sky.
            sunObject.transform.rotation = Quaternion.Euler(17f, 55f, 0f);

            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.86f, 0.58f);
            sun.intensity = 1.45f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            return sun;
        }

        private static void ApplySky(Light sun)
        {
            // Updated in place so every scene that uses the sky keeps its reference.
            var path = $"{Folder}/Sky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            var isNew = sky == null;
            if (isNew)
                sky = new Material(Shader.Find("Skybox/Procedural"));

            sky.SetFloat("_SunSize", 0.045f);
            sky.SetFloat("_SunSizeConvergence", 5f);
            sky.SetFloat("_AtmosphereThickness", 1.05f);
            sky.SetFloat("_Exposure", 1.25f);
            sky.SetColor("_SkyTint", new Color(0.52f, 0.58f, 0.68f));
            sky.SetColor("_GroundColor", HazeColor);
            if (isNew)
                AssetDatabase.CreateAsset(sky, path);
            else
                EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            // Light from the sky itself, so shadowed sides are bluish instead of black.
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            DynamicGI.UpdateEnvironment();
        }

        private static void ApplyFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = FogDensity;
            RenderSettings.fogColor = HazeColor;
        }

        private static VolumeProfile CreateProfile(string path)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            // Few, cheap effects on purpose: the frame budget in VR is 11.1ms. No vignette (it reads as tunnel vision).
            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.ACES);

            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.25f);

            var color = profile.Add<ColorAdjustments>();
            color.contrast.Override(8f);
            color.saturation.Override(10f);

            // Volume components are ScriptableObjects too - they only survive a save as sub-assets.
            AssetDatabase.AddObjectToAsset(tonemapping, profile);
            AssetDatabase.AddObjectToAsset(bloom, profile);
            AssetDatabase.AddObjectToAsset(color, profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void CreatePostProcessing()
        {
            // Kept as it is once it exists, so a value tuned in the inspector survives a rebuild and every
            // scene keeps pointing at the same asset. Delete the asset to get the defaults back.
            var path = $"{Folder}/Environment Volume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
                profile = CreateProfile(path);
            LightingEffects.EnsureColorBalance(profile);

            var volumeObject = new GameObject("Environment Volume");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }
    }
}
