using System;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace FullyVolted.Procedure
{
    // Watches a fixed list of stock XRI interactables and checks they are selected in list order.
    // Knows nothing about PPE, switches or StepResults - the states decide what the order means.
    public class OrderedInteractionSequence
    {
        private readonly XRBaseInteractable[] items;
        private int nextIndex;

        public event Action<XRBaseInteractable, XRBaseInteractable> OutOfOrder; // (expected, actual)
        public event Action Completed;

        public OrderedInteractionSequence(XRBaseInteractable[] items)
        {
            this.items = items;
        }

        public void StartListening()
        {
            nextIndex = 0;
            for (int i = 0; i < items.Length; i++)
                items[i].selectEntered.AddListener(HandleSelected);
        }

        public void StopListening()
        {
            for (int i = 0; i < items.Length; i++)
                items[i].selectEntered.RemoveListener(HandleSelected);
        }

        private void HandleSelected(SelectEnterEventArgs args)
        {
            var index = IndexOf(args.interactableObject);

            // Grabbing something already done again is harmless (index < nextIndex, which also
            // covers "not in the list" = -1). A wrong-order pick doesn't count and doesn't advance.
            if (index < nextIndex || nextIndex >= items.Length)
                return;

            if (index > nextIndex)
            {
                OutOfOrder?.Invoke(items[nextIndex], items[index]);
                return;
            }

            nextIndex++;
            if (nextIndex == items.Length)
                Completed?.Invoke();
        }

        private int IndexOf(IXRSelectInteractable interactable)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (ReferenceEquals(items[i], interactable))
                    return i;
            }

            return -1;
        }
    }
}
