using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace FullyVolted.Feedback
{
    // Plays the lever's own pull-down animation when the player grabs it. The procedure counts the grab
    // (it listens to the same interactable); this only makes the lever move, so it looks like the power went off.
    public class LeverPull : MonoBehaviour
    {
        private PlayableGraph graph;
        private bool pulled;

        [SerializeField] private XRBaseInteractable lever;
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationClip pullClip;

        private void OnEnable()
        {
            if (lever != null)
                lever.selectEntered.AddListener(HandleSelected);
        }

        private void OnDisable()
        {
            if (lever != null)
                lever.selectEntered.RemoveListener(HandleSelected);
        }

        private void OnDestroy()
        {
            if (graph.IsValid())
                graph.Destroy();
        }

        private void HandleSelected(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
        {
            // One pull is all there is: grabbing it again leaves it down.
            if (pulled || animator == null || pullClip == null)
                return;

            pulled = true;
            // A non-looping clip stays on its last frame, so the lever stays down.
            AnimationPlayableUtilities.PlayClip(animator, pullClip, out graph);
        }
    }
}
