namespace FullyVolted.Procedure
{
    public readonly struct StepResult
    {
        public readonly string StepName;
        public readonly bool WasCorrect;
        public readonly string Detail;

        public StepResult(string stepName, bool wasCorrect, string detail)
        {
            StepName = stepName;
            WasCorrect = wasCorrect;
            Detail = detail;
        }
    }
}
