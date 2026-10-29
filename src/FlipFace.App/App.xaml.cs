using System.Windows;

namespace FlipFace.App;

/// <summary>
/// Starts the remap session, the tray icon and (unless started with --tray) the window.
/// Closing the window only hides it; FlipFace exits from the tray menu.
/// </summary>
public partial class App : Application
{
    private FlipFaceController? _controller;
    private TrayIcon? _tray;
    private MainWindow? _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _controller = new FlipFaceController();
        _tray = new TrayIcon(_controller, ShowWindow, Shutdown);
        _controller.Start();
        // Windows signing out or shutting down: unhide the controller before we're killed.
        SessionEnding += (_, _) => Shutdown();

        if (!e.Args.Contains(StartWithWindows.TrayFlag))
            ShowWindow();
    }

    private void ShowWindow()
    {
        if (_window is null)
        {
            _window = new MainWindow(_controller!);
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

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _controller?.Dispose();
        base.OnExit(e);
    }
}
