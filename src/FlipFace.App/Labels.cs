using FlipFace.Core;

namespace FlipFace.App;

/// <summary>Names shown in the UI for each control.</summary>
public static class Labels
{
    /// <summary>What's printed on a Switch-layout pad such as the Onikuma C1, at each position.</summary>
    public static string Printed(PadControl control) => control switch
    {
        PadControl.South => "B",
        PadControl.East => "A",
        PadControl.West => "Y",
        PadControl.North => "X",
        PadControl.Back => "−",
        PadControl.Start => "+",
        PadControl.Guide => "Home",
        PadControl.LeftStick => "L stick",
        PadControl.RightStick => "R stick",
        PadControl.LeftShoulder => "L",
        PadControl.RightShoulder => "R",
        PadControl.LeftTrigger => "ZL",
        PadControl.RightTrigger => "ZR",
        PadControl.DPadUp => "↑",
        PadControl.DPadDown => "↓",
        PadControl.DPadLeft => "←",
        PadControl.DPadRight => "→",
        _ => control.ToString(),
    };

    /// <summary>The Xbox name games use for a control, or "Nothing" when a mapping turns it off.</summary>
    public static string Xbox(PadControl? control) => control switch
    {
        null => "Nothing",
        PadControl.South => "A",
        PadControl.East => "B",
        PadControl.West => "X",
        PadControl.North => "Y",
        PadControl.Back => "View",
        PadControl.Start => "Menu",
        PadControl.Guide => "Guide",
        PadControl.LeftStick => "LS",
        PadControl.RightStick => "RS",
        PadControl.LeftShoulder => "LB",
        PadControl.RightShoulder => "RB",
        PadControl.LeftTrigger => "LT",
        PadControl.RightTrigger => "RT",
        PadControl.DPadUp => "D-pad ↑",
        PadControl.DPadDown => "D-pad ↓",
        PadControl.DPadLeft => "D-pad ←",
        PadControl.DPadRight => "D-pad →",
        _ => control.ToString()!,
    };

    /// <summary>Like <see cref="Xbox"/>, but short enough for the small buttons on the controller picture.</summary>
    public static string XboxShort(PadControl? control) => control switch
    {
        null => "off",
        PadControl.DPadUp => "↑",
        PadControl.DPadDown => "↓",
        PadControl.DPadLeft => "←",
        PadControl.DPadRight => "→",
        _ => Xbox(control),
    };

    /// <summary>Where the control sits, for the editor heading.</summary>
    public static string Where(PadControl control) => control switch
    {
        PadControl.South => "bottom face button",
        PadControl.East => "right face button",
        PadControl.West => "left face button",
        PadControl.North => "top face button",
        PadControl.LeftStick or PadControl.RightStick => "press the stick in",
        PadControl.LeftTrigger or PadControl.RightTrigger => "trigger",
        PadControl.LeftShoulder or PadControl.RightShoulder => "shoulder button",
        PadControl.DPadUp or PadControl.DPadDown or PadControl.DPadLeft or PadControl.DPadRight => "D-pad",
        _ => "center button",
    };
}
