using System.Collections.Generic;
using FullyVolted.Loop;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FullyVolted.Procedure
{
    public class ProcedureController : MonoBehaviour
    {
        private readonly List<IStepObserver> observers = new List<IStepObserver>();
        private IProcedureState currentState;

        [SerializeField] private PlayerZoneTrigger topZone;
        [SerializeField] private PlayerZoneTrigger bottomZone;
        [SerializeField] private GameObject completeScreen;

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
            SceneManager.LoadScene(gameObject.scene.name);
        }
    }
}
