namespace FlipFace.Core;

/// <summary>
/// A controller button identified by where it sits on the pad, not by what is printed on it.
/// On a Switch-layout pad the button printed "A" sits at <see cref="East"/>; on an Xbox pad "A" sits at <see cref="South"/>.
/// </summary>
public enum PadButton
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
}

public enum PadAxis
{
    LeftX,
    LeftY,
    RightX,
    RightY,
    LeftTrigger,
    RightTrigger,
}

public static class PadButtonNames
{
    /// <summary>The Xbox label games show for a face button position, e.g. South → "A".</summary>
    public static string XboxLabel(this PadButton button) => button switch
    {
        PadButton.South => "A",
        PadButton.East => "B",
        PadButton.West => "X",
        PadButton.North => "Y",
        PadButton.Back => "View",
        PadButton.Guide => "Guide",
        PadButton.Start => "Menu",
        PadButton.LeftStick => "LS",
        PadButton.RightStick => "RS",
        PadButton.LeftShoulder => "LB",
        PadButton.RightShoulder => "RB",
        _ => button.ToString(),
    };
}
