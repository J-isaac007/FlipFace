namespace FlipFace.Core;

/// <summary>
/// A saved layout. Only the differences from the physical pad are listed: controls missing from
/// <see cref="Buttons"/> press themselves, and a <c>null</c> target turns that control off.
/// </summary>
public sealed record Profile
{
    public required string Name { get; init; }

    /// <summary>Physical control → what it presses on the virtual pad (<c>null</c> = nothing).</summary>
    public Dictionary<PadControl, PadControl?> Buttons { get; init; } = [];

    public StickSettings LeftStick { get; init; } = new();
    public StickSettings RightStick { get; init; } = new();

    /// <summary>When true, the physical left stick drives the virtual right stick and the reverse.</summary>
    public bool SwapSticks { get; init; }

    /// <summary>How far (0..1) a trigger must be pulled to count as pressed when it's mapped to a button.</summary>
    public double TriggerThreshold { get; init; } = 0.5;
}

/// <summary>Settings for one virtual stick, applied after <see cref="Profile.SwapSticks"/>.</summary>
public sealed record StickSettings
{
    /// <summary>Fraction of the stick's travel (0..0.95) around the center that reads as centered.</summary>
    public double Deadzone { get; init; }

    public bool InvertX { get; init; }

    /// <summary>Flips up and down.</summary>
    public bool InvertY { get; init; }
}
