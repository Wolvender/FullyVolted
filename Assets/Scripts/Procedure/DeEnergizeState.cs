using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace FullyVolted.Procedure
{
    public class DeEnergizeState : IProcedureState
    {
        private const string StepName = "DeEnergize";

        private readonly IProcedureContext controller;
        private readonly OrderedInteractionSequence sequence;
        private readonly IProcedureState next;

        public DeEnergizeState(IProcedureContext controller, OrderedInteractionSequence sequence, IProcedureState next)
        {
            this.controller = controller;
            this.sequence = sequence;
            this.next = next;
        }

        public void Enter()
        {
            sequence.OutOfOrder += HandleOutOfOrder;
            sequence.Completed += HandleCompleted;
            sequence.StartListening();
        }

        public void Exit()
        {
            sequence.StopListening();
            sequence.OutOfOrder -= HandleOutOfOrder;
            sequence.Completed -= HandleCompleted;
        }

        private void HandleOutOfOrder(XRBaseInteractable expected, XRBaseInteractable actual)
        {
            controller.ReportStep(new StepResult(StepName, false, $"Did {actual.name} before {expected.name}."));
        }

        private void HandleCompleted()
        {
            controller.ReportStep(new StepResult(StepName, true, "Switched off, tested for voltage, then grounded and locked.", isFinal: true));
            controller.TransitionTo(next);
        }
    }
}
