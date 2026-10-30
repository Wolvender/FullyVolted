using FullyVolted.Climbing;
using FullyVolted.Procedure;
using UnityEngine;

namespace FullyVolted.Feedback
{
    // Observer: the controllers say how it went, next to the flash and the chimes. A mistake is a firm pulse in both
    // hands, a correct step a soft one, a fall a long rumble (gentler when the lanyard catches you).
    public class HapticAlerts : MonoBehaviour, IStepObserver
    {
        private IHandHaptics haptics;
        private IFallStatus fallStatus;

        [SerializeField] private ProcedureController procedureController;
        [SerializeField] private HandHaptics handHaptics;
        [SerializeField] private FallArrest fallArrest;
        [SerializeField, Range(0f, 1f)] private float mistakeAmplitude = 0.8f;
        [SerializeField, Min(0f)] private float mistakeDuration = 0.3f;
        [SerializeField, Range(0f, 1f)] private float correctAmplitude = 0.25f;
        [SerializeField, Min(0f)] private float correctDuration = 0.15f;
        [SerializeField, Range(0f, 1f)] private float caughtFallAmplitude = 0.6f;
        [SerializeField, Range(0f, 1f)] private float freeFallAmplitude = 1f;
        [SerializeField, Min(0f)] private float fallDuration = 0.7f;

        private void Awake()
        {
            haptics = handHaptics;
            fallStatus = fallArrest;
        }

        private void OnEnable()
        {
            if (procedureController != null)
                procedureController.RegisterObserver(this);
            if (fallStatus != null)
                fallStatus.Fell += OnFell;
        }

        private void OnDisable()
        {
            if (procedureController != null)
                procedureController.UnregisterObserver(this);
            if (fallStatus != null)
                fallStatus.Fell -= OnFell;
        }

        public void OnStepResult(StepResult result)
        {
            if (haptics == null)
                return;

            if (result.WasCorrect)
                haptics.PulseBothHands(correctAmplitude, correctDuration);
            else
                haptics.PulseBothHands(mistakeAmplitude, mistakeDuration);
        }

        private void OnFell(bool caught)
        {
            haptics?.PulseBothHands(caught ? caughtFallAmplitude : freeFallAmplitude, fallDuration);
        }
    }
}
