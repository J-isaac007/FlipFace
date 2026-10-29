namespace FlipFace.Core;

/// <summary>A <see cref="Profile"/> prepared for the remap loop, which calls <see cref="Apply"/> about once per millisecond.</summary>
public sealed class Mapping
{
    private readonly PadControl?[] _targets;
    private readonly short _triggerThreshold;
    private readonly bool _swapSticks;
    private readonly StickSettings _leftStick;
    private readonly StickSettings _rightStick;

    public Mapping(Profile profile)
    {
        Profile = profile;
        _targets = PadControls.All.Select(c => (PadControl?)c).ToArray();
        foreach (var (source, target) in profile.Buttons)
            _targets[(int)source] = target;
        _triggerThreshold = (short)Math.Round(Math.Clamp(profile.TriggerThreshold, 0.01, 1.0) * short.MaxValue);
        _swapSticks = profile.SwapSticks;
        _leftStick = profile.LeftStick;
        _rightStick = profile.RightStick;
    }

    public Profile Profile { get; }

    /// <summary>Builds the state the virtual pad should report for the physical <paramref name="input"/>.</summary>
    public PadState Apply(in PadState input)
    {
        // Several sources may share a target: a button is held while any of them is, a trigger takes the deepest pull.
        var output = new PadState();
        foreach (var source in PadControls.All)
        {
            if (_targets[(int)source] is not { } target)
                continue;

            if (source.IsTrigger())
            {
                var pull = input[source.AsTriggerAxis()];
                if (target.IsTrigger())
                    Raise(ref output, target.AsTriggerAxis(), pull);
                else if (pull >= _triggerThreshold)
                    output.SetButton(target.AsButton(), true);
            }
            else if (input.IsDown(source.AsButton()))
            {
                if (target.IsTrigger())
                    Raise(ref output, target.AsTriggerAxis(), short.MaxValue);
                else
                    output.SetButton(target.AsButton(), true);
            }
        }

        var (left, right) = _swapSticks
            ? ((input.RightX, input.RightY), (input.LeftX, input.LeftY))
            : ((input.LeftX, input.LeftY), (input.RightX, input.RightY));
        (output.LeftX, output.LeftY) = Shape(left, _leftStick);
        (output.RightX, output.RightY) = Shape(right, _rightStick);
        return output;
    }

    private static void Raise(ref PadState state, PadAxis trigger, short pull)
    {
        if (pull > state[trigger])
            state[trigger] = pull;
    }

    private static (short X, short Y) Shape((short X, short Y) stick, StickSettings settings)
    {
        var x = settings.InvertX ? Negate(stick.X) : stick.X;
        var y = settings.InvertY ? Negate(stick.Y) : stick.Y;

        var deadzone = Math.Clamp(settings.Deadzone, 0, 0.95);
        if (deadzone == 0)
            return (x, y);

        // Radial deadzone: anything inside the circle reads as centered, and the rest of the travel is
        // stretched back to the full range so small pushes past the edge don't jump.
        double nx = x / (double)short.MaxValue, ny = y / (double)short.MaxValue;
        var magnitude = Math.Sqrt(nx * nx + ny * ny);
        if (magnitude <= deadzone)
            return (0, 0);
        var scale = Math.Min((magnitude - deadzone) / (1 - deadzone), 1) / magnitude;
        return (ToAxis(nx * scale), ToAxis(ny * scale));
    }

    private static short Negate(short value) => value == short.MinValue ? short.MaxValue : (short)-value;

    private static short ToAxis(double value) => (short)Math.Round(Math.Clamp(value, -1, 1) * short.MaxValue);
}
