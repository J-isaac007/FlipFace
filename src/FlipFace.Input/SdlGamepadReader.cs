using FlipFace.Core;
using SDL;
using static SDL.SDL3;

namespace FlipFace.Input;

/// <summary>
/// Reads physical controllers through SDL3. Must be created, polled and disposed on the same thread.
/// SDL reports buttons by position (South/East/West/North), so printed labels never affect what we read.
/// </summary>
public sealed unsafe class SdlGamepadReader : IDisposable
{
    private readonly Dictionary<SDL_JoystickID, nint> _open = new();
    private bool _disposed;

    public SdlGamepadReader()
    {
        // FlipFace runs in the background, so it must keep receiving input while a game has focus.
        SDL_SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS", "1");
        if (!SDL_Init(SDL_InitFlags.SDL_INIT_GAMEPAD))
            throw new InvalidOperationException($"SDL_Init failed: {SDL_GetError()}");
    }

    /// <summary>
    /// Drains pending SDL events into <paramref name="events"/>. Pads already connected at startup
    /// arrive as <see cref="GamepadConnected"/> on the first poll.
    /// </summary>
    public void Poll(List<GamepadEvent> events)
    {
        SDL_Event e;
        while (SDL_PollEvent(&e))
        {
            switch ((SDL_EventType)e.type)
            {
                case SDL_EventType.SDL_EVENT_GAMEPAD_ADDED:
                    var pad = SDL_OpenGamepad(e.gdevice.which);
                    if (pad == null)
                        break;
                    _open[e.gdevice.which] = (nint)pad;
                    events.Add(new GamepadConnected(Describe(pad)));
                    break;

                case SDL_EventType.SDL_EVENT_GAMEPAD_REMOVED:
                    if (_open.Remove(e.gdevice.which, out var removed))
                        SDL_CloseGamepad((SDL_Gamepad*)removed);
                    events.Add(new GamepadDisconnected((uint)e.gdevice.which));
                    break;

                case SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_DOWN:
                case SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_UP:
                    if (ToPadButton(e.gbutton.button) is { } button)
                        events.Add(new ButtonChanged((uint)e.gbutton.which, button, e.gbutton.down));
                    break;

                case SDL_EventType.SDL_EVENT_GAMEPAD_AXIS_MOTION:
                    if (e.gaxis.axis <= (byte)PadAxis.RightTrigger)
                        events.Add(new AxisChanged((uint)e.gaxis.which, (PadAxis)e.gaxis.axis, e.gaxis.value));
                    break;
            }
        }
    }

    private static GamepadInfo Describe(SDL_Gamepad* pad) => new(
        (uint)SDL_GetGamepadID(pad),
        SDL_GetGamepadName(pad) ?? "(unnamed)",
        SDL_GetGamepadType(pad).ToString().Replace("SDL_GAMEPAD_TYPE_", ""),
        SDL_GetGamepadVendor(pad),
        SDL_GetGamepadProduct(pad),
        SDL_GetGamepadPath(pad));

    // PadButton's members mirror the first entries of SDL_GamepadButton; anything beyond (paddles, misc) is ignored for now.
    private static PadButton? ToPadButton(byte sdlButton) =>
        sdlButton <= (byte)PadButton.DPadRight ? (PadButton)sdlButton : null;

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (var pad in _open.Values)
            SDL_CloseGamepad((SDL_Gamepad*)pad);
        _open.Clear();
        SDL_Quit();
    }
}
