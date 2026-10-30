using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace FullyVolted.Feedback
{
    // What the feedback scripts need from the controllers: "make a hand buzz". They depend on this, not on XRI.
    public interface IHandHaptics
    {
        void PulseBothHands(float amplitude, float duration);

        // A no-op if the interactor is not one of the player's hands.
        void PulseHand(IXRInteractor hand, float amplitude, float duration);
    }

    // Sends impulses through XRI's own HapticImpulsePlayer on each hand. Grabs and hovers already buzz by themselves
    // (SimpleHapticFeedback on the Starter Assets interactors); this is for the moments we add on top.
    public class HandHaptics : MonoBehaviour, IHandHaptics
    {
        private HapticImpulsePlayer[] players;

        [Tooltip("The left and right hand interactors of the rig.")]
        [SerializeField] private XRBaseInteractor[] hands;

        private void Awake()
        {
            players = new HapticImpulsePlayer[hands.Length];
            for (int i = 0; i < hands.Length; i++)
                players[i] = hands[i] != null ? hands[i].GetComponentInParent<HapticImpulsePlayer>(true) : null;
        }

        public void PulseBothHands(float amplitude, float duration)
        {
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] != null)
                    players[i].SendHapticImpulse(amplitude, duration);
            }
        }

        public void PulseHand(IXRInteractor hand, float amplitude, float duration)
        {
            for (int i = 0; i < hands.Length; i++)
            {
                if (ReferenceEquals(hands[i], hand) && players[i] != null)
                {
                    players[i].SendHapticImpulse(amplitude, duration);
                    return;
                }
            }
        }
    }
}
