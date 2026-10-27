using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace FullyVolted.Procedure
{
    public class PpeCheckState : IProcedureState
    {
        private const string StepName = "PpeCheck";

        private readonly IProcedureContext controller;
        private readonly OrderedInteractionSequence sequence;
        private readonly IProcedureState next;

        public PpeCheckState(IProcedureContext controller, OrderedInteractionSequence sequence, IProcedureState next)
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
            controller.ReportStep(new StepResult(StepName, false, $"Put on {actual.name} before {expected.name}."));
        }

        private void HandleCompleted()
        {
            controller.ReportStep(new StepResult(StepName, true, "Helmet on.", isFinal: true));
            controller.TransitionTo(next);
        }
    }
}
