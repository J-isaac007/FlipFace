namespace FlipFace.Core;

/// <summary>Profiles that ship with FlipFace. Saved profiles with the same name replace them.</summary>
public static class Presets
{
    /// <summary>
    /// For Switch-layout pads such as the Onikuma C1: each printed face label acts as the Xbox button
    /// with the same name, so printed A (east) presses Xbox A (south), and so on.
    /// </summary>
    public static Profile MatchLabels { get; } = new()
    {
        Name = "Match labels",
        Buttons =
        {
            [PadControl.East] = PadControl.South,  // printed A
            [PadControl.South] = PadControl.East,  // printed B
            [PadControl.North] = PadControl.West,  // printed X
            [PadControl.West] = PadControl.North,  // printed Y
        },
    };

    /// <summary>
    /// Passes everything through unchanged: buttons keep their positions, so printed labels follow the
    /// Xbox layout by position (printed B, at the bottom, acts as Xbox A).
    /// </summary>
    public static Profile Default { get; } = new() { Name = "Default" };

    public static IReadOnlyList<Profile> All { get; } = [MatchLabels, Default];
}
