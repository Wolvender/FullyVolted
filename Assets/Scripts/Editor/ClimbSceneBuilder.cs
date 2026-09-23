using FullyVolted.Climbing;
using FullyVolted.Loop;
using FullyVolted.Procedure;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Climbing;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace FullyVolted.EditorTools
{
    public static class ClimbSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/ClimbLoop.unity";
        private const string MaterialFolder = "Assets/Materials";
        private const string RigPrefabName = "XR Origin (XR Rig)";
        private const string SimulatorPrefabName = "XR Interaction Simulator";

        private const int RungCount = 10;
        private const float FirstRungHeight = 0.9f;
        private const float RungSpacing = 0.5f;
        private const float RungOffsetX = 0.45f;
        private const float RungInteractionDistance = 0.3f;
        private const float PlatformSurfaceY = 4.775f;

        private static readonly Vector3 RigStartPosition = new Vector3(2.2f, 0f, -1.4f);
        private static readonly Quaternion RigStartRotation = Quaternion.Euler(0f, -58f, 0f);

        [MenuItem("Tools/FullyVolted/Build Climb Scene")]
        public static void BuildClimbScene()
        {
            var rigPrefab = LoadPrefab(RigPrefabName);
            if (rigPrefab == null)
            {
                EditorUtility.DisplayDialog("Climb Scene Builder",
                    "Could not find the '" + RigPrefabName + "' prefab.\n\n" +
                    "Import it first via Window > Package Manager > XR Interaction Toolkit > Samples > Starter Assets > Import.",
                    "OK");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLight();
            CreateGround();
            CreatePole();
            CreatePlatform();

            var rig = InstantiateRig(rigPrefab);
            var startPose = CreateStartPose();

            var climbProvider = rig.GetComponentInChildren<ClimbProvider>(true);
            if (climbProvider == null)
                Debug.LogWarning("[FullyVolted] No ClimbProvider found on the rig; climbing will not work.");

            CreateRungs(climbProvider);
            EnsureInteractionManager();
            EnsureEventSystem();
            AddSimulator();

            var bottomZone = CreateZone("Bottom Zone", "Ground", new Vector3(1.2f, 1f, -0.5f), new Vector3(5f, 2f, 5f));
            var topZone = CreateZone("Top Zone", "Work Platform", new Vector3(1.5f, 5.35f, 0f), new Vector3(2.2f, 1.2f, 1.8f));
            var completeScreen = CreateCompleteScreen(out var restartButton);

            CreateProcedureController(topZone, bottomZone, completeScreen, rig.transform, startPose, restartButton);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log("[FullyVolted] Built climb scene at " + ScenePath);
        }

        private static GameObject LoadPrefab(string prefabName)
        {
            foreach (var guid in AssetDatabase.FindAssets(prefabName + " t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == prefabName)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            return null;
        }

        private static void AddSimulator()
        {
            var simulatorPrefab = LoadPrefab(SimulatorPrefabName);
            if (simulatorPrefab == null)
            {
                Debug.LogWarning("[FullyVolted] XR Interaction Simulator prefab not found; " +
                                 "desktop testing without a headset will not work. Run Tools > FullyVolted > Import XRI Samples.");
                return;
            }

            PrefabUtility.InstantiatePrefab(simulatorPrefab);
        }

        private static GameObject InstantiateRig(GameObject rigPrefab)
        {
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            rig.transform.SetPositionAndRotation(RigStartPosition, RigStartRotation);
            return rig;
        }

        private static Transform CreateStartPose()
        {
            var startPose = new GameObject("Start Pose");
            startPose.transform.SetPositionAndRotation(RigStartPosition, RigStartRotation);
            return startPose.transform;
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
        }

        private static void CreateGround()
        {
            var ground = CreatePrimitive(PrimitiveType.Plane, "Ground", Vector3.zero, new Vector3(3f, 1f, 3f));
            ApplyMaterial(ground, "Ground", new Color(0.32f, 0.34f, 0.30f));
            GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic);
        }

        private static void CreatePole()
        {
            var pole = CreatePrimitive(PrimitiveType.Cylinder, "Mast Pole", new Vector3(0f, 3f, 0f), new Vector3(0.36f, 3f, 0.36f));
            ApplyMaterial(pole, "Steel", new Color(0.38f, 0.40f, 0.44f));
            GameObjectUtility.SetStaticEditorFlags(pole, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic);
        }

        private static void CreatePlatform()
        {
            var platform = CreatePrimitive(PrimitiveType.Cube, "Work Platform", new Vector3(1.5f, 4.7f, 0f), new Vector3(2.4f, 0.15f, 2f));
            ApplyMaterial(platform, "Platform", new Color(0.45f, 0.45f, 0.48f));
            GameObjectUtility.SetStaticEditorFlags(platform, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic);
        }

        private static void CreateRungs(ClimbProvider climbProvider)
        {
            var parent = new GameObject("Rungs").transform;
            var rungMaterial = GetMaterial("Rung", new Color(0.85f, 0.55f, 0.12f));

            // Without the keyword, the shader never samples _EmissionColor, so the hover glow is invisible.
            rungMaterial.EnableKeyword("_EMISSION");
            rungMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            rungMaterial.SetColor("_EmissionColor", Color.black);
            EditorUtility.SetDirty(rungMaterial);

            for (int i = 0; i < RungCount; i++)
            {
                var height = FirstRungHeight + i * RungSpacing;
                var rung = CreatePrimitive(PrimitiveType.Cube, $"Rung {i + 1:00}",
                    new Vector3(RungOffsetX, height, 0f), new Vector3(0.55f, 0.07f, 0.14f));

                rung.transform.SetParent(parent, true);
                rung.GetComponent<Renderer>().sharedMaterial = rungMaterial;

                var climbInteractable = rung.AddComponent<ClimbInteractable>();
                climbInteractable.climbProvider = climbProvider;

                // Default is 0.1m, which is hard to hit while reaching and impossible to trigger with a
                // pointing ray. This gates hover and grab alike, so it stays within arm's reach.
                climbInteractable.maxInteractionDistance = RungInteractionDistance;

                // ClimbInteractable is [RequireComponent(typeof(Rigidbody))], so adding it also adds a
                // dynamic body. Rungs are fixed holds - the player moves, not the rung.
                var body = rung.GetComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;

                rung.AddComponent<ClimbHoldHighlight>();
            }
        }

        private static void EnsureInteractionManager()
        {
            if (Object.FindFirstObjectByType<XRInteractionManager>() != null)
                return;

            var manager = new GameObject("XR Interaction Manager");
            manager.AddComponent<XRInteractionManager>();
        }

        private static PlayerZoneTrigger CreateZone(string objectName, string zoneName, Vector3 position, Vector3 size)
        {
            var zone = new GameObject(objectName);
            zone.transform.position = position;

            var collider = zone.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = size;

            var trigger = zone.AddComponent<PlayerZoneTrigger>();
            var serialized = new SerializedObject(trigger);
            serialized.FindProperty("zoneName").stringValue = zoneName;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return trigger;
        }

        private static GameObject CreateCompleteScreen(out Button restartButton)
        {
            var canvasObject = new GameObject("Complete Screen");
            canvasObject.transform.SetPositionAndRotation(new Vector3(0.5f, 1.7f, -0.35f), Quaternion.Euler(0f, 122f, 0f));
            canvasObject.transform.localScale = Vector3.one * 0.003f;

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();

            var rectTransform = (RectTransform)canvasObject.transform;
            rectTransform.sizeDelta = new Vector2(700f, 400f);

            CreateStretchedImage("Background", canvasObject.transform, new Color(0.04f, 0.05f, 0.07f, 0.88f));

            var title = CreateLabel("Title", canvasObject.transform, "PROCEDURE COMPLETE", 64);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0f, 0.5f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            var buttonObject = new GameObject("Restart Button", typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(canvasObject.transform, false);
            buttonObject.GetComponent<Image>().color = new Color(0.85f, 0.55f, 0.12f);

            var buttonRect = (RectTransform)buttonObject.transform;
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 60f);
            buttonRect.sizeDelta = new Vector2(400f, 110f);

            var buttonLabel = CreateLabel("Label", buttonObject.transform, "CLIMB AGAIN", 44);
            var buttonLabelRect = (RectTransform)buttonLabel.transform;
            buttonLabelRect.anchorMin = Vector2.zero;
            buttonLabelRect.anchorMax = Vector2.one;
            buttonLabelRect.offsetMin = Vector2.zero;
            buttonLabelRect.offsetMax = Vector2.zero;
            buttonLabel.GetComponent<Text>().color = Color.black;

            restartButton = buttonObject.GetComponent<Button>();

            canvasObject.SetActive(false);
            return canvasObject;
        }

        private static void CreateStretchedImage(string objectName, Transform parent, Color color)
        {
            var image = new GameObject(objectName, typeof(Image));
            image.transform.SetParent(parent, false);

            var rect = (RectTransform)image.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            image.GetComponent<Image>().color = color;
        }

        private static GameObject CreateLabel(string objectName, Transform parent, string content, int fontSize)
        {
            var label = new GameObject(objectName, typeof(Text));
            label.transform.SetParent(parent, false);

            var text = label.GetComponent<Text>();
            text.text = content;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.font = LoadLegacyUiFont();

            return label;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<XRUIInputModule>();
        }

        private static void CreateProcedureController(PlayerZoneTrigger topZone, PlayerZoneTrigger bottomZone,
            GameObject completeScreen, Transform playerRig, Transform startPose, Button restartButton)
        {
            var controllerObject = new GameObject("Procedure Controller");
            var controller = controllerObject.AddComponent<ProcedureController>();

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("topZone").objectReferenceValue = topZone;
            serialized.FindProperty("bottomZone").objectReferenceValue = bottomZone;
            serialized.FindProperty("completeScreen").objectReferenceValue = completeScreen;
            serialized.FindProperty("playerRig").objectReferenceValue = playerRig;
            serialized.FindProperty("startPose").objectReferenceValue = startPose;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (restartButton != null)
                UnityEventTools.AddPersistentListener(restartButton.onClick, controller.RestartProcedure);

            var observer = controllerObject.AddComponent<DebugStepObserver>();
            var serializedObserver = new SerializedObject(observer);
            serializedObserver.FindProperty("procedureController").objectReferenceValue = controller;
            serializedObserver.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Font LoadLegacyUiFont()
        {
            // Unity replaced the built-in Arial with LegacyRuntime.ttf; older versions only have Arial.
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            return font;
        }

        private static GameObject CreatePrimitive(PrimitiveType type, string objectName, Vector3 position, Vector3 scale)
        {
            var created = GameObject.CreatePrimitive(type);
            created.name = objectName;
            created.transform.position = position;
            created.transform.localScale = scale;
            return created;
        }

        private static void ApplyMaterial(GameObject target, string materialName, Color color)
        {
            target.GetComponent<Renderer>().sharedMaterial = GetMaterial(materialName, color);
        }

        private static Material GetMaterial(string materialName, Color color)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets", "Materials");

            var path = $"{MaterialFolder}/{materialName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            // Primitives default to the built-in Standard shader, which renders magenta under URP.
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.color = color;

            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
