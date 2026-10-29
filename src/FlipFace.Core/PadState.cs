namespace FlipFace.Core;

/// <summary>A snapshot of every button and axis on a pad. Axis values use SDL's ranges and directions.</summary>
public record struct PadState
{
    /// <summary>Bit <c>n</c> set means <see cref="PadButton"/> <c>n</c> is held.</summary>
    public uint Buttons;

    /// <summary>Sticks: -32768..32767, with Y positive pointing down. Triggers: 0..32767.</summary>
    public short LeftX, LeftY, RightX, RightY, LeftTrigger, RightTrigger;

    public readonly bool IsDown(PadButton button) => (Buttons & Bit(button)) != 0;

    public void SetButton(PadButton button, bool down)
    {
        if (down)
            Buttons |= Bit(button);
        else
            Buttons &= ~Bit(button);
    }

    public short this[PadAxis axis]
    {
        readonly get => axis switch
        {
            PadAxis.LeftX => LeftX,
            PadAxis.LeftY => LeftY,
            PadAxis.RightX => RightX,
            PadAxis.RightY => RightY,
            PadAxis.LeftTrigger => LeftTrigger,
            PadAxis.RightTrigger => RightTrigger,
            _ => throw new ArgumentOutOfRangeException(nameof(axis)),
        };
        set
        {
            switch (axis)
            {
                case PadAxis.LeftX: LeftX = value; break;
                case PadAxis.LeftY: LeftY = value; break;
                case PadAxis.RightX: RightX = value; break;
                case PadAxis.RightY: RightY = value; break;
                case PadAxis.LeftTrigger: LeftTrigger = value; break;
                case PadAxis.RightTrigger: RightTrigger = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(axis));
            }
        }
    }

    private static uint Bit(PadButton button) => 1u << (int)button;
}
