using FullyVolted.Procedure;
using UnityEngine;

namespace FullyVolted.Feedback
{
    // Observer: a soft chime for every step result, rising for correct and falling for incorrect.
    // Quiet on purpose: it tells you how it went without startling you high up a mast.
    public class StepChimes : MonoBehaviour, IStepObserver
    {
        [SerializeField] private ProcedureController procedureController;
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip correctChime;
        [SerializeField] private AudioClip incorrectChime;
        [SerializeField, Range(0f, 1f)] private float volume = 0.6f;

        private void OnEnable()
        {
            if (procedureController != null)
                procedureController.RegisterObserver(this);
        }

        private void OnDisable()
        {
            if (procedureController != null)
                procedureController.UnregisterObserver(this);
        }

        public void OnStepResult(StepResult result)
        {
            var chime = result.WasCorrect ? correctChime : incorrectChime;
            if (chime != null && source != null)
                source.PlayOneShot(chime, volume);
        }
    }
}
