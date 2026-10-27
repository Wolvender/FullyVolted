using UnityEngine;

namespace FullyVolted.Atmosphere
{
    // A slow red warning light, like the ones on top of real masts: the bulb glows and a small point light
    // pulses with it. Purely atmosphere; nothing in the procedure depends on it.
    public class BlinkingBeacon : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock block;

        [SerializeField] private Renderer bulb;
        [SerializeField] private Light glow;
        [ColorUsage(false, true)]
        [SerializeField] private Color emission = new Color(4f, 0.15f, 0.1f);
        [Tooltip("Seconds for one full on/off cycle.")]
        [SerializeField, Min(0.2f)] private float period = 2f;
        [SerializeField, Min(0f)] private float maxLightIntensity = 6f;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            // Mostly off, with a soft flash: the sine is squeezed so it spends little time lit.
            var wave = Mathf.Sin(Time.time / period * Mathf.PI * 2f) * 0.5f + 0.5f;
            var level = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, wave));

            if (bulb != null)
            {
                bulb.GetPropertyBlock(block);
                block.SetColor(EmissionColorId, emission * level);
                bulb.SetPropertyBlock(block);
            }

            if (glow != null)
                glow.intensity = maxLightIntensity * level;
        }
    }
}
