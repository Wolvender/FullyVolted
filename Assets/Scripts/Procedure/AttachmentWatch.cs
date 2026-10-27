using System;
using FullyVolted.Climbing;
using FullyVolted.Loop;

namespace FullyVolted.Procedure
{
    // The continuous-attachment rule: anywhere except the safe zones (the ground, the middle platform and the top deck),
    // the climber must be protected by a carabiner. Raises Violated once each time the rule is broken.
    // Knows nothing about step names or results - the climb and descend states decide what it means.
    public class AttachmentWatch
    {
        private readonly IAttachmentStatus attachment;
        private readonly PlayerZoneTrigger[] safeZones;
        private bool isViolating;

        public event Action Violated;

        public bool HadViolation { get; private set; }

        public AttachmentWatch(IAttachmentStatus attachment, params PlayerZoneTrigger[] safeZones)
        {
            this.attachment = attachment;
            this.safeZones = safeZones;
        }

        public void StartWatching()
        {
            HadViolation = false;
            isViolating = false;

            attachment.ProtectionChanged += HandleProtectionChanged;
            foreach (var zone in safeZones)
            {
                zone.Entered += Evaluate;
                zone.Exited += Evaluate;
            }

            Evaluate();
        }

        public void StopWatching()
        {
            attachment.ProtectionChanged -= HandleProtectionChanged;
            foreach (var zone in safeZones)
            {
                zone.Entered -= Evaluate;
                zone.Exited -= Evaluate;
            }
        }

        private void HandleProtectionChanged(bool isProtected)
        {
            Evaluate();
        }

        private void Evaluate()
        {
            var violating = !attachment.IsProtected && !IsInSafeZone();
            if (violating == isViolating)
                return;

            isViolating = violating;
            if (!violating)
                return;

            HadViolation = true;
            Violated?.Invoke();
        }

        private bool IsInSafeZone()
        {
            foreach (var zone in safeZones)
            {
                if (zone.IsPlayerInside)
                    return true;
            }

            return false;
        }
    }
}
