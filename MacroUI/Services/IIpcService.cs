using System;
using System.Threading.Tasks;

namespace MacroUI.Services
{
    public interface IIpcService
    {
        void StartServerAsync(Action<string, string> onMessageReceived);
        void SendMessage(string message);
    }
}
