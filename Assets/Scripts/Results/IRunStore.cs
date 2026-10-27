namespace FullyVolted.Results
{
    // Where runs end up. Supabase today; a local file or a mock in a test would plug in the same way.
    public interface IRunStore
    {
        void BeginRun(string runId, string trainee);
        void SaveStep(string runId, StepRecord step);
    }
}
