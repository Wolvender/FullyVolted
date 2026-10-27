using UnityEngine;

namespace FullyVolted.Climbing
{
    // Stops this object's colliders from physically touching the given colliders. The carabiners need solid
    // colliders for the hands to find them, but must never shove the player's CharacterController.
    public class IgnoreCollisionWith : MonoBehaviour
    {
        [SerializeField] private Collider[] others;

        private void Awake()
        {
            var own = GetComponentsInChildren<Collider>(true);
            foreach (var other in others)
            {
                if (other == null)
                    continue;

                foreach (var collider in own)
                    Physics.IgnoreCollision(collider, other, true);
            }
        }
    }
}
