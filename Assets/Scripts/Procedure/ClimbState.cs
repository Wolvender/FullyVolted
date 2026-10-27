using FullyVolted.Climbing;
using FullyVolted.Loop;

namespace FullyVolted.Procedure
{
    // Climbing up to one checkpoint. Used once per leg (ground -> middle platform, middle -> top deck),
    // so a new checkpoint is a new instance in ProcedureInstaller rather than a new class.
    public class ClimbState : IProcedureState
    {
        private readonly IProcedureContext controller;
        private readonly string stepName;
        private readonly string destination;
        private readonly PlayerZoneTrigger targetZone;
        private readonly AttachmentWatch attachment;
        private readonly IFallStatus fall;
        private readonly IProcedureState next;
        private bool hasFallen;

        public ClimbState(IProcedureContext controller, string stepName, string destination, PlayerZoneTrigger targetZone,
            AttachmentWatch attachment, IFallStatus fall, IProcedureState next)
        {
            this.controller = controller;
            this.stepName = stepName;
            this.destination = destination;
            this.targetZone = targetZone;
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
            targetZone.Entered += HandleTargetReached;

            // Zones only fire at the moment of entering. If the player was already up there before
            // this state began, waiting for Entered would leave the procedure stuck forever.
            if (targetZone.IsPlayerInside)
                HandleTargetReached();
        }

        public void Exit()
        {
            targetZone.Entered -= HandleTargetReached;
            fall.Fell -= HandleFell;
            attachment.StopWatching();
            attachment.Violated -= HandleUnclipped;
        }

        private void HandleUnclipped()
        {
            controller.ReportStep(new StepResult(stepName, false, "Climbing without a carabiner clipped within reach."));
        }

        private void HandleFell(bool caught)
        {
            hasFallen = true;
            controller.ReportStep(new StepResult(stepName, false, caught
                ? "Fell from the ladder - the lanyard caught the fall."
                : "Fell from the ladder without being clipped in."));
        }

        private void HandleTargetReached()
        {
            if (hasFallen)
                controller.ReportStep(new StepResult(stepName, false, $"Reached {destination}, but fell on the way up.", isFinal: true));
            else if (attachment.HadViolation)
                controller.ReportStep(new StepResult(stepName, false, $"Reached {destination}, but was unclipped on the way up.", isFinal: true));
            else
                controller.ReportStep(new StepResult(stepName, true, $"Reached {destination}, clipped in all the way.", isFinal: true));

            controller.TransitionTo(next);
        }
    }
}
