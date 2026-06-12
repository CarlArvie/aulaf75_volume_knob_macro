using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MacroUI.Services
{
    public class IpcService : IIpcService
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll")]
        static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, ref COPYDATASTRUCT lParam);

        public const uint WM_COPYDATA = 0x004A;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [StructLayout(LayoutKind.Sequential)]
        public struct COPYDATASTRUCT { public IntPtr dwData; public int cbData; public IntPtr lpData; }

        public async void StartServerAsync(Action<string, string> onMessageReceived)
        {
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        using (var server = new NamedPipeServerStream("MacroUIPipe", PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous))
                        {
                            await server.WaitForConnectionAsync();
                            using (var reader = new StreamReader(server))
                            using (var writer = new StreamWriter(server) { AutoFlush = true })
                            {
                                while (!reader.EndOfStream)
                                {
                                    string line = await reader.ReadLineAsync();
                                    if (line == null) break;

                                    string activeProcess = GetForegroundProcessName();
                                    onMessageReceived?.Invoke(line, activeProcess);
                                }
                            }
                        }
                    }
                    catch { await Task.Delay(1000); }
                }
            });
        }

        public void SendMessage(string message)
        {
            try
            {
                IntPtr hWnd = FindWindow("AutoHotkey", "AulaMacroEngine_IPC");
                if (hWnd != IntPtr.Zero)
                {
                    byte[] bytes = Encoding.Unicode.GetBytes(message + "\0");
                    COPYDATASTRUCT cds = new COPYDATASTRUCT { dwData = IntPtr.Zero, cbData = bytes.Length };
                    IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
                    Marshal.Copy(bytes, 0, ptr, bytes.Length);
                    SendMessage(hWnd, WM_COPYDATA, IntPtr.Zero, ref cds);
                    Marshal.FreeHGlobal(ptr);
                }
            }
            catch { }
        }

        private string GetForegroundProcessName()
        {
            try
            {
                IntPtr hWnd = GetForegroundWindow();
                if (hWnd == IntPtr.Zero) return null;
                GetWindowThreadProcessId(hWnd, out uint processId);
                IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
                if (hProcess == IntPtr.Zero) return null;

                uint capacity = 1024;
                StringBuilder sb = new StringBuilder((int)capacity);
                if (QueryFullProcessImageName(hProcess, 0, sb, ref capacity))
                {
                    CloseHandle(hProcess);
                    return Path.GetFileName(sb.ToString());
                }
                CloseHandle(hProcess);
            }
            catch { }
            return null;
        }
    }
}
