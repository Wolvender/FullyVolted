using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Climbing;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;

namespace FullyVolted.Climbing
{
    // The lanyard catching a fall. Let go of the ladder while clipped in and you drop until the lanyard is
    // taut, then hang from the anchor until you grab a hold again; not clipped in, you fall to the ground.
    // Built on stock XRI locomotion: gravity is paused through IGravityController (the same hook ClimbProvider
    // uses) and the rig is moved through the stock body transformer, so it never fights the other providers.
    // Only acts while neither climbing nor standing on something, so it can't tug at a climber or a walker.
    public class FallArrest : LocomotionProvider, IGravityController, IFallStatus
    {
        // The lanyard counts as taut this close to its full length, so hanging doesn't flicker on and off.
        private const float TautTolerance = 0.02f;

        private readonly XROriginMovement movement = new XROriginMovement();
        private float highestPoint = float.NaN;
        private bool hasFallen;
        private bool isHanging;

        [SerializeField] private Carabiner[] carabiners;
        [SerializeField] private Transform harness;
        [SerializeField] private ClimbProvider climbProvider;
        [SerializeField] private GravityProvider gravityProvider;
        [Tooltip("Dropping further than this, while not climbing and not standing on anything, counts as a fall.")]
        [SerializeField, Min(0.1f)] private float fallDistance = 0.5f;
        [SerializeField] private bool logEvents = true;

        public event Action<bool> Fell;

        public bool canProcess => isActiveAndEnabled;
        public bool gravityPaused => isHanging;

        private void Update()
        {
            if (harness == null)
                return;

            var isClimbing = climbProvider != null && climbProvider.isLocomotionActive;
            var isGrounded = gravityProvider != null && gravityProvider.isGrounded;
            if (isClimbing || isGrounded)
            {
                Recover();
                return;
            }

            DetectFall();
            HoldOnLanyard();
        }

        public bool TryLockGravity(GravityOverride gravityOverride)
        {
            // Never locks gravity: pausing it while hanging (gravityPaused) is all this needs.
            return false;
        }

        public void RemoveGravityLock()
        {
        }

        void IGravityController.OnGroundedChanged(bool isGrounded)
        {
        }

        void IGravityController.OnGravityLockChanged(GravityOverride gravityOverride)
        {
        }

        private void DetectFall()
        {
            // Measured from the highest point since leaving the ladder or the floor, so stepping off the top
            // rung onto a platform (a short drop) is not a fall.
            var height = harness.position.y;
            if (float.IsNaN(highestPoint) || height > highestPoint)
                highestPoint = height;

            if (hasFallen || highestPoint - height < fallDistance)
                return;

            hasFallen = true;
            var caught = TryGetAnchor(out _);
            if (logEvents)
                Debug.Log($"[Fall] Fell from {highestPoint:0.0}m - {(caught ? "caught by the lanyard" : "NOT clipped in")}", this);

            Fell?.Invoke(caught);
        }

        private void HoldOnLanyard()
        {
            if (!TryGetAnchor(out var carabiner))
            {
                SetHanging(false);
                return;
            }

            var offset = harness.position - carabiner.Anchor.position;
            var distance = offset.magnitude;
            var length = carabiner.LanyardLength;
            if (distance < length - TautTolerance)
            {
                SetHanging(false);
                return;
            }

            SetHanging(true);
            if (distance <= length)
                return;

            // Pull the harness back onto the sphere the lanyard allows around the anchor.
            movement.motion = -offset * (1f - length / distance);
            if (locomotionState == LocomotionState.Moving || TryStartLocomotionImmediately())
                TryQueueTransformation(movement);
        }

        private void Recover()
        {
            highestPoint = float.NaN;
            hasFallen = false;
            SetHanging(false);
        }

        private void SetHanging(bool hanging)
        {
            if (hanging == isHanging)
                return;

            isHanging = hanging;
            if (logEvents)
                Debug.Log(hanging ? "[Fall] Hanging from the lanyard" : "[Fall] Off the lanyard", this);

            if (!hanging && locomotionState == LocomotionState.Moving)
                TryEndLocomotion();
        }

        // The highest clipped anchor: it is the one whose lanyard goes taut first.
        private bool TryGetAnchor(out Carabiner holding)
        {
            holding = null;
            foreach (var carabiner in carabiners)
            {
                if (carabiner == null || !carabiner.IsAnchored)
                    continue;

                if (holding == null || carabiner.Anchor.position.y > holding.Anchor.position.y)
                    holding = carabiner;
            }

            return holding != null;
        }
    }
}
