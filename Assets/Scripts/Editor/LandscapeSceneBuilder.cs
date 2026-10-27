using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FullyVolted.EditorTools
{
    // A separate scene to judge the environment on its own: the rig, the tower as a plain model (not climbable),
    // and the sky, fog, post-processing and terrain from EnvironmentBuilder. The climb scene is untouched.
    public static class LandscapeSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Landscape.unity";

        [MenuItem("Tools/FullyVolted/Build Landscape Test Scene")]
        public static void BuildLandscapeScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            EnvironmentBuilder.Create();
            CreateRig();
            CreateTowerModel();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log("[FullyVolted] Built landscape test scene at " + ScenePath);
        }

        private static void CreateRig()
        {
            ClimbSceneBuilder.EnsureInteractionManager();

            var rigPrefab = ClimbSceneBuilder.LoadPrefab(ClimbSceneBuilder.RigPrefabName);
            if (rigPrefab == null)
            {
                // No rig: a plain camera, so the scene can still be looked at from Play mode.
                Debug.LogWarning("[FullyVolted] No '" + ClimbSceneBuilder.RigPrefabName + "' prefab; using a plain camera.");
                var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                cameraObject.transform.SetPositionAndRotation(
                    ClimbSceneBuilder.RigStartPosition + Vector3.up * 1.7f, ClimbSceneBuilder.RigStartRotation);
                EnvironmentBuilder.PrepareCamera(cameraObject.AddComponent<Camera>());
                return;
            }

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            rig.transform.SetPositionAndRotation(ClimbSceneBuilder.RigStartPosition, ClimbSceneBuilder.RigStartRotation);
            EnvironmentBuilder.PrepareCamera(rig.GetComponentInChildren<Camera>(true));
        }

        // Only for judging scale and how it sits in the landscape. The real climb setup lives in ClimbSceneBuilder.
        private static void CreateTowerModel()
        {
            var pack = AssetDatabase.LoadAssetAtPath<GameObject>(ClimbSceneBuilder.TowerPackPath);
            if (pack == null)
            {
                Debug.LogWarning("[FullyVolted] Tower pack not found at " + ClimbSceneBuilder.TowerPackPath + "; the landscape has no tower.");
                return;
            }

            var packInstance = Object.Instantiate(pack);
            var tower = ClimbSceneBuilder.FindChild(packInstance.transform,
                child => child.name.StartsWith(ClimbSceneBuilder.TowerNodePrefix));
            if (tower != null)
            {
                var root = new GameObject("Radio Tower").transform;
                root.position = ClimbSceneBuilder.TowerPosition;
                tower.SetParent(root, true);
                tower.localPosition = Vector3.zero;
            }
            else
            {
                Debug.LogWarning("[FullyVolted] No '" + ClimbSceneBuilder.TowerNodePrefix + "' node in the tower pack.");
            }

            Object.DestroyImmediate(packInstance);
        }
    }
}
