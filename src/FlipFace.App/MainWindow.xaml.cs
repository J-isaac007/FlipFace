using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using FlipFace.Core;

namespace FlipFace.App;

public partial class MainWindow : Window
{
    private const int MaxLogLines = 200;

    // Everything a control can send, in the order the editor offers them; null means "Nothing".
    private static readonly PadControl?[] Targets =
    [
        PadControl.South, PadControl.East, PadControl.West, PadControl.North,
        PadControl.LeftShoulder, PadControl.RightShoulder, PadControl.LeftTrigger, PadControl.RightTrigger,
        PadControl.Back, PadControl.Start, PadControl.Guide, PadControl.LeftStick, PadControl.RightStick,
        PadControl.DPadUp, PadControl.DPadDown, PadControl.DPadLeft, PadControl.DPadRight,
        null,
    ];

    private readonly FlipFaceController _controller;
    private readonly ControllerDiagram _diagram = new();
    private readonly Dictionary<Button, PadControl?> _choiceButtons = [];
    private PadState _lastPhysical;
    // Set while code (not the user) is changing controls, so their change handlers don't edit the profile.
    // Starts true because loading the XAML already fires change events (e.g. sliders coercing to their minimum).
    private bool _refreshing = true;

    public MainWindow(FlipFaceController controller)
    {
        InitializeComponent();
        _controller = controller;
        Icon = Imaging.CreateBitmapSourceFromHIcon(TrayIcon.AppIcon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

        DiagramHost.Content = _diagram;
        _diagram.ControlClicked += Select;
        foreach (var target in Targets)
        {
            var button = new Button { Content = Labels.Xbox(target), MinWidth = 58, Margin = new Thickness(0, 0, 6, 6) };
            button.Click += (_, _) => SetTarget(target);
            _choiceButtons[button] = target;
            Choices.Children.Add(button);
        }

        controller.StateChanged += Refresh;
        controller.SnapshotChanged += OnSnapshot;
        controller.Log += AddLog;
        Closed += (_, _) =>
        {
            controller.StateChanged -= Refresh;
            controller.SnapshotChanged -= OnSnapshot;
            controller.Log -= AddLog;
        };
        Refresh();
    }

    private Profile Profile => _controller.Profile;

    private void Refresh()
    {
        _refreshing = true;
        try
        {
            RefreshStatus();
            RefreshProfileBar();
            _diagram.ShowProfile(Profile);
            _diagram.ShowState(_controller.Snapshot, Profile);
            RefreshEditor();
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void RefreshStatus()
    {
        var snapshot = _controller.Snapshot;
        RemapToggle.IsChecked = _controller.Enabled;
        StartupToggle.IsChecked = StartWithWindows.IsEnabled;
        RetryButton.Visibility = _controller.StartError is null ? Visibility.Collapsed : Visibility.Visible;
        StatusText.Foreground = (System.Windows.Media.Brush)FindResource(_controller.StartError is null ? "MutedBrush" : "ErrorBrush");
        StatusText.Text = _controller.StartError
            ?? (snapshot.Controller is not { } pad ? "No controller found. Plug yours in and FlipFace will pick it up."
                : snapshot.Remapping ? $"Remapping \"{pad.Name}\". Games see a virtual Xbox 360 controller."
                : _controller.Enabled ? $"Starting to remap \"{pad.Name}\"…"
                : $"\"{pad.Name}\" is connected. Remapping is off, so games see it as it is.");
    }

    private void RefreshProfileBar()
    {
        var names = _controller.Profiles().Select(p => p.Name).ToList();
        if (!names.Contains(Profile.Name, StringComparer.OrdinalIgnoreCase))
            names.Add(Profile.Name);
        ProfileBox.ItemsSource = names;
        ProfileBox.SelectedItem = names.First(n => string.Equals(n, Profile.Name, StringComparison.OrdinalIgnoreCase));

        UnsavedText.Visibility = _controller.HasUnsavedChanges ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.IsEnabled = _controller.HasUnsavedChanges;
        RevertButton.IsEnabled = _controller.HasUnsavedChanges;

        var isPreset = Presets.All.Any(p => string.Equals(p.Name, Profile.Name, StringComparison.OrdinalIgnoreCase));
        DeleteButton.Content = isPreset ? "Reset to built-in" : "Delete";
        DeleteButton.IsEnabled = _controller.Store.FileFor(Profile.Name) is not null;
    }

    private void RefreshEditor()
    {
        SwapSticksBox.IsChecked = Profile.SwapSticks;
        ThresholdSlider.Value = Profile.TriggerThreshold;
        ThresholdValue.Text = $"{Profile.TriggerThreshold:P0}";

        if (_diagram.Selected is not { } selected)
        {
            SelectedTitle.Text = "Pick a button";
            SelectedHint.Text = "Click a button on the picture, or press it on your controller, to change what it sends.";
            BindingPanel.Visibility = StickPanel.Visibility = TriggerPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var target = Profile.Buttons.TryGetValue(selected, out var mapped) ? mapped : selected;
        SelectedTitle.Text = $"Printed \"{Labels.Printed(selected)}\"";
        SelectedHint.Text = target == selected
            ? $"The {Labels.Where(selected)}. Sends Xbox {Labels.Xbox(selected)}, same as the hardware."
            : $"The {Labels.Where(selected)}. Changed: sends {Labels.Xbox(target)} instead of {Labels.Xbox(selected)}.";
        BindingPanel.Visibility = Visibility.Visible;
        foreach (var (button, choice) in _choiceButtons)
        {
            var current = choice == target;
            button.FontWeight = current ? FontWeights.Bold : FontWeights.Normal;
            button.Background = current ? (System.Windows.Media.Brush)FindResource("AccentBrush") : null;
            button.Foreground = current ? System.Windows.Media.Brushes.White : (System.Windows.Media.Brush)FindResource("TextBrush");
            // Restore the default look for unselected buttons.
            if (!current)
                button.ClearValue(BackgroundProperty);
        }

        var stick = SelectedStick();
        StickPanel.Visibility = stick is null ? Visibility.Collapsed : Visibility.Visible;
        if (stick is not null)
        {
            StickHeading.Text = selected == PadControl.LeftStick ? "Left stick" : "Right stick";
            DeadzoneSlider.Value = stick.Deadzone;
            DeadzoneValue.Text = $"{stick.Deadzone:P0}";
            InvertXBox.IsChecked = stick.InvertX;
            InvertYBox.IsChecked = stick.InvertY;
        }
        TriggerPanel.Visibility = selected.IsTrigger() ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Settings for the selected stick, or null when the selection isn't a stick.</summary>
    private StickSettings? SelectedStick() => _diagram.Selected switch
    {
        PadControl.LeftStick => Profile.LeftStick,
        PadControl.RightStick => Profile.RightStick,
        _ => null,
    };

    private void Select(PadControl control)
    {
        _diagram.Selected = control;
        Refresh();
    }

    private void OnSnapshot()
    {
        if (!IsVisible)
            return;
        var snapshot = _controller.Snapshot;
        _diagram.ShowState(snapshot, Profile);

        // Pressing a button on the controller while this window is focused selects it.
        var physical = snapshot.Physical;
        if (IsActive)
        {
            foreach (var control in PadControls.All)
            {
                if (IsHeld(physical, control) && !IsHeld(_lastPhysical, control))
                {
                    if (_diagram.Selected != control)
                        Select(control);
                    break;
                }
            }
        }
        _lastPhysical = physical;
    }

    private static bool IsHeld(PadState state, PadControl control) =>
        control.IsTrigger() ? state[control.AsTriggerAxis()] > short.MaxValue / 2 : state.IsDown(control.AsButton());

    private void SetTarget(PadControl? target)
    {
        if (_diagram.Selected is not { } selected)
            return;
        _controller.Edit(p =>
        {
            var buttons = new Dictionary<PadControl, PadControl?>(p.Buttons);
            // Keep profiles minimal: a control that sends itself needs no entry.
            if (target == selected)
                buttons.Remove(selected);
            else
                buttons[selected] = target;
            return p with { Buttons = buttons };
        });
    }

    private void EditSelectedStick(Func<StickSettings, StickSettings> change)
    {
        if (_refreshing)
            return;
        if (_diagram.Selected == PadControl.LeftStick)
            _controller.Edit(p => p with { LeftStick = change(p.LeftStick) });
        else if (_diagram.Selected == PadControl.RightStick)
            _controller.Edit(p => p with { RightStick = change(p.RightStick) });
    }

    private void DeadzoneSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        EditSelectedStick(s => s with { Deadzone = Math.Round(e.NewValue, 2) });

    private void Invert_Click(object sender, RoutedEventArgs e) =>
        EditSelectedStick(s => s with { InvertX = InvertXBox.IsChecked == true, InvertY = InvertYBox.IsChecked == true });

    private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_refreshing)
            _controller.Edit(p => p with { TriggerThreshold = Math.Round(e.NewValue, 2) });
    }

    private void SwapSticks_Click(object sender, RoutedEventArgs e) =>
        _controller.Edit(p => p with { SwapSticks = SwapSticksBox.IsChecked == true });

    private void RemapToggle_Click(object sender, RoutedEventArgs e) => _controller.Enabled = RemapToggle.IsChecked == true;

    private void Retry_Click(object sender, RoutedEventArgs e) => _controller.Start();

    private void StartupToggle_Click(object sender, RoutedEventArgs e) => StartWithWindows.IsEnabled = StartupToggle.IsChecked == true;

    private void ProfileBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshing || ProfileBox.SelectedItem is not string name
            || string.Equals(name, Profile.Name, StringComparison.OrdinalIgnoreCase))
            return;
        if (_controller.HasUnsavedChanges && !ConfirmDiscard())
        {
            Refresh();
            return;
        }
        _controller.SelectProfile(name);
    }

    private void Save_Click(object sender, RoutedEventArgs e) => _controller.Save();

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        var name = TextPrompt.Ask(this, "Save profile as", "Name for the new profile:", $"{Profile.Name} copy");
        if (string.IsNullOrWhiteSpace(name))
            return;
        name = name.Trim();
        if (_controller.Store.Find(name) is not null
            && MessageBox.Show(this, $"A profile named \"{name}\" already exists. Replace it?", "FlipFace",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        _controller.SaveAs(name);
    }

    private void Revert_Click(object sender, RoutedEventArgs e) => _controller.Revert();

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var isPreset = Presets.All.Any(p => string.Equals(p.Name, Profile.Name, StringComparison.OrdinalIgnoreCase));
        var question = isPreset
            ? $"Throw away your changes to \"{Profile.Name}\" and go back to the built-in version?"
            : $"Delete the profile \"{Profile.Name}\"?";
        if (MessageBox.Show(this, question, "FlipFace", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            _controller.DeleteSaved();
    }

    private bool ConfirmDiscard() =>
        MessageBox.Show(this, $"You have unsaved changes to \"{Profile.Name}\". Discard them?", "FlipFace",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    private void AddLog(string message)
    {
        LogList.Items.Add($"{DateTime.Now:HH:mm:ss}  {message}");
        while (LogList.Items.Count > MaxLogLines)
            LogList.Items.RemoveAt(0);
        LogList.ScrollIntoView(LogList.Items[^1]);
    }
}
