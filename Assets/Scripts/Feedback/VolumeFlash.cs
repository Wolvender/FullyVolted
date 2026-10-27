using UnityEngine;
using UnityEngine.Rendering;

namespace FullyVolted.Feedback
{
    // Briefly fades a stock URP Volume in and out. Knows nothing about the procedure.
    [RequireComponent(typeof(Volume))]
    public class VolumeFlash : MonoBehaviour
    {
        private Volume volume;
        private float elapsed;

        [SerializeField, Tooltip("Seconds for the whole fade in + out.")]
        private float duration = 0.6f;

        [SerializeField, Tooltip("Volume weight over normalised time (0-1). Fast in, slower out.")]
        private AnimationCurve weightOverTime = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0f));

        private void Awake()
        {
            volume = GetComponent<Volume>();
            volume.weight = 0f;

            // Only runs Update while a flash is playing - zero cost the rest of the time.
            enabled = false;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            var t = elapsed / duration;

            if (t >= 1f)
            {
                enabled = false;
                return;
            }

            volume.weight = weightOverTime.Evaluate(t);
        }

        private void OnDisable()
        {
            volume.weight = 0f;
        }

        public void Play()
        {
            // Restarts cleanly if a flash of the same colour is already running.
            elapsed = 0f;
            enabled = true;
        }
    }
}
