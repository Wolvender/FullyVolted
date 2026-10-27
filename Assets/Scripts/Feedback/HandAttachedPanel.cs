using UnityEngine;

namespace FullyVolted.Feedback
{
    // Floats a world-space panel just above a hand and turns it to face the player's eyes, so it reads like
    // a watch screen on the wrist. Only moves the panel; GazeFade decides when it can be seen.
    public class HandAttachedPanel : MonoBehaviour
    {
        [SerializeField] private Transform hand;
        [SerializeField] private Transform head;
        [SerializeField] private float heightAboveHand = 0.2f;
        [Tooltip("Pulled this far from the hand toward the eyes, so the panel never sits inside the controller.")]
        [SerializeField] private float towardHead = 0.1f;

        private void LateUpdate()
        {
            if (hand == null || head == null)
                return;

            var position = hand.position + Vector3.up * heightAboveHand;
            position += (head.position - position).normalized * towardHead;

            var away = position - head.position;
            if (away.sqrMagnitude < 0.0001f)
                return;

            transform.SetPositionAndRotation(position, Quaternion.LookRotation(away, Vector3.up));
        }
    }
}
