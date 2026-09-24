using System;
using System.Threading;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace eScratch;
public partial class App : Application
{
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var directory = e.Args.Length == 2 && e.Args[0] == "--data-directory" ? e.Args[1] : null;
        var profile = directory == null ? "" : "." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(directory).ToUpperInvariant())))[..16];
        instance = new Mutex(true, "Local\\eScratch.Wpf" + profile, out bool first);
        if (!first) { MessageBox.Show("eScratch is already running. Double-click its tray icon to open it.", "eScratch"); Shutdown(); return; }
        try
        {
            MainWindow = new MainWindow(directory == null ? null : new StateStore(directory));
            MainWindow.Show();
        }
        catch (Exception ex) { MessageBox.Show(ex.GetBaseException().Message, "eScratch could not start"); Shutdown(1); }
    }
    protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}
