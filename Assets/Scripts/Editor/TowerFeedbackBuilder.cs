using FullyVolted.Climbing;
using FullyVolted.Feedback;
using FullyVolted.Procedure;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace FullyVolted.EditorTools
{
    // The tower talks back and the controllers buzz. Sounds are made in code, like the wind and the chimes:
    // damped sine "rings" for the metal, and a resonator kicked by random impulses for the creak.
    internal static class TowerFeedbackBuilder
    {
        private const int SampleRate = 44100;
        private const int CreakVariants = 3;

        public static void Create(GameObject rig, Transform head, FallArrest fallArrest, ProcedureController controller,
            float topDeckHeight, string folder)
        {
            var hands = rig.GetComponentsInChildren<NearFarInteractor>(true);
            if (hands.Length == 0)
                Debug.LogWarning("[FullyVolted] No hand interactors found on the rig; haptics and rung sounds will not work.");

            var carabiners = Object.FindObjectsByType<Carabiner>();

            var haptics = new GameObject("Hand Haptics");
            var handHaptics = haptics.AddComponent<HandHaptics>();
            SetObjects(new SerializedObject(handHaptics), "hands", hands);

            var alerts = haptics.AddComponent<HapticAlerts>();
            var alertsSerialized = new SerializedObject(alerts);
            alertsSerialized.FindProperty("procedureController").objectReferenceValue = controller;
            alertsSerialized.FindProperty("handHaptics").objectReferenceValue = handHaptics;
            alertsSerialized.FindProperty("fallArrest").objectReferenceValue = fallArrest;
            alertsSerialized.ApplyModifiedPropertiesWithoutUndo();

            var root = new GameObject("Tower Feedback");
            var feedback = root.AddComponent<TowerFeedback>();
            var serialized = new SerializedObject(feedback);
            serialized.FindProperty("handHaptics").objectReferenceValue = handHaptics;
            serialized.FindProperty("head").objectReferenceValue = head;
            serialized.FindProperty("impactSource").objectReferenceValue = CreateSource(root.transform, "Impact Audio", 1f, 25f);
            serialized.FindProperty("creakSource").objectReferenceValue = CreateSource(root.transform, "Creak Audio", 2f, 40f);
            serialized.FindProperty("rungClip").objectReferenceValue = GetClip(folder, "Tower Rung", MakeRung);
            serialized.FindProperty("clipClip").objectReferenceValue = GetClip(folder, "Carabiner Clip", MakeClip);
            serialized.FindProperty("unclipClip").objectReferenceValue = GetClip(folder, "Carabiner Unclip", MakeUnclip);
            serialized.FindProperty("topHeight").floatValue = topDeckHeight;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var creaks = new Object[CreakVariants];
            for (int i = 0; i < CreakVariants; i++)
            {
                var variant = i;
                creaks[i] = GetClip(folder, $"Tower Creak {i + 1}", () => MakeCreak(variant));
            }

            serialized = new SerializedObject(feedback);
            SetObjects(serialized, "hands", hands);
            SetObjects(serialized, "carabiners", carabiners);
            SetObjects(serialized, "creakClips", creaks);
        }

        private static void SetObjects(SerializedObject target, string property, Object[] objects)
        {
            var array = target.FindProperty(property);
            array.arraySize = objects.Length;
            for (int i = 0; i < objects.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
            target.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AudioSource CreateSource(Transform parent, string name, float minDistance, float maxDistance)
        {
            var sourceObject = new GameObject(name);
            sourceObject.transform.SetParent(parent, false);

            var source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            // Heard where it happens: 3D, fading out with distance.
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            return source;
        }

        // ---- the sounds ----

        private static AudioClip GetClip(string folder, string clipName, System.Func<float[]> make)
        {
            if (!AssetDatabase.IsValidFolder($"{folder}/Audio"))
                AssetDatabase.CreateFolder(folder, "Audio");

            var path = $"{folder}/Audio/{clipName}.wav";
            var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (existing != null)
                return existing;

            WavWriter.Write(path, make(), SampleRate);
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        // Dull metal "tonk": three low rings that do not line up (that is what makes it sound like a pipe, not a note)
        // plus a short thump.
        private static float[] MakeRung()
        {
            var samples = new float[Mathf.RoundToInt(SampleRate * 0.4f)];
            AddRing(samples, 0f, 165f, 1f, 0.14f);
            AddRing(samples, 0f, 262f, 0.6f, 0.09f);
            AddRing(samples, 0f, 421f, 0.35f, 0.05f);
            AddNoise(samples, 0f, 0.004f, 0.5f, 1);
            return Normalised(samples, 0.8f);
        }

        // Bright, short metallic click, then a tiny second tick as the gate on the hook snaps shut.
        private static float[] MakeClip()
        {
            var samples = new float[Mathf.RoundToInt(SampleRate * 0.25f)];
            AddRing(samples, 0f, 2100f, 1f, 0.03f);
            AddRing(samples, 0f, 3350f, 0.6f, 0.02f);
            AddNoise(samples, 0f, 0.003f, 0.6f, 2);
            AddRing(samples, 0.05f, 1500f, 0.5f, 0.025f);
            AddNoise(samples, 0.05f, 0.002f, 0.3f, 3);
            return Normalised(samples, 0.8f);
        }

        // The same hook the other way: lower and softer, a single snap.
        private static float[] MakeUnclip()
        {
            var samples = new float[Mathf.RoundToInt(SampleRate * 0.2f)];
            AddRing(samples, 0f, 1400f, 1f, 0.035f);
            AddRing(samples, 0f, 2300f, 0.5f, 0.025f);
            AddNoise(samples, 0f, 0.003f, 0.5f, 4);
            return Normalised(samples, 0.7f);
        }

        // Steel groaning: a resonator (about 300-450 Hz) kicked by random impulses whose rate wanders, like a slow
        // stick-slip. Fades in and out smoothly. Each variant has its own length, pitch and seed.
        private static float[] MakeCreak(int variant)
        {
            var length = 1.1f + 0.35f * variant;
            var samples = new float[Mathf.RoundToInt(SampleRate * length)];
            var random = new System.Random(30 + variant);

            var frequency = 300f + 70f * variant;
            var radius = 0.995f;
            var coefficient1 = 2f * radius * Mathf.Cos(2f * Mathf.PI * frequency / SampleRate);
            var coefficient2 = -radius * radius;

            float y1 = 0f, y2 = 0f, smooth = 0f, phase = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                var time = i / (float)SampleRate;

                // Pulse rate wanders between about 12 and 60 per second.
                var rate = 36f + 24f * Mathf.Sin(time * (4f + variant)) * Mathf.Sin(time * 2.3f + variant);
                phase += rate / SampleRate;
                var kick = 0f;
                if (phase >= 1f)
                {
                    phase -= 1f;
                    kick = (float)(random.NextDouble() * 2.0 - 1.0) * 0.6f + 0.4f;
                }

                var y = kick + coefficient1 * y1 + coefficient2 * y2;
                y2 = y1;
                y1 = y;

                // Soften the harsh edge with a one-pole low-pass.
                smooth += 0.25f * (y - smooth);

                var envelope = Mathf.Pow(Mathf.Sin(Mathf.PI * time / length), 2f);
                samples[i] = smooth * envelope;
            }

            return Normalised(samples, 0.6f);
        }

        private static void AddRing(float[] samples, float startSeconds, float pitch, float level, float decaySeconds)
        {
            var start = Mathf.RoundToInt(startSeconds * SampleRate);
            for (int i = start; i < samples.Length; i++)
            {
                var time = (i - start) / (float)SampleRate;
                // 1 ms fade-in so the start never clicks in the speaker.
                var attack = Mathf.Clamp01(time / 0.001f);
                samples[i] += Mathf.Sin(2f * Mathf.PI * pitch * time) * level * attack * Mathf.Exp(-time / decaySeconds);
            }
        }

        private static void AddNoise(float[] samples, float startSeconds, float decaySeconds, float level, int seed)
        {
            var random = new System.Random(seed);
            var start = Mathf.RoundToInt(startSeconds * SampleRate);
            for (int i = start; i < samples.Length; i++)
            {
                var time = (i - start) / (float)SampleRate;
                if (time > decaySeconds * 6f)
                    break;
                samples[i] += (float)(random.NextDouble() * 2.0 - 1.0) * level * Mathf.Exp(-time / decaySeconds);
            }
        }

        private static float[] Normalised(float[] samples, float peakLevel)
        {
            var peak = 0.0001f;
            foreach (var sample in samples)
                peak = Mathf.Max(peak, Mathf.Abs(sample));
            for (int i = 0; i < samples.Length; i++)
                samples[i] = samples[i] / peak * peakLevel;
            return samples;
        }
    }
}
