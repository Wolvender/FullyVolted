using System.Collections.Generic;
using FullyVolted.Loop;
using UnityEngine;

namespace FullyVolted.Procedure
{
    public class ProcedureController : MonoBehaviour
    {
        private readonly List<IStepObserver> observers = new List<IStepObserver>();
        private IProcedureState currentState;

        [SerializeField] private PlayerZoneTrigger topZone;
        [SerializeField] private PlayerZoneTrigger bottomZone;
        [SerializeField] private GameObject completeScreen;
        [SerializeField] private Transform playerRig;
        [SerializeField] private Transform startPose;

        private void Start()
        {
            if (completeScreen != null)
                completeScreen.SetActive(false);

            TransitionTo(new ClimbState(this, topZone, bottomZone));
        }

        private void OnDestroy()
        {
            currentState?.Exit();
            currentState = null;
        }

        public void RegisterObserver(IStepObserver observer)
        {
            if (!observers.Contains(observer))
                observers.Add(observer);
        }

        public void UnregisterObserver(IStepObserver observer)
        {
            observers.Remove(observer);
        }

        public void ReportStep(StepResult result)
        {
            for (int i = 0; i < observers.Count; i++)
                observers[i].OnStepResult(result);
        }

        public void TransitionTo(IProcedureState next)
        {
            currentState?.Exit();
            currentState = next;
            currentState?.Enter();
        }

        public void CompleteProcedure()
        {
            TransitionTo(new ProcedureCompleteState(completeScreen));
        }

        public void RestartProcedure()
        {
            MoveRigToStart();
            TransitionTo(new ClimbState(this, topZone, bottomZone));
        }

        private void MoveRigToStart()
        {
            if (playerRig == null || startPose == null)
                return;

            // A CharacterController overwrites direct transform writes, so it has to be off while we move.
            var characterController = playerRig.GetComponent<CharacterController>();
            if (characterController != null)
                characterController.enabled = false;

            playerRig.SetPositionAndRotation(startPose.position, startPose.rotation);

            if (characterController != null)
                characterController.enabled = true;
        }
    }
}
