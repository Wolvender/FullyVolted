using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

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

        // Raised when the hook clicks onto an anchor and when it leaves one, so feedback (sound, haptics) can react
        // without knowing anything about sockets.
        public event Action<Carabiner> Clipped;
        public event Action<Carabiner> Unclipped;

        // The hand that held the hook last, i.e. the one that just clipped or unclipped it.
        public IXRSelectInteractor LastHolder { get; private set; }

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
            if (anchor == null)
                LastHolder = args.interactorObject;

            if (logEvents)
                Debug.Log($"[Lanyard] {name} taken by {interactor.name}{(anchor != null ? " - CLIPPED" : "")}", this);

            if (anchor != null)
                Clipped?.Invoke(this);
        }

        private void HandleSelectExited(SelectExitEventArgs args)
        {
            var wasClipped = args.interactorObject.transform == anchor;
            if (wasClipped)
                anchor = null;

            if (logEvents)
                Debug.Log($"[Lanyard] {name} let go by {args.interactorObject.transform.name}{(args.isCanceled ? " (canceled)" : "")}", this);

            if (wasClipped)
                Unclipped?.Invoke(this);
        }
    }
}
