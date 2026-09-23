using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace FullyVolted.Climbing
{
    [RequireComponent(typeof(XRBaseInteractable))]
    public class ClimbHoldHighlight : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock propertyBlock;
        private XRBaseInteractable interactable;
        private Renderer holdRenderer;
        private Color restColor;
        private float blend;
        private float targetBlend;

        [SerializeField] private Color highlightTint = new Color(1f, 0.93f, 0.72f);
        [SerializeField, Range(0f, 1f)] private float highlightStrength = 0.55f;
        [SerializeField] private Color glowColor = new Color(1f, 0.62f, 0.18f);
        [SerializeField, Range(0f, 4f)] private float glowIntensity = 1.6f;
        [SerializeField, Min(0.1f)] private float fadeSpeed = 9f;

        private void Awake()
        {
            interactable = GetComponent<XRBaseInteractable>();
            holdRenderer = GetComponent<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
            restColor = holdRenderer.sharedMaterial.GetColor(BaseColorId);
            ApplyBlend();
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

            blend = 0f;
            targetBlend = 0f;
            ApplyBlend();
        }

        private void Update()
        {
            if (Mathf.Approximately(blend, targetBlend))
                return;

            blend = Mathf.MoveTowards(blend, targetBlend, fadeSpeed * Time.deltaTime);
            ApplyBlend();
        }

        private void HandleHoverEntered(HoverEnterEventArgs args)
        {
            targetBlend = 1f;
        }

        private void HandleHoverExited(HoverExitEventArgs args)
        {
            // The other hand may still be hovering this hold.
            if (interactable.isHovered)
                return;

            targetBlend = 0f;
        }

        private void ApplyBlend()
        {
            var eased = blend * blend * (3f - 2f * blend);

            holdRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, Color.Lerp(restColor, highlightTint, eased * highlightStrength));
            propertyBlock.SetColor(EmissionColorId, glowColor * (eased * glowIntensity));
            holdRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
