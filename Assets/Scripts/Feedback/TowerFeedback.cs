using FullyVolted.Climbing;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Climbing;

namespace FullyVolted.Feedback
{
    // The tower talks back: a dull metal "tonk" when a hand takes a rung, a sharp click (and a click in that hand's
    // controller) when a hook clips onto an anchor, and now and then a creak from the steel, more often the higher
    // you are. Sounds are 3D, placed where the thing happened.
    public class TowerFeedback : MonoBehaviour
    {
        private IHandHaptics haptics;
        private float climb;
        private float creakTimer;

        [SerializeField] private HandHaptics handHaptics;
        [SerializeField] private XRBaseInteractor[] hands;
        [SerializeField] private Carabiner[] carabiners;
        [Tooltip("The player's head. Its height decides how often the tower creaks.")]
        [SerializeField] private Transform head;
        [SerializeField] private AudioSource impactSource;
        [SerializeField] private AudioSource creakSource;
        [SerializeField] private AudioClip rungClip;
        [SerializeField] private AudioClip clipClip;
        [SerializeField] private AudioClip unclipClip;
        [SerializeField] private AudioClip[] creakClips;
        [SerializeField, Range(0f, 1f)] private float rungVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float clipVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float clipPulseAmplitude = 0.7f;
        [SerializeField, Min(0f)] private float clipPulseDuration = 0.08f;
        [Tooltip("At this height (and below) the creaks are rare and quiet.")]
        [SerializeField] private float groundHeight = 0f;
        [Tooltip("At this height (and above) the creaks are frequent and clearly audible.")]
        [SerializeField, Min(1f)] private float topHeight = 30f;
        [Tooltip("Seconds between creaks on the ground and at the top, each varied by about a third either way.")]
        [SerializeField, Min(1f)] private float groundInterval = 40f;
        [SerializeField, Min(1f)] private float topInterval = 10f;
        [SerializeField, Range(0f, 1f)] private float groundCreakVolume = 0.15f;
        [SerializeField, Range(0f, 1f)] private float topCreakVolume = 0.6f;
        [Tooltip("How far from the head a creak seems to come from, in metres.")]
        [SerializeField, Min(0.5f)] private float creakDistance = 4f;

        private void Awake()
        {
            haptics = handHaptics;
            creakTimer = NextInterval();
        }

        private void OnEnable()
        {
            foreach (var hand in hands)
            {
                if (hand != null)
                    hand.selectEntered.AddListener(HandleHandSelectEntered);
            }

            foreach (var carabiner in carabiners)
            {
                if (carabiner == null)
                    continue;
                carabiner.Clipped += HandleClipped;
                carabiner.Unclipped += HandleUnclipped;
            }
        }

        private void OnDisable()
        {
            foreach (var hand in hands)
            {
                if (hand != null)
                    hand.selectEntered.RemoveListener(HandleHandSelectEntered);
            }

            foreach (var carabiner in carabiners)
            {
                if (carabiner == null)
                    continue;
                carabiner.Clipped -= HandleClipped;
                carabiner.Unclipped -= HandleUnclipped;
            }
        }

        private void Update()
        {
            if (head == null || creakSource == null || creakClips == null || creakClips.Length == 0)
                return;

            climb = Mathf.InverseLerp(groundHeight, topHeight, head.position.y);
            creakTimer -= Time.deltaTime;
            if (creakTimer > 0f)
                return;

            creakTimer = NextInterval();
            var direction = Random.onUnitSphere;
            direction.y *= 0.3f;
            Play(creakSource, creakClips[Random.Range(0, creakClips.Length)], head.position + direction.normalized * creakDistance,
                Mathf.Lerp(groundCreakVolume, topCreakVolume, climb));
        }

        private float NextInterval()
        {
            return Mathf.Lerp(groundInterval, topInterval, climb) * Random.Range(0.67f, 1.33f);
        }

        private void HandleHandSelectEntered(SelectEnterEventArgs args)
        {
            if (args.interactableObject is ClimbInteractable)
                Play(impactSource, rungClip, args.interactableObject.transform.position, rungVolume, Random.Range(0.9f, 1.1f));
        }

        private void HandleClipped(Carabiner carabiner)
        {
            Play(impactSource, clipClip, carabiner.Anchor.position, clipVolume);
            haptics?.PulseHand(carabiner.LastHolder, clipPulseAmplitude, clipPulseDuration);
        }

        // The hand that takes the hook off gets the stock grab buzz, so only the sound is added here.
        private void HandleUnclipped(Carabiner carabiner)
        {
            Play(impactSource, unclipClip, carabiner.transform.position, clipVolume * 0.7f);
        }

        private static void Play(AudioSource source, AudioClip clip, Vector3 position, float volume, float pitch = 1f)
        {
            if (source == null || clip == null)
                return;

            source.transform.position = position;
            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
        }
    }
}
