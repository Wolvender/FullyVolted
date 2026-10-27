using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace FullyVolted.Climbing
{
    // Let go of the grabbable anywhere except in a socket and it snaps back into its home socket, instead of
    // floating in mid-air or falling off the tower.
    [RequireComponent(typeof(XRGrabInteractable))]
    public class ReturnToHolster : MonoBehaviour
    {
        private XRGrabInteractable grab;
        private float returnAt = -1f;

        [SerializeField] private XRSocketInteractor holster;
        [Tooltip("Seconds to wait after release, so a socket the hook was released into gets to take it first.")]
        [SerializeField, Min(0f)] private float returnDelay = 0.15f;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
        }

        private void OnEnable()
        {
            grab.selectExited.AddListener(HandleSelectExited);
        }

        private void OnDisable()
        {
            grab.selectExited.RemoveListener(HandleSelectExited);
            returnAt = -1f;
        }

        private void Update()
        {
            if (returnAt < 0f || Time.time < returnAt)
                return;

            returnAt = -1f;
            if (grab.isSelected || holster == null || holster.hasSelection)
                return;

            Debug.Log($"[Lanyard] {name} released outside an anchor - back to {holster.name}", this);
            grab.interactionManager.SelectEnter((IXRSelectInteractor)holster, (IXRSelectInteractable)grab);
        }

        private void HandleSelectExited(SelectExitEventArgs args)
        {
            // Also fires when the scene unloads; the timer then simply never runs out.
            if (!args.isCanceled)
                returnAt = Time.time + returnDelay;
        }
    }
}
