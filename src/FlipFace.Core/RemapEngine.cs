namespace FlipFace.Core;

/// <summary>Says which output button each physical button presses. Buttons without an entry press themselves.</summary>
public sealed class ButtonMap
{
    private readonly PadButton[] _targets;

    public ButtonMap(IReadOnlyDictionary<PadButton, PadButton> overrides)
    {
        _targets = Enum.GetValues<PadButton>();
        foreach (var (source, target) in overrides)
            _targets[(int)source] = target;
    }

    public PadButton this[PadButton source] => _targets[(int)source];

    public static ButtonMap Identity { get; } = new(new Dictionary<PadButton, PadButton>());
}

public static class RemapEngine
{
    private static readonly PadButton[] AllButtons = Enum.GetValues<PadButton>();

    /// <summary>Builds the state the virtual pad should report for the physical <paramref name="input"/>.</summary>
    public static PadState Apply(in PadState input, ButtonMap map)
    {
        // Several physical buttons may point at the same target; it is held while any of them is.
        var output = input with { Buttons = 0 };
        foreach (var button in AllButtons)
        {
            if (input.IsDown(button))
                output.SetButton(map[button], true);
        }
        return output;
    }
}

public static class Presets
{
    /// <summary>
    /// For Switch-layout pads such as the Onikuma C1: each printed face label acts as the Xbox button
    /// with the same name, so printed A (east) presses Xbox A (south), and so on.
    /// </summary>
    public static ButtonMap MatchLabels { get; } = new(new Dictionary<PadButton, PadButton>
    {
        [PadButton.East] = PadButton.South,  // printed A
        [PadButton.South] = PadButton.East,  // printed B
        [PadButton.North] = PadButton.West,  // printed X
        [PadButton.West] = PadButton.North,  // printed Y
    });
}
