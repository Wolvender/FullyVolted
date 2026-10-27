using System;
using FullyVolted.Procedure;
using UnityEngine;
using UnityEngine.Serialization;

namespace FullyVolted.Results
{
    // Observer: listens to the procedure and records one run - a split time and mistake count per step.
    // The trainee never sees these times in VR (the training has no visible timer); they are for the
    // instructor's results page.
    public class RunRecordingObserver : MonoBehaviour, IStepObserver
    {
        private IRunStore store;
        private StepTimer timer;
        private string runId;

        [SerializeField] private ProcedureController procedureController;
        [Tooltip("Who is doing this run. Shown on the results page.")]
        [SerializeField] private string trainee = "Trainee";
        [Tooltip("Where results are saved (API config or Supabase config). Leave empty to play without saving.")]
        [FormerlySerializedAs("supabaseConfig")]
        [SerializeField] private RunStoreConfig storeConfig;

        private void Awake()
        {
            if (storeConfig == null || !storeConfig.IsFilledIn)
            {
                Debug.LogWarning("[Results] No results config filled in; this run will not be saved.", this);
                return;
            }

            store = storeConfig.CreateStore();
            runId = Guid.NewGuid().ToString();
            timer = new StepTimer(Time.realtimeSinceStartupAsDouble);
            store.BeginRun(runId, trainee);
        }

        private void OnEnable()
        {
            if (procedureController != null)
                procedureController.RegisterObserver(this);
        }

        private void OnDisable()
        {
            if (procedureController != null)
                procedureController.UnregisterObserver(this);
        }

        public void OnStepResult(StepResult result)
        {
            if (store == null)
                return;

            if (timer.TryClose(result, Time.realtimeSinceStartupAsDouble, out var step))
                store.SaveStep(runId, step);
        }
    }
}
