using System;
using UnityEngine;

namespace FullyVolted.Feedback
{
    // One line of the briefing: what the trainee is doing now and how.
    [Serializable]
    public struct Objective
    {
        public string title;
        [TextArea] public string instruction;

        public Objective(string title, string instruction)
        {
            this.title = title;
            this.instruction = instruction;
        }
    }
}
