using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace MacroUI
{
    public partial class App : Application
    {
        private NotifyIcon _notifyIcon;
        private Process _ahkProcess;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            string ahkPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "MacroEngine.ahk");
            if (File.Exists(ahkPath))
            {
                try
                {
                    _ahkProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = ahkPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception) { }
            }

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = SystemIcons.Application;
            _notifyIcon.Visible = true;
            _notifyIcon.Text = "Aula Macro Configuration";

            var menu = new ContextMenuStrip();
            
            var configItem = new ToolStripMenuItem("Configure Macros...");
            configItem.Click += (s, args) => OpenConfigWindow();
            menu.Items.Add(configItem);

            menu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, args) => ShutdownApp();
            menu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.DoubleClick += (s, args) => OpenConfigWindow();
        }

        private void OpenConfigWindow()
        {
            foreach (Window window in Current.Windows)
            {
                if (window is ConfigWindow)
                {
                    if (window.WindowState == WindowState.Minimized)
                        window.WindowState = WindowState.Normal;
                    window.Activate();
                    return;
                }
            }

            var configWindow = new ConfigWindow();
            configWindow.Show();
        }

        private void ShutdownApp()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Current.Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_ahkProcess != null)
            {
                try { if (!_ahkProcess.HasExited) _ahkProcess.Kill(); } catch { }
            }
            
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            base.OnExit(e);
        }
    }
}
