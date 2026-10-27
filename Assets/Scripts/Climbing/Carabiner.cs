using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace FullyVolted.Climbing
{
    // One leg of the twin lanyard. Knows whether its hook is clipped to an anchor, and whether that anchor
    // is still within lanyard reach of the harness. Grabbing and clipping themselves are stock XRI.
    [RequireComponent(typeof(XRGrabInteractable))]
    public class Carabiner : MonoBehaviour
    {
        private XRGrabInteractable grab;
        private Transform anchor;

        [SerializeField] private Transform harness;
        [Tooltip("Lanyard length in metres. Further than this from the harness, the anchor no longer catches a fall.")]
        [SerializeField, Min(0.1f)] private float lanyardLength = 1.8f;
        [SerializeField] private bool logEvents = true;

        public Transform Harness => harness;
        public Transform Anchor => anchor;
        public float LanyardLength => lanyardLength;
        public bool IsAnchored => anchor != null;

        // Over-stretched: still clipped, but the climber has moved further from the anchor than the lanyard
        // reaches - in reality the lanyard would stop them; here it means the hook should have been moved up.
        public bool IsOverExtended =>
            anchor != null && harness != null && (anchor.position - harness.position).sqrMagnitude > lanyardLength * lanyardLength;

        public bool IsProtecting => IsAnchored && !IsOverExtended;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
        }

        private void OnEnable()
        {
            grab.selectEntered.AddListener(HandleSelectEntered);
            grab.selectExited.AddListener(HandleSelectExited);
        }

        private void OnDisable()
        {
            grab.selectEntered.RemoveListener(HandleSelectEntered);
            grab.selectExited.RemoveListener(HandleSelectExited);
            anchor = null;
        }

        private void HandleSelectEntered(SelectEnterEventArgs args)
        {
            // Only one interactor holds the hook at a time (single select), so this replaces any old anchor.
            var interactor = args.interactorObject.transform;
            anchor = interactor.TryGetComponent<LanyardAnchor>(out _) ? interactor : null;

            if (logEvents)
                Debug.Log($"[Lanyard] {name} taken by {interactor.name}{(anchor != null ? " - CLIPPED" : "")}", this);
        }

        private void HandleSelectExited(SelectExitEventArgs args)
        {
            if (args.interactorObject.transform == anchor)
                anchor = null;

            if (logEvents)
                Debug.Log($"[Lanyard] {name} let go by {args.interactorObject.transform.name}{(args.isCanceled ? " (canceled)" : "")}", this);
        }
    }
}
