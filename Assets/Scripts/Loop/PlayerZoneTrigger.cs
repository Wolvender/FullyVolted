using System;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace FullyVolted.Loop
{
    [RequireComponent(typeof(Collider))]
    public class PlayerZoneTrigger : MonoBehaviour
    {
        [SerializeField] private string zoneName = "Zone";
        [SerializeField] private bool logDetections = true;

        public event Action Entered;

        public string ZoneName => zoneName;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayerBody(other))
            {
                if (logDetections)
                    Debug.Log($"[Zone] {zoneName}: ignored '{other.name}' (not the player body)", this);

                return;
            }

            if (logDetections)
                Debug.Log($"[Zone] {zoneName}: player ENTERED", this);

            Entered?.Invoke();
        }

        private void OnTriggerExit(Collider other)
        {
            if (logDetections && IsPlayerBody(other))
                Debug.Log($"[Zone] {zoneName}: player left", this);
        }

        private static bool IsPlayerBody(Collider other)
        {
            // The hands are children of the XR Origin too, so match the rig body specifically -
            // otherwise reaching an arm over the platform edge would count as arriving.
            return other is CharacterController && other.GetComponentInParent<XROrigin>() != null;
        }
    }
}
