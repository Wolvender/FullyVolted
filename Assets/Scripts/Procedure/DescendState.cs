using FullyVolted.Climbing;
using FullyVolted.Loop;

namespace FullyVolted.Procedure
{
    public class DescendState : IProcedureState
    {
        private const string StepName = "DescendToBottom";

        private readonly IProcedureContext controller;
        private readonly PlayerZoneTrigger bottomZone;
        private readonly AttachmentWatch attachment;
        private readonly IFallStatus fall;
        private readonly IProcedureState next;
        private bool hasFallen;

        public DescendState(IProcedureContext controller, PlayerZoneTrigger bottomZone, AttachmentWatch attachment,
            IFallStatus fall, IProcedureState next)
        {
            this.controller = controller;
            this.bottomZone = bottomZone;
            this.attachment = attachment;
            this.fall = fall;
            this.next = next;
        }

        public void Enter()
        {
            hasFallen = false;
            attachment.Violated += HandleUnclipped;
            attachment.StartWatching();
            fall.Fell += HandleFell;
            bottomZone.Entered += HandleBottomReached;

            if (bottomZone.IsPlayerInside)
                HandleBottomReached();
        }

        public void Exit()
        {
            bottomZone.Entered -= HandleBottomReached;
            fall.Fell -= HandleFell;
            attachment.StopWatching();
            attachment.Violated -= HandleUnclipped;
        }

        private void HandleUnclipped()
        {
            controller.ReportStep(new StepResult(StepName, false, "Off the platform without a carabiner clipped within reach."));
        }

        private void HandleFell(bool caught)
        {
            hasFallen = true;
            controller.ReportStep(new StepResult(StepName, false, caught
                ? "Fell from the ladder - the lanyard caught the fall."
                : "Fell from the ladder without being clipped in."));
        }

        private void HandleBottomReached()
        {
            if (hasFallen)
                controller.ReportStep(new StepResult(StepName, false, "Returned to the ground, but fell on the way down.", isFinal: true));
            else if (attachment.HadViolation)
                controller.ReportStep(new StepResult(StepName, false, "Returned to the ground, but was unclipped on the way down.", isFinal: true));
            else
                controller.ReportStep(new StepResult(StepName, true, "Returned to the ground, clipped in all the way.", isFinal: true));

            controller.TransitionTo(next);
        }
    }
}
