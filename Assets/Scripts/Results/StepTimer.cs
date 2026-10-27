using System.Collections.Generic;
using FullyVolted.Procedure;

namespace FullyVolted.Results
{
    // Turns the stream of StepResults into per-step split times and mistake counts. The procedure is one
    // straight chain, so a step starts the moment the previous one closes (or the run starts).
    // Plain C#, no Unity: the clock is passed in, so it can be checked without a scene.
    public class StepTimer
    {
        private double stepStartedAt;
        private List<string> mistakeReasons = new List<string>();
        private int order;

        public StepTimer(double runStartedAt)
        {
            stepStartedAt = runStartedAt;
        }

        // Returns true when the result closes a step; record is then filled in.
        public bool TryClose(StepResult result, double now, out StepRecord record)
        {
            if (!result.IsFinal)
            {
                if (!result.WasCorrect)
                    mistakeReasons.Add(result.Detail);

                record = default;
                return false;
            }

            // A final INCORRECT ("reached the top, but fell on the way") sums up mistakes that were
            // already counted when they happened, so it does not add another one.
            record = new StepRecord(++order, result.StepName, now - stepStartedAt, mistakeReasons.Count, result.WasCorrect, mistakeReasons);
            stepStartedAt = now;
            mistakeReasons = new List<string>();
            return true;
        }
    }
}
