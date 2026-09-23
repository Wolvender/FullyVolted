using UnityEngine;

namespace FullyVolted.Procedure
{
    public class DebugStepObserver : MonoBehaviour, IStepObserver
    {
        [SerializeField] private ProcedureController procedureController;

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
            Debug.Log($"[Procedure] {result.StepName}: {(result.WasCorrect ? "CORRECT" : "INCORRECT")} - {result.Detail}");
        }
    }
}
