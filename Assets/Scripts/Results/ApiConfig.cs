using UnityEngine;

namespace FullyVolted.Results
{
    // Where our own results API lives and the key that lets the game write to it (Api/.env, API_KEY).
    // The asset is gitignored because it holds the key. Create it via Tools > FullyVolted > Create API Config.
    [CreateAssetMenu(fileName = "Api Config", menuName = "FullyVolted/Api Config")]
    public class ApiConfig : RunStoreConfig
    {
        [Tooltip("Where the API runs. Locally: http://localhost:8000")]
        [SerializeField] private string baseUrl = "http://localhost:8000";

        [Tooltip("The API_KEY value from Api/.env. Sent in the X-Api-Key header on every write.")]
        [SerializeField] private string apiKey;

        public string BaseUrl => baseUrl.TrimEnd('/');
        public string ApiKey => apiKey;

        public override bool IsFilledIn => !string.IsNullOrWhiteSpace(baseUrl) && !string.IsNullOrWhiteSpace(apiKey);

        public override IRunStore CreateStore() => new ApiRunStore(this);
    }
}
