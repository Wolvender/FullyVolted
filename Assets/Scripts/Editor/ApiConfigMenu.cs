using System.IO;
using FullyVolted.Results;
using UnityEditor;
using UnityEngine;

namespace FullyVolted.EditorTools
{
    public static class ApiConfigMenu
    {
        // Must match ClimbSceneBuilder, which wires this asset into the scene. Gitignored (holds the key).
        private const string ConfigPath = "Assets/Settings/Api Config.asset";
        private const string ApiUrl = "http://localhost:8000";

        [MenuItem("Tools/FullyVolted/Create API Config")]
        public static void CreateOrSelect()
        {
            var config = AssetDatabase.LoadAssetAtPath<ApiConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<ApiConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            // The key is copied from Api/.env on this machine, so it never has to be typed or pasted.
            var serialized = new SerializedObject(config);
            serialized.FindProperty("baseUrl").stringValue = ApiUrl;
            var key = ReadKeyFromEnvFile();
            if (key != null)
                serialized.FindProperty("apiKey").stringValue = key;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            Debug.Log(key != null
                ? "[FullyVolted] Api Config ready (key read from Api/.env). Rebuild the scene to use it."
                : "[FullyVolted] Api Config created, but Api/.env has no API_KEY. Paste it into Api Key by hand.", config);

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        // Api/ sits next to Assets/ in the project folder.
        private static string ReadKeyFromEnvFile()
        {
            var envPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Api", ".env");
            if (!File.Exists(envPath))
                return null;

            foreach (var line in File.ReadAllLines(envPath))
            {
                if (line.StartsWith("API_KEY="))
                    return line.Substring("API_KEY=".Length).Trim();
            }

            return null;
        }
    }
}
