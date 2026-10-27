using UnityEngine;

namespace FullyVolted.Results
{
    // The seam between "where results go" and the code that records them. Each backend is a config asset
    // that knows how to build its own IRunStore, so RunRecordingObserver never names a concrete store
    // (Dependency Inversion) and a new backend is a new subclass, not an edit to the observer (Open/Closed).
    public abstract class RunStoreConfig : ScriptableObject
    {
        public abstract bool IsFilledIn { get; }

        public abstract IRunStore CreateStore();
    }
}
