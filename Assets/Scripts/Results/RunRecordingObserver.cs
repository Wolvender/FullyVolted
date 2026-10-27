using System;
using System.Collections.Generic;
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
        private bool runBegun;

        // Steps that finished before the run was announced (the name is still being fetched). Sent right after it.
        private readonly List<StepRecord> waiting = new List<StepRecord>();

        [SerializeField] private ProcedureController procedureController;
        [Tooltip("Used when nobody has signed in on the portal (or the API is not reachable).")]
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

            var lookup = storeConfig.CreateTraineeLookup();
            if (lookup == null)
                BeginRun(null);
            else
                lookup.Fetch(BeginRun);
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

            if (!timer.TryClose(result, Time.realtimeSinceStartupAsDouble, out var step))
                return;

            if (runBegun)
                store.SaveStep(runId, step);
            else
                waiting.Add(step);
        }

        // Called with the signed-in name, or null when there is none: then the name set above is used.
        private void BeginRun(string signedIn)
        {
            if (this == null)
                return;

            var name = string.IsNullOrWhiteSpace(signedIn) ? trainee : signedIn;
            Debug.Log($"[Results] Recording this run for '{name}'" + (signedIn == null ? " (nobody signed in on the portal)." : "."));

            store.BeginRun(runId, name);
            runBegun = true;
            foreach (var step in waiting)
                store.SaveStep(runId, step);
            waiting.Clear();
        }
    }
}
