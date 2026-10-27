using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FullyVolted.Procedure
{
    // The "context" of the State pattern: it holds the current state and swaps it, but has no idea
    // which states exist or in what order they run. That knowledge lives in ProcedureInstaller.
    public class ProcedureController : MonoBehaviour, IProcedureContext
    {
        private readonly List<IStepObserver> observers = new List<IStepObserver>();
        private IProcedureState currentState;

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

        public void RestartProcedure()
        {
            SceneManager.LoadScene(gameObject.scene.name);
        }
    }
}
