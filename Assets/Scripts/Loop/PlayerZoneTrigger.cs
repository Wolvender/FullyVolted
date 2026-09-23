using System;
using UnityEngine;

namespace FullyVolted.Loop
{
    [RequireComponent(typeof(Collider))]
    public class PlayerZoneTrigger : MonoBehaviour
    {
        private Collider zoneCollider;
        private bool wasInside;

        [SerializeField] private string zoneName = "Zone";
        [SerializeField] private Transform playerHead;
        [SerializeField] private bool logDetections = true;

        public event Action Entered;

        public string ZoneName => zoneName;
        public bool IsPlayerInside => wasInside;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void Awake()
        {
            zoneCollider = GetComponent<Collider>();
        }

        private void Update()
        {
            if (playerHead == null)
                return;

            // Tracks the head rather than the rig collider: the rig only moves when a locomotion
            // provider moves it, so head-only movement (room-scale, or the simulator) would be missed.
            var inside = zoneCollider.bounds.Contains(playerHead.position);
            if (inside == wasInside)
                return;

            wasInside = inside;

            if (logDetections)
                Debug.Log($"[Zone] {zoneName}: player {(inside ? "ENTERED" : "left")}", this);

            if (inside)
                Entered?.Invoke();
        }
    }
}
