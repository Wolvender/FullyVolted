using System.Collections.Generic;

namespace FullyVolted.Results
{
    // One finished part of a run: what gets stored per step.
    public readonly struct StepRecord
    {
        public readonly int Order;
        public readonly string StepName;
        public readonly double Seconds;
        public readonly int Mistakes;
        public readonly bool Correct;

        // One line per mistake, in the order they happened ("Did Ground And Lock before Switch Off.").
        public readonly IReadOnlyList<string> MistakeReasons;

        public StepRecord(int order, string stepName, double seconds, int mistakes, bool correct, IReadOnlyList<string> mistakeReasons)
        {
            Order = order;
            StepName = stepName;
            Seconds = seconds;
            Mistakes = mistakes;
            Correct = correct;
            MistakeReasons = mistakeReasons;
        }
    }
}
