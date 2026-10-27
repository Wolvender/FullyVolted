using UnityEngine;

namespace FullyVolted.Climbing
{
    // The rig has no body, only a head and hands. This puts a harness point at hip height under the head,
    // turned with the head's yaw only, so the hip sockets stay at your sides when you look up or down.
    public class HarnessFollower : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [Tooltip("How far below the eyes the harness sits, in metres.")]
        [SerializeField, Min(0f)] private float dropBelowHead = 0.65f;

        private void LateUpdate()
        {
            if (head == null)
                return;

            var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(head.up, Vector3.up); // looking straight up or down

            transform.SetPositionAndRotation(head.position + Vector3.down * dropBelowHead,
                Quaternion.LookRotation(forward, Vector3.up));
        }
    }
}
