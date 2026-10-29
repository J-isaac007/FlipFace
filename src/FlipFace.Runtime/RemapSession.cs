using System.Runtime.InteropServices;
using FlipFace.Core;
using FlipFace.Input;
using FlipFace.Output;
using FlipFace.Setup;

namespace FlipFace.Runtime;

/// <summary>A driver FlipFace needs is missing. The message says which one and what to do.</summary>
public sealed class SetupMissingException(string message) : Exception(message);

/// <summary>
/// The remap loop: reads the physical pad, hides it from games, and replays it through a virtual
/// Xbox 360 pad with <paramref name="map"/> applied. Handles one physical pad at a time.
/// </summary>
public sealed class RemapSession(ButtonMap map, Action<string> log)
{
    public void Run(CancellationToken cancel)
    {
        using var output = ViGEmOutput.TryConnect()
            ?? throw new SetupMissingException("ViGEmBus isn't installed. Install it from https://github.com/nefarius/ViGEmBus/releases and try again.");
        var hidHide = HidHide.TryFind()
            ?? throw new SetupMissingException("HidHide isn't installed. Install it from https://github.com/nefarius/HidHide/releases, reboot, and try again.");
        var ledger = new HiddenDeviceLedger(hidHide);

        foreach (var path in ledger.RestoreAll())
            log($"Unhid {path}, left hidden by a previous run that didn't exit cleanly.");
        hidHide.RegisterApp(Environment.ProcessPath!);
        hidHide.CloakOn();

        using var reader = new SdlGamepadReader();
        // Windows sleeps in ~15 ms steps by default; 1 ms keeps input latency at a millisecond or two.
        timeBeginPeriod(1);
        var loop = new Loop(map, log, output, hidHide, ledger);
        try
        {
            var events = new List<GamepadEvent>();
            while (!cancel.IsCancellationRequested)
            {
                events.Clear();
                reader.Poll(events);
                foreach (var ev in events)
                    loop.Handle(ev);
                loop.Flush();
                Thread.Sleep(1);
            }
        }
        finally
        {
            loop.Stop();
            timeEndPeriod(1);
        }
    }

    private sealed class Loop(ButtonMap map, Action<string> log, ViGEmOutput output, HidHide hidHide, HiddenDeviceLedger ledger)
    {
        private uint? _sourceId;
        private IVirtualPad? _virtualPad;
        private PadState _input;
        private PadState _lastSent;

        public void Handle(GamepadEvent ev)
        {
            switch (ev)
            {
                case GamepadConnected c:
                    OnConnected(c.Info);
                    break;
                case GamepadDisconnected d when d.Id == _sourceId:
                    log("Controller unplugged. Virtual pad removed; waiting for it to come back.");
                    Stop();
                    break;
                case ButtonChanged b when b.Id == _sourceId:
                    _input.SetButton(b.Button, b.Down);
                    break;
                case AxisChanged a when a.Id == _sourceId:
                    _input[a.Axis] = a.Value;
                    break;
            }
        }

        /// <summary>Sends the remapped state to the virtual pad if anything changed since the last send.</summary>
        public void Flush()
        {
            if (_virtualPad is null)
                return;
            var mapped = RemapEngine.Apply(_input, map);
            if (mapped == _lastSent)
                return;
            _virtualPad.Send(mapped);
            _lastSent = mapped;
        }

        /// <summary>Unplugs the virtual pad and makes the physical one visible to games again.</summary>
        public void Stop()
        {
            _virtualPad?.Dispose();
            _virtualPad = null;
            _sourceId = null;
            ledger.RestoreAll();
        }

        private void OnConnected(GamepadInfo info)
        {
            var instancePath = DeviceTree.InstancePathFromInterfacePath(info.Path);
            if (instancePath is null)
            {
                log($"Skipping \"{info.Name}\": SDL gave no device path ({info.Path ?? "none"}), so it can't be hidden from games.");
                return;
            }
            if (DeviceTree.IsOnViGEmBus(instancePath))
            {
                log($"Ignoring virtual pad {instancePath}.");
                return;
            }
            if (_sourceId is not null)
            {
                log($"Already remapping a controller; ignoring \"{info.Name}\" ({instancePath}).");
                return;
            }

            // Hide the HID device and its root device: games read Xbox pads through either one (raw input/DirectInput vs XInput).
            var device = hidHide.GamingDevices()
                .FirstOrDefault(d => string.Equals(d.DeviceInstancePath, instancePath, StringComparison.OrdinalIgnoreCase));
            string[] toHide = device is null
                ? [instancePath]
                : [device.DeviceInstancePath, device.BaseContainerDeviceInstancePath];
            ledger.Hide(toHide.Distinct(StringComparer.OrdinalIgnoreCase));

            _sourceId = info.Id;
            _input = default;
            _lastSent = default;
            _virtualPad = output.CreateXbox360();
            log($"Remapping \"{info.Name}\" ({instancePath}) to a virtual Xbox 360 controller.");
            log($"Hid from games: {string.Join(", ", toHide)}");
        }
    }

    [DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint milliseconds);

    [DllImport("winmm.dll")]
    private static extern uint timeEndPeriod(uint milliseconds);
}
