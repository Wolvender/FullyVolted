using System.IO;
using FullyVolted.Atmosphere;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FullyVolted.EditorTools
{
    // Wind you can hear and see. Heard: a looping wind sound made in code, played louder the higher you climb.
    // Seen: long thin streaks that drift through the air, like the wind lines in many games.
    internal static class WindBuilder
    {
        private const int SampleRate = 22050;
        private const float ClipSeconds = 14f;

        private static readonly Vector3 WindDirection = new Vector3(1f, 0.03f, 0.35f).normalized;

        // ---- seen: wisps ----

        public static void CreateWisps(string folder)
        {
            var wisps = new GameObject("Wind Wisps");
            // The particles are emitted along the object's forward, so face it down the wind.
            wisps.transform.SetPositionAndRotation(ClimbSceneBuilder.TowerPosition + Vector3.up * 20f, Quaternion.LookRotation(WindDirection));

            var particles = wisps.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            // Slow on purpose: a wisp draws itself across the sky over several seconds and then fades away.
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startColor = new Color(1f, 1f, 1f, 0.3f);
            main.gravityModifier = 0f;

            var emission = particles.emission;
            emission.rateOverTime = 8f;

            // A wide, tall box around the tower: wisps start low and high, near and far.
            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(160f, 45f, 160f);

            // Gentle swirling, so the streaks curve instead of flying straight.
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = 1.6f;
            noise.frequency = 0.12f;
            noise.scrollSpeed = 0.3f;
            noise.damping = true;
            noise.octaveCount = 2;

            // The streak is the particle's trail; the particle itself is invisible. While the particle flies, its
            // trail grows out behind it (the wisp draws itself from its tail to its tip). When the particle dies the
            // trail stays and its tail catches up with the tip (the wisp fades away the same way, end to tip).
            var trails = particles.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.PerParticle;
            trails.ratio = 1f;
            trails.lifetime = 0.45f;
            trails.minVertexDistance = 0.5f;
            trails.worldSpace = true;
            trails.dieWithParticles = false;
            trails.inheritParticleColor = true;
            trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
            trails.sizeAffectsWidth = true;

            // Pointed and see-through at both ends, solid in the middle, so neither end ever shows a hard edge.
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f)));
            var ends = new Gradient();
            ends.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            trails.colorOverTrail = ends;

            var renderer = wisps.GetComponent<ParticleSystemRenderer>();
            var material = GetWispMaterial(folder);
            renderer.renderMode = ParticleSystemRenderMode.None;
            renderer.sharedMaterial = material;
            renderer.trailMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            particles.Play();
        }

        private static Material GetWispMaterial(string folder)
        {
            var path = $"{folder}/Wind Wisp.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }

            // What URP's own material inspector does for "Transparent": alpha blending, no depth write, drawn late.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetTexture("_BaseMap", GetStreakTexture(folder));
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        // A soft streak: fades out at both ends and toward the edges, so it reads as moving air and not as a line.
        private static Texture2D GetStreakTexture(string folder)
        {
            var path = $"{folder}/Textures/Wind Streak.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
                return existing;

            const int width = 128;
            const int height = 32;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                var across = 1f - Mathf.Abs(y / (height - 1f) * 2f - 1f);
                for (int x = 0; x < width; x++)
                {
                    var along = Mathf.Sin(x / (width - 1f) * Mathf.PI);
                    pixels[y * width + x] = new Color(1f, 1f, 1f, Mathf.Pow(across, 1.6f) * along);
                }
            }

            texture.SetPixels(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---- heard: wind sound ----

        public static void CreateAudio(Transform head, float topDeckHeight, string folder)
        {
            var windObject = new GameObject("Wind Audio");

            var source = windObject.AddComponent<AudioSource>();
            source.clip = GetWindClip(folder);
            source.loop = true;
            source.playOnAwake = true;
            // Wind is all around you, not in one place: plain 2D sound.
            source.spatialBlend = 0f;
            source.volume = 0.15f;

            var lowPass = windObject.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = 1400f;

            var wind = windObject.AddComponent<WindAudio>();
            var serialized = new SerializedObject(wind);
            serialized.FindProperty("head").objectReferenceValue = head;
            serialized.FindProperty("source").objectReferenceValue = source;
            serialized.FindProperty("lowPass").objectReferenceValue = lowPass;
            serialized.FindProperty("topHeight").floatValue = topDeckHeight;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (head == null)
                Debug.LogWarning("[FullyVolted] No player head found; the wind sound will stay silent.");
        }

        // Wind made from noise: a low rumble plus a swishing band of air whose pitch drifts, with slow gusts.
        // The ends are crossfaded, so the loop has no click.
        private static AudioClip GetWindClip(string folder)
        {
            var path = $"{folder}/Audio/Wind Loop.wav";
            var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (existing != null)
                return existing;

            if (!AssetDatabase.IsValidFolder($"{folder}/Audio"))
                AssetDatabase.CreateFolder(folder, "Audio");

            var length = Mathf.RoundToInt(SampleRate * ClipSeconds);
            var fade = SampleRate * 2;
            var raw = new float[length + fade];
            var random = new System.Random(5);

            float rumble = 0f, low = 0f, band = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                var white = (float)(random.NextDouble() * 2.0 - 1.0);
                var time = i / (float)SampleRate;

                // Deep rumble: noise through a slow low-pass.
                rumble += 0.015f * (white - rumble);

                // Swishing air: a resonant band-pass whose centre wanders between about 350 and 700 Hz.
                var centre = 520f + 170f * Mathf.Sin(time * Mathf.PI * 2f / ClipSeconds * 3f);
                var f = 2f * Mathf.Sin(Mathf.PI * centre / SampleRate);
                var high = white - low - 0.35f * band;
                band += f * high;
                low += f * band;

                // Slow gusts, whole cycles per clip so they line up at the loop point.
                var gust = 0.62f + 0.22f * Mathf.Sin(time * Mathf.PI * 2f / ClipSeconds * 2f + 1.1f) +
                           0.16f * Mathf.Sin(time * Mathf.PI * 2f / ClipSeconds * 5f + 2.3f);

                raw[i] = (rumble * 7f + band * 0.9f) * gust;
            }

            var samples = new float[length];
            var peak = 0.0001f;
            for (int i = 0; i < length; i++)
            {
                // Crossfade the extra tail into the start, so the end flows back into the beginning.
                samples[i] = i < fade
                    ? Mathf.Lerp(raw[length + i], raw[i], i / (float)fade)
                    : raw[i];
                peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            }

            for (int i = 0; i < length; i++)
                samples[i] = samples[i] / peak * 0.9f;

            WriteWav(path, samples);
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                importer.forceToMono = true;
                importer.loadInBackground = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        // 16-bit mono PCM, the simplest WAV there is.
        private static void WriteWav(string path, float[] samples)
        {
            using (var stream = new FileStream(path, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                var dataSize = samples.Length * 2;
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataSize);
                writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(SampleRate);
                writer.Write(SampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataSize);
                foreach (var sample in samples)
                    writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
            }
        }
    }
}
