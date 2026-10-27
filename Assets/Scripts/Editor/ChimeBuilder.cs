using System.IO;
using FullyVolted.Feedback;
using FullyVolted.Procedure;
using UnityEditor;
using UnityEngine;

namespace FullyVolted.EditorTools
{
    // The step chimes, made in code: two soft bell-like notes. Up for correct, down for incorrect, so you can tell
    // them apart without looking. Pure sine tones with a gentle start and a long fade: nothing sharp or buzzing.
    internal static class ChimeBuilder
    {
        private const int SampleRate = 44100;

        private const float C6 = 1046.5f;
        private const float E6 = 1318.5f;
        private const float E5 = 659.3f;
        private const float C5 = 523.3f;

        public static void CreateStepChimes(ProcedureController controller, string folder)
        {
            var chimeObject = new GameObject("Step Chimes");

            var source = chimeObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            // Feedback for you, not a sound in the world: plain 2D.
            source.spatialBlend = 0f;

            var chimes = chimeObject.AddComponent<StepChimes>();
            var serialized = new SerializedObject(chimes);
            serialized.FindProperty("procedureController").objectReferenceValue = controller;
            serialized.FindProperty("source").objectReferenceValue = source;
            serialized.FindProperty("correctChime").objectReferenceValue = GetClip(folder, "Correct Chime", C6, E6);
            serialized.FindProperty("incorrectChime").objectReferenceValue = GetClip(folder, "Incorrect Chime", E5, C5);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AudioClip GetClip(string folder, string clipName, float first, float second)
        {
            if (!AssetDatabase.IsValidFolder($"{folder}/Audio"))
                AssetDatabase.CreateFolder(folder, "Audio");

            var path = $"{folder}/Audio/{clipName}.wav";
            var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (existing != null)
                return existing;

            var samples = new float[Mathf.RoundToInt(SampleRate * 1.1f)];
            AddNote(samples, first, 0f);
            AddNote(samples, second, 0.14f);

            var peak = 0.0001f;
            foreach (var sample in samples)
                peak = Mathf.Max(peak, Mathf.Abs(sample));
            for (int i = 0; i < samples.Length; i++)
                samples[i] = samples[i] / peak * 0.5f;

            WriteWav(path, samples);
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        // One soft bell note: the pitch, a quiet octave above it for sparkle, a 12 ms fade-in so it never clicks,
        // and a smooth fade-out.
        private static void AddNote(float[] samples, float pitch, float startSeconds)
        {
            var start = Mathf.RoundToInt(startSeconds * SampleRate);
            for (int i = start; i < samples.Length; i++)
            {
                var time = (i - start) / (float)SampleRate;
                var attack = Mathf.Clamp01(time / 0.012f);
                var fade = Mathf.Exp(-time / 0.28f);
                var tone = Mathf.Sin(2f * Mathf.PI * pitch * time) + 0.18f * Mathf.Sin(2f * Mathf.PI * pitch * 2f * time) * Mathf.Exp(-time / 0.12f);
                samples[i] += tone * attack * fade;
            }
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
