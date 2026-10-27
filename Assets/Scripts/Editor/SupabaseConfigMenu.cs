using FullyVolted.Results;
using UnityEditor;
using UnityEngine;

namespace FullyVolted.EditorTools
{
    public static class SupabaseConfigMenu
    {
        // Must match ClimbSceneBuilder, which wires this asset into the scene. Gitignored.
        private const string ConfigPath = "Assets/Settings/Supabase Config.asset";
        private const string ProjectUrl = "https://iujtcyicuvommnatxcpt.supabase.co";

        [MenuItem("Tools/FullyVolted/Create Supabase Config")]
        public static void CreateOrSelect()
        {
            var config = AssetDatabase.LoadAssetAtPath<SupabaseConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<SupabaseConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);

                // Only the URL: the key is pasted in by hand so it never ends up in the (public) repo.
                var serialized = new SerializedObject(config);
                serialized.FindProperty("projectUrl").stringValue = ProjectUrl;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();

                Debug.Log("[FullyVolted] Created " + ConfigPath + ". Paste the publishable key into Anon Key.", config);
            }

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }
    }
}
