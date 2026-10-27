namespace FullyVolted.Procedure
{
    public readonly struct StepResult
    {
        public readonly string StepName;
        public readonly bool WasCorrect;
        public readonly string Detail;

        // True for the one result that closes a step (right before the state hands over to the next one).
        // Every other result is a mistake reported along the way.
        public readonly bool IsFinal;

        public StepResult(string stepName, bool wasCorrect, string detail, bool isFinal = false)
        {
            StepName = stepName;
            WasCorrect = wasCorrect;
            Detail = detail;
            IsFinal = isFinal;
        }
    }
}
