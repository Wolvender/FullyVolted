using System.Collections.Generic;
using FullyVolted.Climbing;
using FullyVolted.Feedback;
using FullyVolted.Loop;
using FullyVolted.Procedure;
using FullyVolted.Results;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Climbing;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace FullyVolted.EditorTools
{
    public static class ClimbSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/ClimbLoop.unity";
        private const string MaterialFolder = "Assets/Materials";
        internal const string RigPrefabName = "XR Origin (XR Rig)";

        // Holds the Supabase URL + key. Gitignored: create it via Create > FullyVolted > Supabase Config.
        private const string SupabaseConfigPath = "Assets/Settings/Supabase Config.asset";
        // Our own API. Preferred over Supabase when its asset exists. Gitignored (holds the key).
        private const string ApiConfigPath = "Assets/Settings/Api Config.asset";

        private const float RungInteractionDistance = 0.3f;

        internal static readonly Vector3 RigStartPosition = new Vector3(2.2f, 0f, -1.4f);
        internal static readonly Quaternion RigStartRotation = Quaternion.Euler(0f, -58f, 0f);

        // Imported by glTFast. The pack is one model with ~30 props side by side; only radio tower_01 is used.
        internal const string TowerPackPath = "Assets/Models/TowersPack/towers_lamps_and_structures.glb";
        internal const string TowerNodePrefix = "radio tower_01";

        // The tower's steel frame, ladders and platforms - everything that is walked on or climbed.
        private const string ClimbableNodeName = "Object_5";

        // The model's rungs are ~1cm thick; the grab point around each one is this much fatter so the
        // glow is visible and the hand has something to find.
        private const float ClimbPointPadding = 0.04f;

        // ~15m footprint. In view from the rig start, clear of the PPE bench.
        internal static readonly Vector3 TowerPosition = new Vector3(-10.48f, 0f, 10f);

        // Zones test the head position. Ground: head below this height, anywhere around the tower and the
        // bench. Middle platform and top deck: head in this band above the floor, over the tower's footprint.
        private const float GroundZoneHeadHeight = 2.6f;
        private const float GroundZoneSize = 60f;
        private const float PlatformZoneMinHead = 0.9f;
        private const float PlatformZoneMaxHead = 2.6f;
        private const float PlatformZoneSize = 10f;

        // Free scooter helmet pack. Its materials are Built-in: convert them to URP once (see tasks/todo.md).
        private const string HelmetPrefabPath = "Assets/Scooter helmet/Prefab/helmet_red.prefab";
        private const float HelmetSize = 0.3f;

        private const float PpeBenchHeight = 0.8f;

        // The player starts at the foot of the first ladder, this far out from its rungs and facing them. The helmet
        // bench stands beside them, a step to the right, measured in the player's own frame (x right, z forward).
        private const float SpawnDistanceFromLadder = 1.3f;
        private static readonly Vector3 PpeBenchOffset = new Vector3(1.0f, 0f, 0f);

        // The switch panel is on the top deck (~3x3m), this far in from the last ladder: past the hatch it comes
        // up through, so the player steps off the ladder, takes a step, and faces it.
        private const float SwitchPanelDistance = 1.8f;
        private const float SwitchPanelHeight = 1.25f;

        // How far below the last ladder's top rung to look for the top deck (it is ~1.4m on this tower).
        private const float TopDeckSearchDepth = 3f;

        // Behind the player stepping off the bottom of the ladder.
        private const float CompleteScreenDistance = 2.5f;
        private const float CompleteScreenHeight = 1.7f;

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

            // Sun, sky, fog, post-processing and the terrain (also the ground): shared with the landscape test scene.
            EnvironmentBuilder.Create();

            var rig = InstantiateRig(rigPrefab);

            var climbProvider = rig.GetComponentInChildren<ClimbProvider>(true);
            if (climbProvider == null)
                Debug.LogWarning("[FullyVolted] No ClimbProvider found on the rig; climbing will not work.");

            if (!CreateRadioTower(climbProvider, out var tower, out var route, out var topDeckHeight))
            {
                EditorUtility.DisplayDialog("Climb Scene Builder",
                    "The radio tower or its ladder could not be found, so the procedure has nowhere to happen. " +
                    "See the console for details.", "OK");
                return;
            }

            // The ladder decides where everything starts: the player at its foot, facing it, with the helmet bench beside them.
            var spawnForward = route.First.TowardPlatform;
            var spawnPosition = new Vector3(route.Bottom.x, 0f, route.Bottom.z) - spawnForward * SpawnDistanceFromLadder;
            var spawnRotation = Quaternion.LookRotation(spawnForward);
            rig.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

            // Decoration only: a plaza, streets and low-poly buildings round the tower.
            TownBuilder.Create(tower, EnvironmentBuilder.Folder);

            EnsureInteractionManager();
            EnsureEventSystem();
            // No XR Interaction Simulator in the scene: it is spawned by XRI's
            // "Use XR Interaction Simulator in scenes" project setting instead, so
            // switching between headset and keyboard testing is one checkbox.

            var playerHead = ResolvePlayerHead(rig);
            if (playerHead != null)
                EnvironmentBuilder.PrepareCamera(playerHead.GetComponent<Camera>());
            // Everything placed relative to the tower is parented under it, so moving the tower in the scene
            // takes the zones, anchors, switch panel and complete screen along.
            var bottomZone = CreateZone("Bottom Zone", "Ground",
                new Vector3(TowerPosition.x, GroundZoneHeadHeight / 2f, TowerPosition.z),
                new Vector3(GroundZoneSize, GroundZoneHeadHeight, GroundZoneSize), playerHead, tower);
            var middleZone = CreatePlatformZone("Middle Zone", "Middle Platform", route.MiddlePlatformHeight, playerHead, tower);
            var topZone = CreatePlatformZone("Top Zone", "Top Deck", topDeckHeight, playerHead, tower);

            var completeScreenPosition = new Vector3(route.Bottom.x, CompleteScreenHeight, route.Bottom.z) -
                                         route.First.TowardPlatform * CompleteScreenDistance;
            var completeScreen = CreateCompleteScreen(completeScreenPosition, Quaternion.LookRotation(-route.First.TowardPlatform),
                out var restartButton);
            completeScreen.transform.SetParent(tower, true);

            LightingEffects.AddTowerBeacon(tower, EnvironmentBuilder.Folder);
            WindBuilder.CreateAudio(playerHead, topDeckHeight, EnvironmentBuilder.Folder);
            WindBuilder.AttachWispsToPlayer(playerHead, topDeckHeight);

            var ppeItems = CreatePpeBench(spawnPosition, spawnRotation);
            var last = route.Last;
            var deEnergizeSteps = CreateSwitchPanel(
                new Vector3(last.Top.x, topDeckHeight + SwitchPanelHeight, last.Top.z) + last.TowardPlatform * SwitchPanelDistance,
                Quaternion.LookRotation(last.TowardPlatform), topDeckHeight, tower);

            var attachmentMonitor = LanyardBuilder.CreateHarness(rig, playerHead, out var fallArrest);
            LanyardBuilder.CreateAnchors(route, tower);

            var controller = CreateProcedureController(middleZone, topZone, bottomZone, attachmentMonitor, fallArrest, completeScreen, restartButton,
                ppeItems, deEnergizeSteps);
            CreateStepFlash(controller, playerHead);
            ChimeBuilder.CreateStepChimes(controller, EnvironmentBuilder.Folder);
            TowerFeedbackBuilder.Create(rig, playerHead, fallArrest, controller, topDeckHeight, EnvironmentBuilder.Folder);
            CreateObjectivePanel(controller, rig, playerHead);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            EnsureSceneInBuildSettings();

            Debug.Log("[FullyVolted] Built climb scene at " + ScenePath);
        }

        internal static GameObject LoadPrefab(string prefabName)
        {
            foreach (var guid in AssetDatabase.FindAssets(prefabName + " t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == prefabName)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            return null;
        }

        private static GameObject InstantiateRig(GameObject rigPrefab)
        {
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab);
            rig.transform.SetPositionAndRotation(RigStartPosition, RigStartRotation);
            return rig;
        }

        private static void EnsureSceneInBuildSettings()
        {
            // The restart button reloads the scene by name, which only resolves for scenes listed here.
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(entry => entry.path == ScenePath))
                return;

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[FullyVolted] Added " + ScenePath + " to Build Settings so the restart button can reload it.");
        }

        private static Transform ResolvePlayerHead(GameObject rig)
        {
            var origin = rig.GetComponentInChildren<XROrigin>(true);
            if (origin != null && origin.Camera != null)
                return origin.Camera.transform;

            var camera = rig.GetComponentInChildren<Camera>(true);
            if (camera != null)
                return camera.transform;

            Debug.LogWarning("[FullyVolted] No camera found on the rig; zone detection will not work.");
            return null;
        }



        private static bool CreateRadioTower(ClimbProvider climbProvider, out Transform towerRoot, out LadderRoute route,
            out float topDeckHeight)
        {
            towerRoot = null;
            route = default;
            topDeckHeight = 0f;
            var pack = AssetDatabase.LoadAssetAtPath<GameObject>(TowerPackPath);
            if (pack == null)
            {
                Debug.LogWarning("[FullyVolted] Tower pack not found at " + TowerPackPath +
                                 ". Is the glTFast package installed so the .glb imports?");
                return false;
            }

            // A plain copy (no prefab link), so the tower can be lifted out and the rest thrown away.
            // The meshes and materials stay shared sub-assets of the .glb.
            var packInstance = Object.Instantiate(pack);
            var tower = FindChild(packInstance.transform, child => child.name.StartsWith(TowerNodePrefix));
            if (tower == null)
            {
                Object.DestroyImmediate(packInstance);
                Debug.LogWarning("[FullyVolted] No node starting with '" + TowerNodePrefix + "' in the tower pack.");
                return false;
            }

            // A plain root (scale 1, no rotation) that the model and everything placed around it hang under.
            // Kept unscaled on purpose: under the model's x8.25 every child would need its scale compensated.
            towerRoot = new GameObject("Radio Tower").transform;
            towerRoot.position = TowerPosition;

            // Keeps the pack's import scale (x8.25 on this node) and rotation.
            tower.SetParent(towerRoot, true);
            Object.DestroyImmediate(packInstance);

            tower.name = "Tower Model";
            tower.localPosition = Vector3.zero;

            // Never moves. Not an occluder: it's an open lattice you can see straight through.
            foreach (var child in tower.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.BatchingStatic);

            // Exact match: a prefix check would also hit Object_50..Object_59 elsewhere in the pack.
            var climbable = FindChild(tower, child => child.name == ClimbableNodeName);
            if (climbable == null || !climbable.TryGetComponent<MeshFilter>(out var meshFilter))
            {
                Debug.LogWarning("[FullyVolted] No '" + ClimbableNodeName + "' mesh on the radio tower; it will not be walkable or climbable.");
                return false;
            }

            // Walkable: the platforms and stairs become solid for the rig's CharacterController.
            climbable.gameObject.AddComponent<MeshCollider>().sharedMesh = meshFilter.sharedMesh;

            var worldScale = Mathf.Abs(climbable.lossyScale.x);
            var rungs = LadderRungFinder.FindRungs(meshFilter.sharedMesh, worldScale);
            CreateClimbPoints(climbable, meshFilter.sharedMesh, rungs, worldScale, climbProvider);

            if (!LadderRoute.TryFind(climbable, rungs, tower.position, out route))
            {
                Debug.LogWarning("[FullyVolted] Found no ladder on the radio tower to use as the training route.");
                return false;
            }

            // Probed beside the hatch, where the switch panel goes: straight down the hatch there is no floor.
            var last = route.Last;
            var probe = last.Top + last.TowardPlatform * SwitchPanelDistance;
            if (!PlatformFloorFinder.TryFindFloorBelow(meshFilter.sharedMesh, climbable, probe, TopDeckSearchDepth, out topDeckHeight))
            {
                Debug.LogWarning("[FullyVolted] Found no deck at the top of the last ladder; the top checkpoint has nowhere to go.");
                return false;
            }

            Debug.Log($"[FullyVolted] Route: {route.Sections.Length} ladders, {route.Bottom.y:0.0}m to {last.Top.y:0.0}m. " +
                      $"Middle platform at {route.MiddlePlatformHeight:0.0}m, top deck at {topDeckHeight:0.0}m.");
            return true;
        }

        private static void CreateClimbPoints(Transform climbable, Mesh mesh, List<LadderRungFinder.Rung> rungs,
            float worldScale, ClimbProvider climbProvider)
        {
            // The whole frame is one mesh, so one highlight would light up the entire tower. Instead every
            // ladder rung gets its own grab point, and only the rung near the hand glows.
            var material = GetClimbPointMaterial();
            var padding = Vector3.one * (ClimbPointPadding / worldScale);

            var parent = new GameObject("Climb Points").transform;
            parent.SetParent(climbable, false);

            for (int i = 0; i < rungs.Count; i++)
            {
                CreateClimbPoint($"Climb Point {i + 1:000}", parent, rungs[i].Center, rungs[i].Rotation,
                    rungs[i].Size + padding, material, climbProvider);
            }

            // The side rails too: past the top rung they are the only thing to hold while stepping onto
            // the platform, so without them every ladder is a dead end.
            var rails = LadderRailFinder.FindRails(mesh, rungs, worldScale);
            for (int i = 0; i < rails.Count; i++)
            {
                CreateClimbPoint($"Rail Point {i + 1:000}", parent, rails[i].Center, rails[i].Rotation,
                    rails[i].Size + padding, material, climbProvider);
            }

            Debug.Log($"[FullyVolted] Radio tower: {rungs.Count} climb points on the ladder rungs, " +
                      $"{rails.Count} on the ladder rails.");
        }

        private static void CreateClimbPoint(string pointName, Transform parent, Vector3 localPosition,
            Quaternion localRotation, Vector3 localScale, Material material, ClimbProvider climbProvider)
        {
            var point = GameObject.CreatePrimitive(PrimitiveType.Cube);
            point.name = pointName;
            point.transform.SetParent(parent, false);
            point.transform.localPosition = localPosition;
            point.transform.localRotation = localRotation;
            point.transform.localScale = localScale;

            var pointRenderer = point.GetComponent<MeshRenderer>();
            pointRenderer.sharedMaterial = material;
            pointRenderer.shadowCastingMode = ShadowCastingMode.Off;
            pointRenderer.receiveShadows = false;

            MakeClimbHold(point, climbProvider);
        }

        private static void MakeClimbHold(GameObject hold, ClimbProvider climbProvider)
        {
            var climbInteractable = hold.AddComponent<ClimbInteractable>();
            climbInteractable.climbProvider = climbProvider;

            // Default is 0.1m, which is hard to hit while reaching and impossible to trigger with a
            // pointing ray. This gates hover and grab alike, so it stays within arm's reach.
            climbInteractable.maxInteractionDistance = RungInteractionDistance;

            // ClimbInteractable is [RequireComponent(typeof(Rigidbody))], so adding it also adds a
            // dynamic body. Holds are fixed - the player moves, not the hold.
            var body = hold.GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            hold.AddComponent<ClimbHoldHighlight>();
        }

        private static Material GetClimbPointMaterial()
        {
            const string path = MaterialFolder + "/Climb Point.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            // Transparent with zero alpha: invisible at rest, so the model's own rung is what you see.
            // ClimbHoldHighlight raises the alpha and the emission while a hand hovers.
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetColor("_BaseColor", new Color(1f, 0.62f, 0.18f, 0f));

            // Without the keyword, the shader never samples _EmissionColor, so the hover glow is invisible.
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetColor("_EmissionColor", Color.black);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        internal static Transform FindChild(Transform parent, System.Predicate<Transform> match)
        {
            foreach (var child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (match(child))
                    return child;
            }

            return null;
        }

        internal static void EnsureInteractionManager()
        {
            if (Object.FindFirstObjectByType<XRInteractionManager>() != null)
                return;

            var manager = new GameObject("XR Interaction Manager");
            manager.AddComponent<XRInteractionManager>();
        }

        private static PlayerZoneTrigger CreatePlatformZone(string objectName, string zoneName, float floorHeight, Transform playerHead,
            Transform parent)
        {
            return CreateZone(objectName, zoneName,
                new Vector3(TowerPosition.x, floorHeight + (PlatformZoneMinHead + PlatformZoneMaxHead) / 2f, TowerPosition.z),
                new Vector3(PlatformZoneSize, PlatformZoneMaxHead - PlatformZoneMinHead, PlatformZoneSize), playerHead, parent);
        }

        private static PlayerZoneTrigger CreateZone(string objectName, string zoneName, Vector3 position, Vector3 size,
            Transform playerHead, Transform parent)
        {
            var zone = new GameObject(objectName);
            zone.transform.position = position;
            zone.transform.SetParent(parent, true);

            var collider = zone.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = size;

            var trigger = zone.AddComponent<PlayerZoneTrigger>();
            var serialized = new SerializedObject(trigger);
            serialized.FindProperty("zoneName").stringValue = zoneName;
            serialized.FindProperty("playerHead").objectReferenceValue = playerHead;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return trigger;
        }

        private static GameObject CreateCompleteScreen(Vector3 position, Quaternion rotation, out Button restartButton)
        {
            var canvasObject = new GameObject("Complete Screen");
            canvasObject.transform.SetPositionAndRotation(position, rotation);
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

        private static ProcedureController CreateProcedureController(PlayerZoneTrigger middleZone, PlayerZoneTrigger topZone,
            PlayerZoneTrigger bottomZone,
            AttachmentMonitor attachmentMonitor, FallArrest fallArrest, GameObject completeScreen, Button restartButton, XRBaseInteractable[] ppeItems, XRBaseInteractable[] deEnergizeSteps)
        {
            var controllerObject = new GameObject("Procedure Controller");
            var controller = controllerObject.AddComponent<ProcedureController>();

            if (restartButton != null)
                UnityEventTools.AddPersistentListener(restartButton.onClick, controller.RestartProcedure);

            var installer = controllerObject.AddComponent<ProcedureInstaller>();
            var serializedInstaller = new SerializedObject(installer);
            serializedInstaller.FindProperty("controller").objectReferenceValue = controller;
            serializedInstaller.FindProperty("middleZone").objectReferenceValue = middleZone;
            serializedInstaller.FindProperty("topZone").objectReferenceValue = topZone;
            serializedInstaller.FindProperty("bottomZone").objectReferenceValue = bottomZone;
            serializedInstaller.FindProperty("attachmentMonitor").objectReferenceValue = attachmentMonitor;
            serializedInstaller.FindProperty("fallArrest").objectReferenceValue = fallArrest;
            serializedInstaller.FindProperty("completeScreen").objectReferenceValue = completeScreen;
            AssignArray(serializedInstaller.FindProperty("ppeItems"), ppeItems);
            AssignArray(serializedInstaller.FindProperty("deEnergizeSteps"), deEnergizeSteps);
            serializedInstaller.ApplyModifiedPropertiesWithoutUndo();

            var observer = controllerObject.AddComponent<DebugStepObserver>();
            var serializedObserver = new SerializedObject(observer);
            serializedObserver.FindProperty("procedureController").objectReferenceValue = controller;
            serializedObserver.ApplyModifiedPropertiesWithoutUndo();

            // Optional: without a filled-in config asset the game runs normally and just doesn't save results.
            var recorder = controllerObject.AddComponent<RunRecordingObserver>();
            var serializedRecorder = new SerializedObject(recorder);
            serializedRecorder.FindProperty("procedureController").objectReferenceValue = controller;
            serializedRecorder.FindProperty("storeConfig").objectReferenceValue =
                (Object)AssetDatabase.LoadAssetAtPath<ApiConfig>(ApiConfigPath)
                ?? AssetDatabase.LoadAssetAtPath<SupabaseConfig>(SupabaseConfigPath);
            serializedRecorder.ApplyModifiedPropertiesWithoutUndo();

            return controller;
        }

        private static void AssignArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static XRBaseInteractable[] CreatePpeBench(Vector3 spawnPosition, Quaternion spawnRotation)
        {
            // Faces the way the player looks on spawn, so the labels read correctly.
            var rotation = spawnRotation;
            var benchPosition = spawnPosition + rotation * PpeBenchOffset;

            var bench = CreatePrimitive(PrimitiveType.Cube, "PPE Bench",
                benchPosition + Vector3.up * (PpeBenchHeight / 2f), new Vector3(1f, PpeBenchHeight, 0.4f));
            bench.transform.rotation = rotation;
            ApplyMaterial(bench, "Platform", new Color(0.45f, 0.45f, 0.48f));
            GameObjectUtility.SetStaticEditorFlags(bench, StaticEditorFlags.BatchingStatic);

            var material = GetMaterial("Equipment", new Color(0.20f, 0.45f, 0.80f));
            var itemCenter = benchPosition + Vector3.up * (PpeBenchHeight + 0.06f);

            // Laid out shuffled on purpose - the bench must not give away the procedure order.
            var helmet = CreateModelInteractable("Helmet", itemCenter + Vector3.up * (HelmetSize / 2f - 0.06f),
                rotation, HelmetPrefabPath, HelmetSize, material);

            // The array order IS the procedure order. Just the helmet: no harness model was available.
            return new[] { helmet };
        }

        // A real model as an interactable: a wrapper named after the item (the procedure reports that name),
        // the model scaled to `size` and centred inside it, and a box collider around it for XRI to hit.
        // Falls back to the plain cube if the model is not in the project, so the builder never breaks on it.
        private static XRBaseInteractable CreateModelInteractable(string objectName, Vector3 position, Quaternion rotation,
            string prefabPath, float size, Material fallbackMaterial)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[FullyVolted] No model at '{prefabPath}'; {objectName} falls back to a cube.");
                return CreateLabelledInteractable(objectName, position, rotation, fallbackMaterial, null);
            }

            // Measured at the origin with no rotation, so the renderer bounds are the model's own size.
            var root = new GameObject(objectName);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            UrpMaterials.ConvertAll(model);

            var collider = root.AddComponent<BoxCollider>();
            collider.size = ModelFit.FitCentered(model, size);

            root.transform.SetPositionAndRotation(position, rotation);
            CreateWorldLabel(objectName, position + Vector3.up * (size / 2f + 0.08f), rotation, null);

            return root.AddComponent<XRSimpleInteractable>();
        }

        // Where the control cabinet is bolted to the deck, and how big it is. The panel plate sits on its front face.
        private const float CabinetWidth = 1.9f;
        private const float CabinetDepth = 0.4f;
        private const float CabinetHeight = 1.8f;
        private const float PlinthHeight = 0.12f;

        private static XRBaseInteractable[] CreateSwitchPanel(Vector3 position, Quaternion rotation, float floorHeight, Transform parent)
        {
            // rotation looks the way the player walks up to the panel; the buttons face them.
            var right = rotation * Vector3.right;

            // A group at the button row, so the panel, its buttons and their labels move as one.
            var group = new GameObject("Switch Panel").transform;
            group.SetPositionAndRotation(position, rotation);
            group.SetParent(parent, true);

            CreateControlCabinet(position, rotation, floorHeight, group);

            var panel = CreatePrimitive(PrimitiveType.Cube, "Panel",
                position + rotation * Vector3.forward * 0.08f, new Vector3(1.7f, 0.7f, 0.04f));
            panel.transform.rotation = rotation;
            panel.transform.SetParent(group, true);
            ApplyMaterial(panel, "Steel", new Color(0.38f, 0.40f, 0.44f));
            GameObjectUtility.SetStaticEditorFlags(panel, StaticEditorFlags.BatchingStatic);

            var material = GetMaterial("Switch", new Color(0.75f, 0.15f, 0.12f));

            var groundAndLock = CreateLabelledInteractable("Ground And Lock", position - right * 0.6f, rotation, material, group);
            var testVoltage = CreateLabelledInteractable("Test Voltage", position + right * 0.6f, rotation, material, group);

            // The downloaded lever is the main control: pulling it is the Switch Off step. Cube if the model is missing.
            var switchOff = LeverModel.Create("Switch Off", position, rotation, group);
            if (switchOff != null)
                CreateWorldLabel("Switch Off", position + Vector3.up * 0.36f, rotation, group);
            else
                switchOff = CreateLabelledInteractable("Switch Off", position, rotation, material, group);

            return new[] { switchOff, testVoltage, groundAndLock };
        }

        // A steel control cabinet standing on the deck, so the switches are mounted on something and not floating.
        // Its front face sits just behind the buttons; the panel plate is the raised face plate on it.
        private static void CreateControlCabinet(Vector3 panelPosition, Quaternion rotation, float floorHeight, Transform group)
        {
            var forward = rotation * Vector3.forward;
            var frontFace = new Vector3(panelPosition.x, floorHeight, panelPosition.z) + forward * 0.10f;
            var color = new Color(0.30f, 0.33f, 0.36f);

            // The body stands on the deck from the plinth up.
            var body = CreatePrimitive(PrimitiveType.Cube, "Cabinet",
                frontFace + forward * (CabinetDepth / 2f) + Vector3.up * (CabinetHeight / 2f),
                new Vector3(CabinetWidth, CabinetHeight, CabinetDepth));

            // A wider plinth at the foot and a cap on top: reads as a bolted-down box, not a floating slab.
            var plinth = CreatePrimitive(PrimitiveType.Cube, "Cabinet Plinth",
                frontFace + forward * (CabinetDepth / 2f) + Vector3.up * (PlinthHeight / 2f),
                new Vector3(CabinetWidth + 0.2f, PlinthHeight, CabinetDepth + 0.2f));
            var cap = CreatePrimitive(PrimitiveType.Cube, "Cabinet Cap",
                frontFace + forward * (CabinetDepth / 2f) + Vector3.up * CabinetHeight,
                new Vector3(CabinetWidth + 0.1f, 0.06f, CabinetDepth + 0.1f));

            foreach (var part in new[] { body, plinth, cap })
            {
                part.transform.rotation = rotation;
                part.transform.SetParent(group, true);
                ApplyMaterial(part, "Steel", new Color(0.38f, 0.40f, 0.44f));
                GameObjectUtility.SetStaticEditorFlags(part, StaticEditorFlags.BatchingStatic);
            }

            ApplyMaterial(body, "Cabinet Steel", color);
        }

        private static XRBaseInteractable CreateLabelledInteractable(string objectName, Vector3 position,
            Quaternion rotation, Material material, Transform parent)
        {
            var item = CreatePrimitive(PrimitiveType.Cube, objectName, position, Vector3.one * 0.12f);
            item.transform.rotation = rotation;
            item.transform.SetParent(parent, true);
            item.GetComponent<Renderer>().sharedMaterial = material;

            CreateWorldLabel(objectName, position + Vector3.up * 0.14f, rotation, parent);

            // Stock XRI: fires selectEntered when grabbed but never moves, and pulls in no Rigidbody.
            return item.AddComponent<XRSimpleInteractable>();
        }

        private static void CreateWorldLabel(string content, Vector3 position, Quaternion rotation, Transform parent)
        {
            var canvasObject = new GameObject(content + " Label");
            canvasObject.transform.SetPositionAndRotation(position, rotation);
            canvasObject.transform.localScale = Vector3.one * 0.001f;
            canvasObject.transform.SetParent(parent, true);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvasObject.transform).sizeDelta = new Vector2(300f, 60f);

            var label = CreateLabel("Text", canvasObject.transform, content, 32);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private static void CreateStepFlash(ProcedureController controller, Transform playerHead)
        {
            // The Starter Assets camera has post-processing off, which would make every Volume do nothing.
            if (playerHead != null && playerHead.TryGetComponent<Camera>(out var camera))
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            var correctFlash = CreateFlashVolume("Correct Flash", new Color(0.45f, 1f, 0.45f));
            var incorrectFlash = CreateFlashVolume("Incorrect Flash", new Color(1f, 0.3f, 0.3f));

            var feedbackObject = new GameObject("Step Feedback");
            var flash = feedbackObject.AddComponent<StepResultFlash>();
            var serialized = new SerializedObject(flash);
            serialized.FindProperty("procedureController").objectReferenceValue = controller;
            serialized.FindProperty("correctFlash").objectReferenceValue = correctFlash;
            serialized.FindProperty("incorrectFlash").objectReferenceValue = incorrectFlash;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // One entry per step, in the order ProcedureInstaller chains them.
        private static readonly Objective[] ObjectiveTexts =
        {
            new Objective("Put on your helmet",
                "Pick up the HELMET from the bench."),
            new Objective("Climb to the middle platform",
                "Use the ladder. Clip a carabiner onto an anchor and keep one clipped at all times."),
            new Objective("Climb to the top of the tower",
                "Keep climbing and moving your carabiners up. Never let go unclipped."),
            new Objective("Cut the power",
                "At the top, press the buttons in order: SWITCH OFF, TEST VOLTAGE, then GROUND AND LOCK."),
            new Objective("Climb down safely",
                "Power is off. Stay clipped in all the way back to the ground."),
        };

        // ~0.42m x 0.22m: a screen that sits above the hand.
        private const float ObjectivePanelScale = 0.0007f;
        private static readonly Vector2 ObjectivePanelSize = new Vector2(600f, 320f);

        // Where the "look at your hand" sign floats, measured from the rig's start pose: close (the ladder is only
        // ~1.3m ahead), a little to the left and slightly above eye level, so it never covers the first rungs.
        private const float HandSignDistance = 0.8f;
        private const float HandSignLeft = 0.55f;
        private const float HandSignHeight = 1.75f;

        private static void CreateObjectivePanel(ProcedureController controller, GameObject rig, Transform playerHead)
        {
            var hand = FindChild(rig.transform, child => child.name == "Left Controller");
            if (hand == null)
                Debug.LogWarning("[FullyVolted] No 'Left Controller' on the rig; the objective panel will not follow a hand.");

            var sign = CreateHandSign(rig.transform);

            var canvasObject = new GameObject("Objective Panel");
            canvasObject.transform.localScale = Vector3.one * ObjectivePanelScale;

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvasObject.transform).sizeDelta = ObjectivePanelSize;
            canvasObject.AddComponent<CanvasGroup>().interactable = false;

            CreateStretchedImage("Background", canvasObject.transform, new Color(0.04f, 0.05f, 0.07f, 0.88f));

            var mission = CreatePanelText("Mission", canvasObject.transform, 22, new Color(1f, 0.8f, 0.2f), 0.85f, 1f);
            var stepCounter = CreatePanelText("Step Counter", canvasObject.transform, 24, new Color(0.7f, 0.75f, 0.8f), 0.71f, 0.85f);
            var title = CreatePanelText("Title", canvasObject.transform, 44, Color.white, 0.48f, 0.71f);
            var instruction = CreatePanelText("Instruction", canvasObject.transform, 30, new Color(0.9f, 0.92f, 0.95f), 0.04f, 0.48f);
            instruction.horizontalOverflow = HorizontalWrapMode.Wrap;
            instruction.verticalOverflow = VerticalWrapMode.Overflow;

            var panel = canvasObject.AddComponent<ObjectivePanel>();
            var serializedPanel = new SerializedObject(panel);
            serializedPanel.FindProperty("procedureController").objectReferenceValue = controller;
            serializedPanel.FindProperty("missionText").objectReferenceValue = mission;
            serializedPanel.FindProperty("stepCounterText").objectReferenceValue = stepCounter;
            serializedPanel.FindProperty("titleText").objectReferenceValue = title;
            serializedPanel.FindProperty("instructionText").objectReferenceValue = instruction;
            var objectives = serializedPanel.FindProperty("objectives");
            objectives.arraySize = ObjectiveTexts.Length;
            for (int i = 0; i < ObjectiveTexts.Length; i++)
            {
                var element = objectives.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("title").stringValue = ObjectiveTexts[i].title;
                element.FindPropertyRelative("instruction").stringValue = ObjectiveTexts[i].instruction;
            }
            serializedPanel.ApplyModifiedPropertiesWithoutUndo();

            var attached = canvasObject.AddComponent<HandAttachedPanel>();
            var serializedAttached = new SerializedObject(attached);
            serializedAttached.FindProperty("hand").objectReferenceValue = hand;
            serializedAttached.FindProperty("head").objectReferenceValue = playerHead;
            serializedAttached.ApplyModifiedPropertiesWithoutUndo();

            var fade = canvasObject.AddComponent<GazeFade>();
            var serializedFade = new SerializedObject(fade);
            serializedFade.FindProperty("head").objectReferenceValue = playerHead;
            serializedFade.FindProperty("hideOnFirstReveal").objectReferenceValue = sign;
            serializedFade.ApplyModifiedPropertiesWithoutUndo();
        }

        // Floats in front of the spawn point and disappears once the trainee has looked at their hand.
        private static GameObject CreateHandSign(Transform rig)
        {
            var forward = rig.rotation * Vector3.forward;
            var right = rig.rotation * Vector3.right;
            var position = rig.position + forward * HandSignDistance - right * HandSignLeft + Vector3.up * HandSignHeight;

            var canvasObject = new GameObject("Look At Your Hand Sign");
            canvasObject.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward));
            canvasObject.transform.localScale = Vector3.one * 0.0016f;

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvasObject.transform).sizeDelta = new Vector2(760f, 260f);

            CreateStretchedImage("Background", canvasObject.transform, new Color(0.85f, 0.55f, 0.12f, 0.95f));

            var title = CreatePanelText("Title", canvasObject.transform, 48, Color.black, 0.45f, 1f);
            title.text = "LOOK AT YOUR LEFT HAND";
            var hint = CreatePanelText("Hint", canvasObject.transform, 32, Color.black, 0.05f, 0.45f);
            hint.text = "Your mission is shown there";

            return canvasObject;
        }

        private static Text CreatePanelText(string objectName, Transform parent, int fontSize, Color color, float anchorMinY, float anchorMaxY)
        {
            var label = CreateLabel(objectName, parent, string.Empty, fontSize);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = new Vector2(0f, anchorMinY);
            rect.anchorMax = new Vector2(1f, anchorMaxY);
            rect.offsetMin = new Vector2(30f, 0f);
            rect.offsetMax = new Vector2(-30f, 0f);

            var text = label.GetComponent<Text>();
            text.color = color;
            text.resizeTextForBestFit = false;
            return text;
        }

        private static VolumeFlash CreateFlashVolume(string objectName, Color tint)
        {
            var volumeObject = new GameObject(objectName);
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.weight = 0f;
            volume.sharedProfile = GetFlashProfile(objectName, tint);

            return volumeObject.AddComponent<VolumeFlash>();
        }

        private static VolumeProfile GetFlashProfile(string profileName, Color tint)
        {
            var path = $"Assets/Settings/{profileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing != null)
                return existing;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            // The colour filter multiplies the whole image, so the view is tinted without going dark.
            var colorAdjustments = profile.Add<ColorAdjustments>();
            colorAdjustments.colorFilter.Override(tint);

            // Volume components are ScriptableObjects too - they only survive a save as sub-assets.
            AssetDatabase.AddObjectToAsset(colorAdjustments, profile);
            EditorUtility.SetDirty(profile);
            return profile;
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

        internal static Material GetMaterial(string materialName, Color color)
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
