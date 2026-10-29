using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlipFace.Runtime;

namespace FlipFace.App;

/// <summary>
/// Shows whether ViGEmBus and HidHide are ready, with download links for missing ones.
/// Opens by itself at startup when something is missing, and from the tray's "Check setup…".
/// </summary>
public partial class SetupWindow : Window
{
    private readonly Action _start;

    /// <param name="start">Starts remapping; offered once both drivers are ready.</param>
    /// <param name="running">Whether FlipFace is already remapping, in which case there's nothing to start.</param>
    public SetupWindow(Action start, bool running)
    {
        InitializeComponent();
        _start = start;
        Icon = Imaging.CreateBitmapSourceFromHIcon(TrayIcon.AppIcon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        StartButton.Visibility = running ? Visibility.Collapsed : Visibility.Visible;
        Refresh();
    }

    private void Refresh()
    {
        var status = SetupCheck.Run();
        Show(status.ViGEmBus, ViGEmIcon, ViGEmState, ViGEmDownload);
        Show(status.HidHide, HidHideIcon, HidHideState, HidHideDownload);
        RestartNote.Visibility = status.ViGEmBus == RequirementState.NeedsRestart || status.HidHide == RequirementState.NeedsRestart
            ? Visibility.Visible
            : Visibility.Collapsed;
        StartButton.IsEnabled = status.AllReady;
    }

    private void Show(RequirementState state, TextBlock icon, TextBlock text, Button download)
    {
        (icon.Text, text.Text, var brush) = state switch
        {
            RequirementState.Ready => ("✔", "Installed and running", Brushes.SeaGreen),
            RequirementState.NeedsRestart => ("⟳", "Installed. Restart your PC to finish.", (Brush)FindResource("AccentBrush")),
            _ => ("✖", "Not installed", (Brush)FindResource("ErrorBrush")),
        };
        icon.Foreground = text.Foreground = brush;
        download.Visibility = state == RequirementState.Missing ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void OpenInBrowser(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    private void ViGEmDownload_Click(object sender, RoutedEventArgs e) => OpenInBrowser(SetupCheck.ViGEmBusDownload);

    private void HidHideDownload_Click(object sender, RoutedEventArgs e) => OpenInBrowser(SetupCheck.HidHideDownload);

    private void CheckAgain_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        Close();
        _start();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
