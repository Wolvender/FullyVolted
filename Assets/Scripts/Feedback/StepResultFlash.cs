using FullyVolted.Procedure;
using UnityEngine;

namespace FullyVolted.Feedback
{
    // Observer: turns every step result into a green (correct) or red (incorrect) vision flash.
    public class StepResultFlash : MonoBehaviour, IStepObserver
    {
        [SerializeField] private ProcedureController procedureController;
        [SerializeField] private VolumeFlash correctFlash;
        [SerializeField] private VolumeFlash incorrectFlash;

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
            var flash = result.WasCorrect ? correctFlash : incorrectFlash;
            if (flash != null)
                flash.Play();
        }
    }
}
