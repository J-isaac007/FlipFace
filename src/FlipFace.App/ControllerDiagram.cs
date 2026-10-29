using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using FlipFace.Core;
using FlipFace.Runtime;

namespace FlipFace.App;

/// <summary>
/// A drawing of a Switch-layout pad. Each control shows its printed label and what it sends,
/// lights up while held, and can be clicked. Sticks show two dots: a ring for where the physical
/// stick is, and a filled dot for what games receive after swap, invert and deadzone.
/// </summary>
public sealed class ControllerDiagram : Viewbox
{
    private static readonly Brush BodyBrush = Frozen(Color.FromRgb(0xE3, 0xE6, 0xEB));
    private static readonly Brush IdleBrush = Brushes.White;
    private static readonly Color HeldColor = Color.FromRgb(0xFF, 0xC9, 0x4D);
    private static readonly Brush HeldBrush = Frozen(HeldColor);
    private static readonly Brush OutlineBrush = Frozen(Color.FromRgb(0xB8, 0xBD, 0xC6));
    private static readonly Brush AccentBrush = Frozen(Color.FromRgb(0x2F, 0x6F, 0xEB));
    private static readonly Brush MutedBrush = Frozen(Color.FromRgb(0x8A, 0x90, 0x9A));

    private readonly Dictionary<PadControl, ControlView> _views = [];
    private readonly Canvas _canvas = new() { Width = 600, Height = 340 };
    private readonly Stick _leftStick;
    private readonly Stick _rightStick;
    private PadControl? _selected;

    public ControllerDiagram()
    {
        Child = _canvas;
        Stretch = Stretch.Uniform;

        // Body: two grips under a rounded slab.
        Add(new Ellipse { Width = 170, Height = 150, Fill = BodyBrush }, 55, 185);
        Add(new Ellipse { Width = 170, Height = 150, Fill = BodyBrush }, 375, 185);
        Add(new Rectangle { Width = 520, Height = 220, RadiusX = 80, RadiusY = 80, Fill = BodyBrush }, 40, 74);

        AddRect(PadControl.LeftTrigger, 70, 6, 130, 32);
        AddRect(PadControl.LeftShoulder, 70, 42, 130, 28);
        AddRect(PadControl.RightTrigger, 400, 6, 130, 32);
        AddRect(PadControl.RightShoulder, 400, 42, 130, 28);

        AddRound(PadControl.North, 455, 112, 44);
        AddRound(PadControl.East, 503, 160, 44);
        AddRound(PadControl.South, 455, 208, 44);
        AddRound(PadControl.West, 407, 160, 44);

        AddRound(PadControl.Back, 245, 118, 42);
        AddRound(PadControl.Start, 355, 118, 42);
        AddRound(PadControl.Guide, 330, 175, 42);

        const double dpadX = 225, dpadY = 250, arm = 36;
        AddRect(PadControl.DPadUp, dpadX - arm / 2, dpadY - arm * 1.5, arm, arm);
        AddRect(PadControl.DPadDown, dpadX - arm / 2, dpadY + arm / 2, arm, arm);
        AddRect(PadControl.DPadLeft, dpadX - arm * 1.5, dpadY - arm / 2, arm, arm);
        AddRect(PadControl.DPadRight, dpadX + arm / 2, dpadY - arm / 2, arm, arm);

        _leftStick = AddStick(PadControl.LeftStick, 145, 160);
        _rightStick = AddStick(PadControl.RightStick, 385, 250);
    }

    /// <summary>Raised when a control is clicked.</summary>
    public event Action<PadControl>? ControlClicked;

    public PadControl? Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            foreach (var (control, view) in _views)
            {
                var selected = control == value;
                view.Border.BorderBrush = selected ? AccentBrush : OutlineBrush;
                view.Border.BorderThickness = new Thickness(selected ? 3 : 1);
            }
        }
    }

    /// <summary>Updates the "sends" line on every control.</summary>
    public void ShowProfile(Profile profile)
    {
        foreach (var (control, view) in _views)
        {
            var target = profile.Buttons.TryGetValue(control, out var mapped) ? mapped : control;
            view.Output.Text = "→ " + Labels.XboxShort(target);
            view.Output.Foreground = target == control ? MutedBrush : AccentBrush;
            view.Output.FontWeight = target == control ? FontWeights.Normal : FontWeights.SemiBold;
        }
    }

    /// <summary>Lights up held controls and moves the stick dots.</summary>
    public void ShowState(SessionSnapshot snapshot, Profile profile)
    {
        var physical = snapshot.Physical;
        foreach (var (control, view) in _views)
        {
            if (control.IsTrigger())
            {
                var pull = physical[control.AsTriggerAxis()] / (double)short.MaxValue;
                view.Border.Background = pull <= 0 ? IdleBrush : Blend(pull);
            }
            else
            {
                view.Border.Background = physical.IsDown(control.AsButton()) ? HeldBrush : IdleBrush;
            }
        }

        // A physical stick's output lands on the other virtual stick when they're swapped.
        var output = snapshot.Output;
        var (leftOut, rightOut) = profile.SwapSticks
            ? ((output.RightX, output.RightY), (output.LeftX, output.LeftY))
            : ((output.LeftX, output.LeftY), (output.RightX, output.RightY));
        _leftStick.Show((physical.LeftX, physical.LeftY), leftOut);
        _rightStick.Show((physical.RightX, physical.RightY), rightOut);
    }

    private void AddRect(PadControl control, double x, double y, double width, double height) =>
        AddView(control, x, y, width, height, cornerRadius: 9);

    private void AddRound(PadControl control, double centerX, double centerY, double diameter) =>
        AddView(control, centerX - diameter / 2, centerY - diameter / 2, diameter, diameter, cornerRadius: diameter / 2);

    private ControlView AddView(PadControl control, double x, double y, double width, double height, double cornerRadius)
    {
        var small = height < 40;
        var printed = new TextBlock
        {
            Text = Labels.Printed(control),
            FontSize = small ? 12 : 15,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var output = new TextBlock { FontSize = small ? 8.5 : 9.5, HorizontalAlignment = HorizontalAlignment.Center };
        var horizontal = width > height * 2;
        var border = new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(cornerRadius),
            Background = IdleBrush,
            BorderBrush = OutlineBrush,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            ToolTip = $"{Labels.Printed(control)} ({Labels.Where(control)})",
            Child = new StackPanel
            {
                Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { printed, output },
            },
        };
        if (horizontal)
            output.Margin = new Thickness(6, 3, 0, 0);
        border.MouseLeftButtonDown += (_, e) =>
        {
            ControlClicked?.Invoke(control);
            e.Handled = true;
        };
        Add(border, x, y);
        var view = new ControlView(border, output);
        _views[control] = view;
        return view;
    }

    private Stick AddStick(PadControl control, double centerX, double centerY)
    {
        const double diameter = 84;
        var view = AddView(control, centerX - diameter / 2, centerY - diameter / 2, diameter, diameter, diameter / 2);
        // Keep the label in the top of the circle so the position dots, which rest at the center, don't cover it.
        var label = (StackPanel)view.Border.Child;
        label.VerticalAlignment = VerticalAlignment.Top;
        label.Margin = new Thickness(0, 6, 0, 0);
        label.Opacity = 0.75;
        ((TextBlock)label.Children[0]).FontSize = 12;
        var ring = new Ellipse { Width = 14, Height = 14, Stroke = MutedBrush, StrokeThickness = 2, IsHitTestVisible = false };
        var dot = new Ellipse { Width = 10, Height = 10, Fill = AccentBrush, IsHitTestVisible = false };
        _canvas.Children.Add(ring);
        _canvas.Children.Add(dot);
        var stick = new Stick(centerX, centerY, (diameter / 2) - 10, ring, dot);
        stick.Show((0, 0), (0, 0));
        return stick;
    }

    private void Add(UIElement element, double x, double y)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        _canvas.Children.Add(element);
    }

    private static Brush Blend(double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        byte Mix(byte to) => (byte)(255 + (to - 255) * amount);
        return Frozen(Color.FromRgb(Mix(HeldColor.R), Mix(HeldColor.G), Mix(HeldColor.B)));
    }

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private sealed record ControlView(Border Border, TextBlock Output);

    private sealed record Stick(double CenterX, double CenterY, double Travel, Ellipse Ring, Ellipse Dot)
    {
        public void Show((short X, short Y) physical, (short X, short Y) output)
        {
            Place(Ring, physical);
            Place(Dot, output);
        }

        private void Place(Ellipse marker, (short X, short Y) value)
        {
            // Axis values are SDL's: Y grows downwards, like screen coordinates.
            Canvas.SetLeft(marker, CenterX + value.X / (double)short.MaxValue * Travel - marker.Width / 2);
            Canvas.SetTop(marker, CenterY + value.Y / (double)short.MaxValue * Travel - marker.Height / 2);
        }
    }
}
