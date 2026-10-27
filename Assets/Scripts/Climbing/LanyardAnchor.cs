using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace FullyVolted.Climbing
{
    // Marks a stock XRI socket as a fall-arrest anchor point. A carabiner clipped into this socket protects
    // the climber; the same socket type on the harness (the hip holsters) does not.
    [RequireComponent(typeof(XRSocketInteractor))]
    public class LanyardAnchor : MonoBehaviour
    {
        private XRSocketInteractor socket;

        [Tooltip("Log when a carabiner comes into reach of this anchor (hover) and when it clips in.")]
        [SerializeField] private bool logEvents = true;

        private void Awake()
        {
            socket = GetComponent<XRSocketInteractor>();
        }

        private void OnEnable()
        {
            socket.hoverEntered.AddListener(HandleHoverEntered);
            socket.hoverExited.AddListener(HandleHoverExited);
        }

        private void OnDisable()
        {
            socket.hoverEntered.RemoveListener(HandleHoverEntered);
            socket.hoverExited.RemoveListener(HandleHoverExited);
        }

        private void HandleHoverEntered(HoverEnterEventArgs args)
        {
            if (logEvents)
                Debug.Log($"[Lanyard] {args.interactableObject.transform.name} in reach of {name}", this);
        }

        private void HandleHoverExited(HoverExitEventArgs args)
        {
            if (logEvents)
                Debug.Log($"[Lanyard] {args.interactableObject.transform.name} out of reach of {name}", this);
        }
    }
}
