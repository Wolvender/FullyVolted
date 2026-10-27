using FullyVolted.Climbing;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Climbing;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;

namespace FullyVolted.EditorTools
{
    // Builds the twin-lanyard fall protection: a harness on the rig with two carabiners in hip sockets, and
    // anchor sockets along the training route. All grabbing and clipping is stock XRI; the scripts added
    // here only read the result.
    public static class LanyardBuilder
    {
        private const string InteractionLayerSettingsPath = "Assets/XRI/Settings/Resources/InteractionLayerSettings.asset";

        // XRI interaction layer 1. The hands only use Default, the sockets only use Lanyard, and the carabiners
        // use both - so a socket can never grab a rung, a PPE item or a switch, and the hands can still grab
        // the carabiners.
        private const int LanyardLayerIndex = 1;
        private const string LanyardLayerName = "Lanyard";
        private static readonly InteractionLayerMask LanyardMask = 1 << LanyardLayerIndex;
        private static readonly InteractionLayerMask CarabinerMask = 1 | (1 << LanyardLayerIndex);

        private const float HarnessDropBelowHead = 0.65f;
        private const float HolsterSideOffset = 0.2f;
        private const float HolsterForwardOffset = 0.08f;
        private const float HolsterRadius = 0.08f;

        private const float LanyardLength = 1.8f;
        private const float LanyardWidth = 0.012f;
        private static readonly Vector3 CarabinerSize = new Vector3(0.04f, 0.1f, 0.04f);

        // One anchor every this many rungs (~1.26m on the route ladder), alternating left and right, so the
        // next anchor up is always within reach while the one below still holds you.
        private const int AnchorEveryRungs = 3;
        private const float AnchorRadius = 0.1f;
        private const float AnchorVisualSize = 0.05f;
        // Anchors sit just inside the rails and on the climber's side of the rungs, not on top of the grab
        // points, so reaching for a rung doesn't grab a clipped carabiner instead.
        private const float AnchorInsetFromRail = 0.06f;
        private const float AnchorStandOff = 0.07f;
        // One more anchor on the handrail above the middle platform, for stepping off and onto the ladder.
        private const float PlatformAnchorDistance = 1f;
        private const float HandrailHeight = 0.84f;

        public static AttachmentMonitor CreateHarness(GameObject rig, Transform head, out FallArrest fallArrest)
        {
            EnsureLanyardInteractionLayer();

            var harness = new GameObject("Harness");
            harness.transform.SetParent(rig.transform, false);
            if (head != null)
                harness.transform.SetPositionAndRotation(head.position + Vector3.down * HarnessDropBelowHead, rig.transform.rotation);

            var follower = harness.AddComponent<HarnessFollower>();
            var serializedFollower = new SerializedObject(follower);
            serializedFollower.FindProperty("head").objectReferenceValue = head;
            serializedFollower.FindProperty("dropBelowHead").floatValue = HarnessDropBelowHead;
            serializedFollower.ApplyModifiedPropertiesWithoutUndo();

            var body = rig.GetComponentInChildren<CharacterController>(true);
            var material = ClimbSceneBuilder.GetMaterial("Carabiner", new Color(0.95f, 0.75f, 0.15f));
            var lanyardMaterial = GetLanyardMaterial();

            var left = CreateCarabiner("Left", -1f, harness.transform, body, material, lanyardMaterial);
            var right = CreateCarabiner("Right", 1f, harness.transform, body, material, lanyardMaterial);

            var monitor = harness.AddComponent<AttachmentMonitor>();
            var serializedMonitor = new SerializedObject(monitor);
            var carabiners = serializedMonitor.FindProperty("carabiners");
            carabiners.arraySize = 2;
            carabiners.GetArrayElementAtIndex(0).objectReferenceValue = left;
            carabiners.GetArrayElementAtIndex(1).objectReferenceValue = right;
            serializedMonitor.ApplyModifiedPropertiesWithoutUndo();

            fallArrest = CreateFallArrest(rig, harness.transform, left, right);
            return monitor;
        }

        private static FallArrest CreateFallArrest(GameObject rig, Transform harness, params Carabiner[] carabiners)
        {
            // A locomotion provider, so it goes under the rig's LocomotionMediator: that's where the stock
            // GravityProvider looks for gravity controllers, and how it gets to move the rig.
            var mediator = rig.GetComponentInChildren<LocomotionMediator>(true);
            if (mediator == null)
            {
                Debug.LogWarning("[FullyVolted] No LocomotionMediator on the rig; falls will not be caught or reported.");
                return null;
            }

            var fallObject = new GameObject("Fall Arrest");
            fallObject.transform.SetParent(mediator.transform, false);
            var fallArrest = fallObject.AddComponent<FallArrest>();

            var serialized = new SerializedObject(fallArrest);
            var list = serialized.FindProperty("carabiners");
            list.arraySize = carabiners.Length;
            for (int i = 0; i < carabiners.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = carabiners[i];
            serialized.FindProperty("harness").objectReferenceValue = harness;
            serialized.FindProperty("climbProvider").objectReferenceValue = rig.GetComponentInChildren<ClimbProvider>(true);
            serialized.FindProperty("gravityProvider").objectReferenceValue = rig.GetComponentInChildren<GravityProvider>(true);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return fallArrest;
        }

        public static void CreateAnchors(LadderRoute route, Transform tower)
        {
            // Under the tower, so the anchors move with it. Positions below are world space, set after parenting.
            var parent = new GameObject("Lanyard Anchors").transform;
            parent.SetParent(tower, false);
            var material = ClimbSceneBuilder.GetMaterial("Anchor", new Color(0.2f, 0.8f, 0.3f));

            // Counted over the whole route, so the left/right alternation carries on across ladder hand-overs.
            var count = 0;
            foreach (var section in route.Sections)
            {
                var inset = section.RungHalfLength - AnchorInsetFromRail;
                for (int i = 0; i < section.Rungs.Length; i += AnchorEveryRungs)
                {
                    var side = count % 2 == 0 ? -1f : 1f;
                    var position = section.Rungs[i] + section.RungAxis * (side * inset) - section.TowardPlatform * AnchorStandOff;
                    CreateAnchor($"Anchor {++count:00}", parent, position, material);
                }
            }

            // The middle platform's handrail, for stepping off ladder 1 and onto the next one. The top deck
            // needs none: the last ladder carries on above the deck, so its own top anchors are within reach.
            var first = route.First;
            var platformAnchor = first.Top + first.TowardPlatform * PlatformAnchorDistance + Vector3.up * HandrailHeight +
                                 first.RungAxis * (first.RungHalfLength - AnchorInsetFromRail);
            CreateAnchor($"Anchor {++count:00} (Middle Platform)", parent, platformAnchor, material);

            Debug.Log($"[FullyVolted] {count} lanyard anchors placed along {route.Sections.Length} ladders.");
        }

        private static Carabiner CreateCarabiner(string side, float sideSign, Transform harness, CharacterController body,
            Material material, Material lanyardMaterial)
        {
            var holster = CreateSocket($"{side} Hip Holster", harness, HolsterRadius);
            holster.transform.localPosition = new Vector3(sideSign * HolsterSideOffset, 0f, HolsterForwardOffset);

            var hook = CarabinerModel.Create($"{side} Carabiner");
            if (hook == null)
            {
                hook = GameObject.CreatePrimitive(PrimitiveType.Cube);
                hook.name = $"{side} Carabiner";
                hook.transform.localScale = CarabinerSize;
                hook.GetComponent<Renderer>().sharedMaterial = material;
            }

            hook.transform.SetPositionAndRotation(holster.transform.position, holster.transform.rotation);

            var grab = hook.AddComponent<XRGrabInteractable>();
            grab.interactionLayers = CarabinerMask;
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = true;

            // XRGrabInteractable is [RequireComponent(typeof(Rigidbody))]: a dynamic body would fall off the tower.
            var rigidbody = hook.GetComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            holster.startingSelectedInteractable = grab;

            var carabiner = hook.AddComponent<Carabiner>();
            var serializedCarabiner = new SerializedObject(carabiner);
            serializedCarabiner.FindProperty("harness").objectReferenceValue = harness;
            serializedCarabiner.FindProperty("lanyardLength").floatValue = LanyardLength;
            serializedCarabiner.ApplyModifiedPropertiesWithoutUndo();

            var returnToHolster = hook.AddComponent<ReturnToHolster>();
            var serializedReturn = new SerializedObject(returnToHolster);
            serializedReturn.FindProperty("holster").objectReferenceValue = holster;
            serializedReturn.ApplyModifiedPropertiesWithoutUndo();

            if (body != null)
            {
                var ignore = hook.AddComponent<IgnoreCollisionWith>();
                var serializedIgnore = new SerializedObject(ignore);
                var others = serializedIgnore.FindProperty("others");
                others.arraySize = 1;
                others.GetArrayElementAtIndex(0).objectReferenceValue = body;
                serializedIgnore.ApplyModifiedPropertiesWithoutUndo();
            }

            CreateLanyardLine($"{side} Lanyard", harness, carabiner, lanyardMaterial);
            return carabiner;
        }

        private static void CreateLanyardLine(string objectName, Transform harness, Carabiner carabiner, Material material)
        {
            // Its own object: a GameObject can only have one Renderer, and the carabiner already has one.
            var lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(harness, false);

            var line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.widthMultiplier = LanyardWidth;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;

            var lanyard = lineObject.AddComponent<LanyardLine>();
            var serialized = new SerializedObject(lanyard);
            serialized.FindProperty("carabiner").objectReferenceValue = carabiner;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateAnchor(string objectName, Transform parent, Vector3 position, Material material)
        {
            var anchor = CreateSocket(objectName, parent, AnchorRadius);
            anchor.transform.position = position;
            anchor.gameObject.AddComponent<LanyardAnchor>();

            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Ring";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(anchor.transform, false);
            visual.transform.localScale = Vector3.one * AnchorVisualSize;

            var renderer = visual.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static XRSocketInteractor CreateSocket(string objectName, Transform parent, float radius)
        {
            var socketObject = new GameObject(objectName);
            socketObject.transform.SetParent(parent, false);

            // The socket's catch area. A trigger, so it never blocks the player or the hands' grab casts.
            var trigger = socketObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = radius;

            var socket = socketObject.AddComponent<XRSocketInteractor>();
            socket.interactionLayers = LanyardMask;
            return socket;
        }

        private static Material GetLanyardMaterial()
        {
            const string path = "Assets/Materials/Lanyard.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            // Unlit, and this shader multiplies by vertex colour - which is how LanyardLine turns it red.
            var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureLanyardInteractionLayer()
        {
            // XRI only exposes renaming layers internally, so this edits the settings asset like its inspector does.
            var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(InteractionLayerSettingsPath);
            if (settings == null)
            {
                Debug.LogWarning("[FullyVolted] No XRI InteractionLayerSettings at " + InteractionLayerSettingsPath +
                                 "; the Lanyard interaction layer stays unnamed (it still works).");
                return;
            }

            var serialized = new SerializedObject(settings);
            var name = serialized.FindProperty("m_LayerNames").GetArrayElementAtIndex(LanyardLayerIndex);
            if (name.stringValue == LanyardLayerName)
                return;

            if (!string.IsNullOrEmpty(name.stringValue))
                Debug.LogWarning($"[FullyVolted] XRI interaction layer {LanyardLayerIndex} was '{name.stringValue}', renaming to '{LanyardLayerName}'.");

            name.stringValue = LanyardLayerName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }
    }
}
