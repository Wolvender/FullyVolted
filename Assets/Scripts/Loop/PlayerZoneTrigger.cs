using System;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace FullyVolted.Loop
{
    [RequireComponent(typeof(Collider))]
    public class PlayerZoneTrigger : MonoBehaviour
    {
        [SerializeField] private string zoneName = "Zone";

        public event Action Entered;

        public string ZoneName => zoneName;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<XROrigin>() == null)
                return;

            Entered?.Invoke();
        }
    }
}
