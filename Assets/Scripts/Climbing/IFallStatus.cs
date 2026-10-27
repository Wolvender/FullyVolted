using System;

namespace FullyVolted.Climbing
{
    // "Did the climber just fall?" - all the procedure needs to know about falling, so the states don't
    // depend on XRI locomotion at all.
    public interface IFallStatus
    {
        // Raised once per fall. True when a clipped carabiner catches it, false when nothing does.
        event Action<bool> Fell;
    }
}
