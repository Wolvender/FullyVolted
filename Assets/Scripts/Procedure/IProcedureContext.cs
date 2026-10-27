namespace FullyVolted.Procedure
{
    // What a state is allowed to do to the machine - nothing more.
    public interface IProcedureContext
    {
        void ReportStep(StepResult result);
        void TransitionTo(IProcedureState next);
    }
}
