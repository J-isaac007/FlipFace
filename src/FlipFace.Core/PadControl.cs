namespace FlipFace.Core;

/// <summary>
/// Anything a profile can map from or to: every <see cref="PadButton"/> (same names, same order) plus the two triggers.
/// This is the vocabulary of profile files, e.g. <c>"LeftTrigger": "RightShoulder"</c>.
/// </summary>
public enum PadControl
{
    South,
    East,
    West,
    North,
    Back,
    Guide,
    Start,
    LeftStick,
    RightStick,
    LeftShoulder,
    RightShoulder,
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight,
    LeftTrigger,
    RightTrigger,
}

public static class PadControls
{
    public static IReadOnlyList<PadControl> All { get; } = Enum.GetValues<PadControl>();

    public static bool IsTrigger(this PadControl control) =>
        control is PadControl.LeftTrigger or PadControl.RightTrigger;

    /// <summary>The button this control is. Only valid when <see cref="IsTrigger"/> is false.</summary>
    public static PadButton AsButton(this PadControl control) => (PadButton)control;

    /// <summary>The trigger axis this control is. Only valid when <see cref="IsTrigger"/> is true.</summary>
    public static PadAxis AsTriggerAxis(this PadControl control) =>
        control == PadControl.LeftTrigger ? PadAxis.LeftTrigger : PadAxis.RightTrigger;
}
