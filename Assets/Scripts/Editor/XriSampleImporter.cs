using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FullyVolted.EditorTools
{
    public static class XriSampleImporter
    {
        private const string PackageName = "com.unity.xr.interaction.toolkit";

        private static readonly string[] WantedSamples =
        {
            "Starter Assets",
            "XR Interaction Simulator",
        };

        [MenuItem("Tools/FullyVolted/Import XRI Samples")]
        public static void ImportSamples()
        {
            var found = new List<string>();
            var missing = new List<string>(WantedSamples);

            foreach (var sample in UnityEditor.PackageManager.UI.Sample.FindByPackage(PackageName, string.Empty))
            {
                if (!missing.Contains(sample.displayName))
                    continue;

                if (sample.isImported || sample.Import())
                {
                    found.Add(sample.displayName);
                    missing.Remove(sample.displayName);
                }
            }

            AssetDatabase.Refresh();

            if (found.Count > 0)
                Debug.Log("[FullyVolted] XRI samples available: " + string.Join(", ", found));

            if (missing.Count > 0)
                Debug.LogWarning("[FullyVolted] Could not import: " + string.Join(", ", missing) +
                                 ". Import them manually via Window > Package Manager > XR Interaction Toolkit > Samples.");
        }
    }
}
