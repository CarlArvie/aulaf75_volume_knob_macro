using System;

namespace MacroUI.Services
{
    public interface ITimer
    {
        event Action OnTick;
        void Start(int milliseconds);
        void Stop();
    }
}
