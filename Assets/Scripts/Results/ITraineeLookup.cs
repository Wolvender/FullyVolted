using System;

namespace FullyVolted.Results
{
    // Finds out who is at the station, so a run is saved under their name. The trainee types it on the portal
    // (typing in VR is miserable); the game only asks for it. Calls back with null when there is no answer.
    public interface ITraineeLookup
    {
        void Fetch(Action<string> onFetched);
    }
}
