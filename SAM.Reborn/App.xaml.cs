using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;
[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]
namespace SAM.Picker.Modern;
public partial class App : Application {
  private static Mutex _mutex = null;
  [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
  private const int SW_RESTORE = 9;
  protected override void OnStartup(StartupEventArgs e) {
    const string appName = "Global\\SLAM_SteamAchievementManager_Reborn_Lock";
    bool createdNew;
    _mutex = new Mutex(true, appName, out createdNew);
    if (!createdNew) {
      Process current = Process.GetCurrentProcess();
      foreach (Process process in Process.GetProcessesByName(current.ProcessName)) {
        if (process.Id != current.Id) {
          ShowWindow(process.MainWindowHandle, SW_RESTORE);
          SetForegroundWindow(process.MainWindowHandle);
          break;
        }
      }
      Shutdown();
      return;
    }
    base.OnStartup(e);
    DispatcherUnhandledException += App_DispatcherUnhandledException;
    AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
  }
  private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) {
    LogCrash(e.Exception, "DispatcherUnhandledException");
    MessageBox.Show("An unexpected error occurred. Details have been logged to crash.log.\n\n" + e.Exception.Message, "SLAM Crash", MessageBoxButton.OK, MessageBoxImage.Error);
    e.Handled = true;
  }
  private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
    LogCrash(e.ExceptionObject as Exception, "CurrentDomain_UnhandledException");
    MessageBox.Show("A critical error occurred. Details have been logged to crash.log.\n\n" + e.ExceptionObject, "SLAM Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
  }
  public static void LogCrash(Exception ex, string source) {
    try {
      string logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
      string errorMsg = $"[{DateTime.Now}] [{source}]\n{ex?.ToString() ?? "Unknown Error"}\n--------------------------------------------------\n";
      File.AppendAllText(logFile, errorMsg);
    } catch { }
  }
}
