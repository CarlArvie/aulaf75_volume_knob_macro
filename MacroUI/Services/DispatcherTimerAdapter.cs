using System;
using System.Windows.Threading;

namespace MacroUI.Services
{
    public class DispatcherTimerAdapter : ITimer
    {
        private readonly DispatcherTimer _timer;

        public event Action OnTick;

        public DispatcherTimerAdapter()
        {
            _timer = new DispatcherTimer();
            _timer.Tick += (s, e) => OnTick?.Invoke();
        }

        public void Start(int milliseconds)
        {
            _timer.Stop();
            _timer.Interval = TimeSpan.FromMilliseconds(milliseconds);
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }
    }
}
