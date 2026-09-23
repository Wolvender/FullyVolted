using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace FullyVolted.Climbing
{
    [RequireComponent(typeof(XRBaseInteractable))]
    public class ClimbHoldHighlight : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private MaterialPropertyBlock propertyBlock;
        private XRBaseInteractable interactable;
        private Renderer holdRenderer;
        private Color restColor;

        [SerializeField] private Color highlightTint = Color.white;
        [SerializeField, Range(0f, 1f)] private float highlightStrength = 0.4f;

        private void Awake()
        {
            interactable = GetComponent<XRBaseInteractable>();
            holdRenderer = GetComponent<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
            restColor = holdRenderer.sharedMaterial.GetColor(BaseColorId);
        }

        private void OnEnable()
        {
            interactable.hoverEntered.AddListener(HandleHoverEntered);
            interactable.hoverExited.AddListener(HandleHoverExited);
        }

        private void OnDisable()
        {
            interactable.hoverEntered.RemoveListener(HandleHoverEntered);
            interactable.hoverExited.RemoveListener(HandleHoverExited);
            ApplyColor(restColor);
        }

        private void HandleHoverEntered(HoverEnterEventArgs args)
        {
            ApplyColor(Color.Lerp(restColor, highlightTint, highlightStrength));
        }

        private void HandleHoverExited(HoverExitEventArgs args)
        {
            // The other hand may still be hovering this hold.
            if (interactable.isHovered)
                return;

            ApplyColor(restColor);
        }

        private void ApplyColor(Color color)
        {
            holdRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            holdRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
