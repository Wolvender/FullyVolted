using UnityEngine;

namespace FullyVolted.Results
{
    // The project URL and the anon (public) key. The anon key is meant to ship in clients - what it may do
    // is limited by the row level security policies in Database/schema.sql. Never put the service_role key here.
    [CreateAssetMenu(fileName = "Supabase Config", menuName = "FullyVolted/Supabase Config")]
    public class SupabaseConfig : RunStoreConfig
    {
        [Tooltip("Project Settings > API > Project URL, e.g. https://abcd1234.supabase.co")]
        [SerializeField] private string projectUrl;

        [Tooltip("Project Settings > API > anon / public key.")]
        [SerializeField] private string anonKey;

        public string ProjectUrl => projectUrl.TrimEnd('/');
        public string AnonKey => anonKey;
        public override bool IsFilledIn => !string.IsNullOrWhiteSpace(projectUrl) && !string.IsNullOrWhiteSpace(anonKey);

        public override IRunStore CreateStore() => new SupabaseRunStore(this);
    }
}
