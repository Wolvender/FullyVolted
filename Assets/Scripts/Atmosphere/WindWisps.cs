using UnityEngine;

namespace FullyVolted.Atmosphere
{
    // Keeps the wind streaks around the player. The streaks live in world space, so moving the emitter does not
    // drag them along; it only decides where new ones appear. Higher up there are more of them, to match the
    // wind sound getting stronger.
    public class WindWisps : MonoBehaviour
    {
        private ParticleSystem.EmissionModule emission;

        [Tooltip("The player's head. New wisps appear in a box around it.")]
        [SerializeField] private Transform head;
        [SerializeField] private ParticleSystem particles;
        [Tooltip("At this height (and below) there are the fewest wisps.")]
        [SerializeField] private float groundHeight = 0f;
        [Tooltip("At this height (and above) there are the most wisps.")]
        [SerializeField, Min(1f)] private float topHeight = 30f;
        [Tooltip("New wisps per second on the ground and at the top.")]
        [SerializeField, Min(0f)] private float groundRate = 14f;
        [SerializeField, Min(0f)] private float topRate = 30f;

        private void Awake()
        {
            if (particles != null)
                emission = particles.emission;
        }

        private void LateUpdate()
        {
            if (head == null || particles == null)
                return;

            transform.position = head.position;

            var climb = Mathf.InverseLerp(groundHeight, topHeight, head.position.y);
            emission.rateOverTime = Mathf.Lerp(groundRate, topRate, climb);
        }
    }
}
