using FullyVolted.Loop;

namespace FullyVolted.Procedure
{
    public class ClimbState : IProcedureState
    {
        private readonly ProcedureController controller;
        private readonly PlayerZoneTrigger topZone;
        private readonly PlayerZoneTrigger bottomZone;
        private bool reachedTop;

        public ClimbState(ProcedureController controller, PlayerZoneTrigger topZone, PlayerZoneTrigger bottomZone)
        {
            this.controller = controller;
            this.topZone = topZone;
            this.bottomZone = bottomZone;
        }

        public void Enter()
        {
            if (topZone != null)
                topZone.Entered += HandleTopReached;

            if (bottomZone != null)
                bottomZone.Entered += HandleBottomReached;
        }

        public void Exit()
        {
            if (topZone != null)
                topZone.Entered -= HandleTopReached;

            if (bottomZone != null)
                bottomZone.Entered -= HandleBottomReached;
        }

        private void HandleTopReached()
        {
            if (reachedTop)
                return;

            reachedTop = true;
            controller.ReportStep(new StepResult("ClimbToTop", true, "Reached the work platform."));
        }

        private void HandleBottomReached()
        {
            // The player starts inside the bottom zone, so only a return trip after the top counts.
            if (!reachedTop)
                return;

            controller.ReportStep(new StepResult("DescendToBottom", true, "Returned to the ground."));
            controller.CompleteProcedure();
        }
    }
}
