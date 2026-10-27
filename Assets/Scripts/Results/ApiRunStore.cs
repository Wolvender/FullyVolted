using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace FullyVolted.Results
{
    // Sends runs and steps to our own results API (Api/, FastAPI + MySQL). Only sends - the timing lives in
    // StepTimer, the wiring in RunRecordingObserver. Swappable with SupabaseRunStore through IRunStore.
    public class ApiRunStore : IRunStore
    {
        private const int MaxReasonLength = 255;

        // One request at a time, in order: a step must never arrive before the run it belongs to.
        private readonly Queue<(string path, string json)> pending = new Queue<(string, string)>();
        private readonly ApiConfig config;
        private bool sending;

        public ApiRunStore(ApiConfig config)
        {
            this.config = config;
        }

        public void BeginRun(string runId, string trainee)
        {
            Enqueue("/runs", JsonUtility.ToJson(new RunBody { id = runId, trainee = trainee }));
        }

        public void SaveStep(string runId, StepRecord step)
        {
            var body = new StepBody
            {
                step_order = step.Order,
                step_name = step.StepName,
                seconds = Math.Round(step.Seconds, 3),
                mistakes = step.Mistakes,
                correct = step.Correct,
                mistake_reasons = ToReasons(step.MistakeReasons)
            };
            Enqueue($"/runs/{runId}/steps", JsonUtility.ToJson(body));
        }

        // The API rejects empty or over-long reasons (and the whole step with them), so clean them up first.
        private static List<string> ToReasons(IReadOnlyList<string> reasons)
        {
            var cleaned = new List<string>();
            foreach (var reason in reasons)
            {
                if (string.IsNullOrWhiteSpace(reason))
                    continue;

                cleaned.Add(reason.Length > MaxReasonLength ? reason.Substring(0, MaxReasonLength) : reason);
            }

            return cleaned;
        }

        private void Enqueue(string path, string json)
        {
            pending.Enqueue((path, json));
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
            var (path, json) = pending.Dequeue();

            var request = new UnityWebRequest($"{config.BaseUrl}{path}", UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-Api-Key", config.ApiKey);

            request.SendWebRequest().completed += _ =>
            {
                if (request.result == UnityWebRequest.Result.Success)
                    Debug.Log($"[Results] Saved to {path}: {json}");
                else
                    Debug.LogWarning($"[Results] Saving to {path} failed ({request.responseCode} {request.error}): " +
                                     $"{request.downloadHandler.text}\nSent: {json}");

                request.Dispose();
                SendNext();
            };
        }

        // Field names match the API's JSON keys exactly (JsonUtility uses the field name as the key).
        [Serializable]
        private class RunBody
        {
            public string id;
            public string trainee;
        }

        [Serializable]
        private class StepBody
        {
            public int step_order;
            public string step_name;
            public double seconds;
            public int mistakes;
            public bool correct;
            public List<string> mistake_reasons;
        }
    }
}
