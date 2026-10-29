using FlipFace.Core;

namespace FlipFace.Input;

/// <param name="Path">OS device path; used to tell a physical pad apart from a virtual one with the same VID/PID.</param>
public sealed record GamepadInfo(uint Id, string Name, string Type, ushort VendorId, ushort ProductId, string? Path);

public abstract record GamepadEvent(uint Id);

public sealed record GamepadConnected(GamepadInfo Info) : GamepadEvent(Info.Id);

public sealed record GamepadDisconnected(uint Id) : GamepadEvent(Id);

public sealed record ButtonChanged(uint Id, PadButton Button, bool Down) : GamepadEvent(Id);

/// <param name="Value">Sticks: -32768..32767. Triggers: 0..32767.</param>
public sealed record AxisChanged(uint Id, PadAxis Axis, short Value) : GamepadEvent(Id);
