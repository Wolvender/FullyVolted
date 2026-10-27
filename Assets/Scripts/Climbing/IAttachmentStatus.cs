using System;

namespace FullyVolted.Climbing
{
    // "Is the climber protected against a fall right now?" - all the procedure needs to know about the
    // lanyard, so the states don't depend on carabiners, sockets or XRI at all.
    public interface IAttachmentStatus
    {
        bool IsProtected { get; }
        event Action<bool> ProtectionChanged;
    }
}
