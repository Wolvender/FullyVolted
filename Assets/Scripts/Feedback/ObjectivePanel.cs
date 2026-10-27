using FullyVolted.Procedure;
using UnityEngine;
using UnityEngine.UI;

namespace FullyVolted.Feedback
{
    // Observer: tells the trainee what the inspection is and which step they are on. Each step closes with
    // exactly one final result, so the panel moves to the next objective on every final result. The order
    // of the objectives mirrors the chain built in ProcedureInstaller.
    public class ObjectivePanel : MonoBehaviour, IStepObserver
    {
        [SerializeField] private ProcedureController procedureController;
        [SerializeField] private Text missionText;
        [SerializeField] private Text stepCounterText;
        [SerializeField] private Text titleText;
        [SerializeField] private Text instructionText;
        [Tooltip("In the order of the procedure. One entry per step.")]
        [SerializeField] private Objective[] objectives;
        [SerializeField] private string missionLine = "INSPECT THE TOWER: cut the power at the top";
        [SerializeField] private Objective finished = new Objective("Inspection complete", "Well done. Your results are saved.");

        private int index;

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

        private void Start()
        {
            if (missionText != null)
                missionText.text = missionLine;

            Show();
        }

        public void OnStepResult(StepResult result)
        {
            if (!result.IsFinal)
                return;

            index++;
            Show();
        }

        private void Show()
        {
            bool done = index >= objectives.Length;
            var objective = done ? finished : objectives[index];

            if (stepCounterText != null)
                stepCounterText.text = done ? "DONE" : $"STEP {index + 1} OF {objectives.Length}";
            if (titleText != null)
                titleText.text = objective.title;
            if (instructionText != null)
                instructionText.text = objective.instruction;
        }
    }
}
