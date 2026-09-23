using UnityEngine;

namespace FullyVolted.Procedure
{
    public class ProcedureCompleteState : IProcedureState
    {
        private readonly GameObject completeScreen;

        public ProcedureCompleteState(GameObject completeScreen)
        {
            this.completeScreen = completeScreen;
        }

        public void Enter()
        {
            if (completeScreen != null)
                completeScreen.SetActive(true);
        }

        public void Exit()
        {
            if (completeScreen != null)
                completeScreen.SetActive(false);
        }
    }
}
