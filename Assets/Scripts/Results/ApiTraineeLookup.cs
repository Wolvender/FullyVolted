using System;
using UnityEngine;
using UnityEngine.Networking;

namespace FullyVolted.Results
{
    // Asks our results API who signed in on the portal (GET /active-trainee).
    public class ApiTraineeLookup : ITraineeLookup
    {
        // Short on purpose: a slow or missing API must not hold up the start of a run.
        private const int TimeoutSeconds = 3;

        private readonly ApiConfig config;

        public ApiTraineeLookup(ApiConfig config)
        {
            this.config = config;
        }

        public void Fetch(Action<string> onFetched)
        {
            var request = UnityWebRequest.Get($"{config.BaseUrl}/active-trainee");
            request.timeout = TimeoutSeconds;

            request.SendWebRequest().completed += _ =>
            {
                string name = null;
                if (request.result == UnityWebRequest.Result.Success)
                    name = JsonUtility.FromJson<ActiveTraineeBody>(request.downloadHandler.text).name;
                else
                    Debug.LogWarning($"[Results] Could not ask who is signed in ({request.responseCode} {request.error}).");

                request.Dispose();
                onFetched(string.IsNullOrWhiteSpace(name) ? null : name.Trim());
            };
        }

        // Field name matches the API's JSON key (JsonUtility uses the field name as the key).
        [Serializable]
        private class ActiveTraineeBody
        {
            public string name;
        }
    }
}
