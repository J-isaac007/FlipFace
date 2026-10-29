using FlipFace.Core;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace FlipFace.Output;

/// <summary>A controller games can see. Disposing it unplugs it.</summary>
public interface IVirtualPad : IDisposable
{
    void Send(in PadState state);
}

/// <summary>Owns the connection to the ViGEmBus driver and creates virtual pads on it.</summary>
public sealed class ViGEmOutput : IDisposable
{
    private readonly ViGEmClient _client;

    private ViGEmOutput(ViGEmClient client) => _client = client;

    /// <summary>Returns null when the ViGEmBus driver isn't installed.</summary>
    public static ViGEmOutput? TryConnect()
    {
        try
        {
            return new ViGEmOutput(new ViGEmClient());
        }
        catch (VigemBusNotFoundException)
        {
            return null;
        }
    }

    public IVirtualPad CreateXbox360() => new ViGEmXbox360Pad(_client.CreateXbox360Controller());

    public void Dispose() => _client.Dispose();
}

internal sealed class ViGEmXbox360Pad : IVirtualPad
{
    private static readonly (PadButton Button, Xbox360Button Xbox)[] Buttons =
    [
        (PadButton.South, Xbox360Button.A),
        (PadButton.East, Xbox360Button.B),
        (PadButton.West, Xbox360Button.X),
        (PadButton.North, Xbox360Button.Y),
        (PadButton.Back, Xbox360Button.Back),
        (PadButton.Guide, Xbox360Button.Guide),
        (PadButton.Start, Xbox360Button.Start),
        (PadButton.LeftStick, Xbox360Button.LeftThumb),
        (PadButton.RightStick, Xbox360Button.RightThumb),
        (PadButton.LeftShoulder, Xbox360Button.LeftShoulder),
        (PadButton.RightShoulder, Xbox360Button.RightShoulder),
        (PadButton.DPadUp, Xbox360Button.Up),
        (PadButton.DPadDown, Xbox360Button.Down),
        (PadButton.DPadLeft, Xbox360Button.Left),
        (PadButton.DPadRight, Xbox360Button.Right),
    ];

    private readonly IXbox360Controller _pad;

    public ViGEmXbox360Pad(IXbox360Controller pad)
    {
        _pad = pad;
        // Send one report per state change instead of one per field.
        _pad.AutoSubmitReport = false;
        _pad.Connect();
    }

    public void Send(in PadState state)
    {
        foreach (var (button, xbox) in Buttons)
            _pad.SetButtonState(xbox, state.IsDown(button));

        _pad.SetAxisValue(Xbox360Axis.LeftThumbX, state.LeftX);
        _pad.SetAxisValue(Xbox360Axis.LeftThumbY, FlipY(state.LeftY));
        _pad.SetAxisValue(Xbox360Axis.RightThumbX, state.RightX);
        _pad.SetAxisValue(Xbox360Axis.RightThumbY, FlipY(state.RightY));
        _pad.SetSliderValue(Xbox360Slider.LeftTrigger, ToTrigger(state.LeftTrigger));
        _pad.SetSliderValue(Xbox360Slider.RightTrigger, ToTrigger(state.RightTrigger));
        _pad.SubmitReport();
    }

    // SDL's Y points down, XInput's points up. Bitwise NOT maps -32768..32767 onto 32767..-32768 without overflow.
    private static short FlipY(short y) => (short)~y;

    private static byte ToTrigger(short value) => (byte)(Math.Max((int)value, 0) * 255 / short.MaxValue);

    public void Dispose() => _pad.Disconnect();
}
