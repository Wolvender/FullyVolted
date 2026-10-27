using System;
using UnityEngine;

namespace FullyVolted.Climbing
{
    // Protected = at least one carabiner clipped to an anchor within lanyard reach. Polled every frame,
    // because over-stretching happens by climbing, not through any XRI event.
    public class AttachmentMonitor : MonoBehaviour, IAttachmentStatus
    {
        [SerializeField] private Carabiner[] carabiners;

        public bool IsProtected { get; private set; }
        public event Action<bool> ProtectionChanged;

        private void Awake()
        {
            IsProtected = Evaluate();
        }

        private void Update()
        {
            var isProtected = Evaluate();
            if (isProtected == IsProtected)
                return;

            IsProtected = isProtected;
            ProtectionChanged?.Invoke(isProtected);
        }

        private bool Evaluate()
        {
            for (int i = 0; i < carabiners.Length; i++)
            {
                if (carabiners[i] != null && carabiners[i].IsProtecting)
                    return true;
            }

            return false;
        }
    }
}
