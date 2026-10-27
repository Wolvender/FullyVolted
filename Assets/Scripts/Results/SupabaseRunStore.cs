using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace FullyVolted.Results
{
    // Sends runs and steps to Supabase's auto-generated REST API (PostgREST). Only sends - the timing
    // lives in StepTimer, the wiring in RunRecordingObserver.
    public class SupabaseRunStore : IRunStore
    {
        // One request at a time, in order: a step row must never arrive before the run row it points to.
        private readonly Queue<(string table, string json)> pending = new Queue<(string, string)>();
        private readonly SupabaseConfig config;
        private bool sending;

        public SupabaseRunStore(SupabaseConfig config)
        {
            this.config = config;
        }

        public void BeginRun(string runId, string trainee)
        {
            Enqueue("runs", JsonUtility.ToJson(new RunRow { id = runId, trainee = trainee }));
        }

        public void SaveStep(string runId, StepRecord step)
        {
            var row = new StepRow
            {
                run_id = runId,
                step_order = step.Order,
                step_name = step.StepName,
                seconds = Math.Round(step.Seconds, 3),
                mistakes = step.Mistakes,
                correct = step.Correct
            };
            Enqueue("step_results", JsonUtility.ToJson(row));
        }

        private void Enqueue(string table, string json)
        {
            pending.Enqueue((table, json));
            if (!sending)
                SendNext();
        }

        private void SendNext()
        {
            if (pending.Count == 0)
            {
                sending = false;
                return;
            }

            sending = true;
            var (table, json) = pending.Dequeue();

            var request = new UnityWebRequest($"{config.ProjectUrl}/rest/v1/{table}", UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            // The new sb_publishable_ keys go in the apikey header only; they are not JWTs, so no Authorization header.
            request.SetRequestHeader("apikey", config.AnonKey);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Prefer", "return=minimal");

            request.SendWebRequest().completed += _ =>
            {
                if (request.result == UnityWebRequest.Result.Success)
                    Debug.Log($"[Results] Saved to {table}: {json}");
                else
                    Debug.LogWarning($"[Results] Saving to {table} failed ({request.responseCode} {request.error}): " +
                                     $"{request.downloadHandler.text}\nSent: {json}");

                request.Dispose();
                SendNext();
            };
        }

        // Field names match the database columns exactly (JsonUtility uses the field name as the key).
        [Serializable]
        private class RunRow
        {
            public string id;
            public string trainee;
        }

        [Serializable]
        private class StepRow
        {
            public string run_id;
            public int step_order;
            public string step_name;
            public double seconds;
            public int mistakes;
            public bool correct;
        }
    }
}
