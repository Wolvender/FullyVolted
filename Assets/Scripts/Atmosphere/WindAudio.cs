using UnityEngine;

namespace FullyVolted.Atmosphere
{
    // The sound of the wind, louder, higher and more whistling the higher the player climbs. Standing on the ground
    // it is a calm breeze; on the top deck it is a strong wind. Slow gusts keep it from sounding like a loop.
    public class WindAudio : MonoBehaviour
    {
        private float climb;
        private float climbVelocity;

        [Tooltip("The player's head. Its height decides how strong the wind is.")]
        [SerializeField] private Transform head;
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioLowPassFilter lowPass;
        [Tooltip("At this height (and below) the wind is at its calmest.")]
        [SerializeField] private float groundHeight = 0f;
        [Tooltip("At this height (and above) the wind is at its strongest.")]
        [SerializeField, Min(1f)] private float topHeight = 30f;
        [SerializeField, Range(0f, 1f)] private float groundVolume = 0.15f;
        [SerializeField, Range(0f, 1f)] private float topVolume = 0.85f;
        [Tooltip("The sound is muffled on the ground (low) and clear and whistling at the top (high), in Hz.")]
        [SerializeField, Min(200f)] private float groundCutoff = 1400f;
        [SerializeField, Min(200f)] private float topCutoff = 9000f;
        [Tooltip("How long, in seconds, the wind takes to catch up while climbing.")]
        [SerializeField, Min(0.1f)] private float smoothing = 1.5f;

        private void Update()
        {
            if (head == null || source == null)
                return;

            var target = Mathf.InverseLerp(groundHeight, topHeight, head.position.y);
            climb = Mathf.SmoothDamp(climb, target, ref climbVelocity, smoothing);

            // Two slow noise gusts: one for loudness, one for how bright the wind sounds.
            var gust = Mathf.PerlinNoise(Time.time * 0.3f, 0.5f);
            var whistle = Mathf.PerlinNoise(Time.time * 0.17f, 7.3f);

            source.volume = Mathf.Lerp(groundVolume, topVolume, climb) * (0.75f + 0.5f * gust);
            source.pitch = Mathf.Lerp(0.92f, 1.08f, climb);
            if (lowPass != null)
                lowPass.cutoffFrequency = Mathf.Lerp(groundCutoff, topCutoff, climb) * (0.8f + 0.4f * whistle);
        }
    }
}
