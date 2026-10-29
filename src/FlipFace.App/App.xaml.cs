using System.IO;
using System.Windows;
using System.Windows.Threading;
using FlipFace.Runtime;

namespace FlipFace.App;

/// <summary>
/// Checks the drivers, then starts the remap session, the tray icon and (unless started with --tray) the window.
/// Closing the window only hides it; FlipFace exits from the tray menu.
/// </summary>
public partial class App : Application
{
    /// <summary>Command-line flag that opens the setup window even when everything is installed.</summary>
    private const string SetupFlag = "--setup";

    private static readonly string CrashLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlipFace", "crash.log");

    // A second launch signals this event (so the running copy shows its window) and exits.
    private const string ShowSignalName = @"Local\FlipFace.App.Show";

    private Mutex? _singleInstance;
    private EventWaitHandle? _showSignal;
    private FlipFaceController? _controller;
    private TrayIcon? _tray;
    private MainWindow? _window;
    private SetupWindow? _setup;
    private bool _startHidden;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstance = new Mutex(false, @"Local\FlipFace.App", out var firstInstance);
        if (!firstInstance)
        {
            if (EventWaitHandle.TryOpenExisting(ShowSignalName, out var running))
                running.Set();
            Shutdown();
            return;
        }
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);
        ThreadPool.RegisterWaitForSingleObject(_showSignal, (_, _) => Dispatcher.BeginInvoke(ShowWindow), null, -1, executeOnlyOnce: false);

        DispatcherUnhandledException += OnUiCrash;
        AppDomain.CurrentDomain.UnhandledException += OnBackgroundCrash;

        _controller = new FlipFaceController();
        _controller.LeftoversRestored += _ => _tray?.ShowTip("FlipFace cleaned up",
            "FlipFace didn't close properly last time and had left your controller hidden. That's fixed now.");
        _tray = new TrayIcon(_controller, ShowWindow, ShowSetup, Shutdown);
        // Windows signing out or shutting down: unhide the controller before we're killed.
        SessionEnding += (_, _) => Shutdown();

        _startHidden = e.Args.Contains(StartWithWindows.TrayFlag);
        if (SetupCheck.Run().AllReady && !e.Args.Contains(SetupFlag))
            StartRemapping();
        else
            ShowSetup();
    }

    private void StartRemapping()
    {
        _controller!.Start();
        if (!_startHidden)
            ShowWindow();
        _startHidden = false;
    }

    /// <summary>"Try again" in the main window: restart if the drivers are fine, otherwise show what's missing.</summary>
    private void Retry()
    {
        if (SetupCheck.Run().AllReady)
            _controller!.Start();
        else
            ShowSetup();
    }

    private void ShowSetup()
    {
        if (_setup is null)
        {
            _setup = new SetupWindow(StartRemapping, _controller!.IsRunning);
            _setup.Closed += (_, _) => _setup = null;
        }
        _setup.Show();
        _setup.Activate();
    }

    private void ShowWindow()
    {
        if (_window is null)
        {
            _window = new MainWindow(_controller!, Retry);
            _window.Closing += (_, args) =>
            {
                // Keep running in the tray; say so the first time.
                args.Cancel = true;
                _window.Hide();
                if (!_controller!.Settings.TrayTipShown)
                {
                    _tray!.ShowTip("FlipFace is still running", "It keeps remapping from the tray. Right-click the icon to exit.");
                    _controller.Settings.TrayTipShown = true;
                    _controller.Settings.Save();
                }
            };
        }
        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
            _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    /// <summary>
    /// A bug in the UI. Stop cleanly (which unhides the controller) instead of dying with it hidden.
    /// </summary>
    private void OnUiCrash(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        WriteCrashLog(e.Exception);
        MessageBox.Show($"FlipFace ran into a problem and has to close. Your controller is visible to games again.\n\n{e.Exception.Message}\n\nDetails were saved to {CrashLogPath}",
            "FlipFace", MessageBoxButton.OK, MessageBoxImage.Error);
        Shutdown();
    }

    /// <summary>A crash on another thread: the process is going down, so unhide synchronously while we still can.</summary>
    private static void OnBackgroundCrash(object sender, UnhandledExceptionEventArgs e)
    {
        WriteCrashLog(e.ExceptionObject);
        RemapSession.TryEmergencyRestore();
    }

    private static void WriteCrashLog(object exception)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
            File.AppendAllText(CrashLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Logging is best-effort.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _controller?.Dispose();
        _showSignal?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
