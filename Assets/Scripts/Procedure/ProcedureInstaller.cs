using FullyVolted.Climbing;
using FullyVolted.Loop;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace FullyVolted.Procedure
{
    // Composition root: the one place that knows which steps exist and in what order.
    // Adding a step = one new state class + one line here. ProcedureController never changes.
    public class ProcedureInstaller : MonoBehaviour
    {
        [SerializeField] private ProcedureController controller;

        [Header("1. PPE check - in the order they must be put on")]
        [SerializeField] private XRBaseInteractable[] ppeItems;

        [Header("2 + 4. Climb and descend - checkpoints, lowest first")]
        [SerializeField] private PlayerZoneTrigger bottomZone;
        [Tooltip("The first platform, halfway up. Reaching it is its own checkpoint.")]
        [SerializeField] private PlayerZoneTrigger middleZone;
        [Tooltip("The deck at the top of the tower, where the switch panel is.")]
        [SerializeField] private PlayerZoneTrigger topZone;
        [Tooltip("Off the ground and the platforms, this must report the climber as clipped in.")]
        [SerializeField] private AttachmentMonitor attachmentMonitor;
        [Tooltip("On the rig: reports falls, and catches them while a carabiner is clipped.")]
        [SerializeField] private FallArrest fallArrest;

        [Header("3. De-energize - in the order they must be done")]
        [SerializeField] private XRBaseInteractable[] deEnergizeSteps;

        [Header("5. Complete")]
        [SerializeField] private GameObject completeScreen;

        private void Start()
        {
            if (completeScreen != null)
                completeScreen.SetActive(false);

            // Built back to front, because each state is handed the state that follows it.
            var complete = new ProcedureCompleteState(completeScreen);
            var descend = new DescendState(controller, bottomZone, CreateAttachmentWatch(), fallArrest, complete);
            var deEnergize = new DeEnergizeState(controller, new OrderedInteractionSequence(deEnergizeSteps), descend);
            var climbToTop = new ClimbState(controller, "ClimbToTop", "the top of the tower", topZone, CreateAttachmentWatch(), fallArrest, deEnergize);
            var climbToMiddle = new ClimbState(controller, "ClimbToMiddle", "the middle platform", middleZone, CreateAttachmentWatch(), fallArrest, climbToTop);
            var ppeCheck = new PpeCheckState(controller, new OrderedInteractionSequence(ppeItems), climbToMiddle);

            // Start (not Awake) so every observer has registered in its OnEnable first.
            controller.TransitionTo(ppeCheck);
        }

        // Standing on the ground, the middle platform or the top deck is safe; anywhere else needs a carabiner.
        private AttachmentWatch CreateAttachmentWatch()
        {
            return new AttachmentWatch(attachmentMonitor, bottomZone, middleZone, topZone);
        }
    }
}
