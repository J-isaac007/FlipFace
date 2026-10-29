using System.Runtime.InteropServices;
using FlipFace.Core;
using FlipFace.Input;
using FlipFace.Output;
using FlipFace.Setup;

namespace FlipFace.Runtime;

/// <summary>FlipFace can't start: a driver is missing or another copy is running. The message says what to do.</summary>
public sealed class CannotStartException(string message) : Exception(message);

/// <param name="Controller">The physical pad being read, or null when none is connected.</param>
/// <param name="Remapping">True while the physical pad is hidden and games see the virtual one.</param>
/// <param name="Physical">What the physical pad is doing.</param>
/// <param name="Output">What the virtual pad reports (or would report, while remapping is off).</param>
public sealed record SessionSnapshot(GamepadInfo? Controller, bool Remapping, PadState Physical, PadState Output)
{
    public static SessionSnapshot Idle { get; } = new(null, false, default, default);
}

/// <summary>
/// The remap loop: reads the physical pad, hides it from games, and replays it through a virtual
/// Xbox 360 pad with <see cref="Mapping"/> applied. Handles one physical pad at a time.
/// <see cref="Mapping"/> and <see cref="Enabled"/> may be changed from any thread while <see cref="Run"/> is going.
/// </summary>
public sealed class RemapSession(Mapping mapping, Action<string> log)
{
    private readonly Action<string> _log = log;
    private Mapping _mapping = mapping;
    private volatile bool _enabled = true;
    private SessionSnapshot _snapshot = SessionSnapshot.Idle;

    public Mapping Mapping
    {
        get => Volatile.Read(ref _mapping);
        set => Volatile.Write(ref _mapping, value);
    }

    /// <summary>When false the physical pad stays visible to games and no virtual pad exists, but it is still read for the live view.</summary>
    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>The latest state, replaced whenever anything changes. Cheap to read from a UI timer.</summary>
    public SessionSnapshot Snapshot => Volatile.Read(ref _snapshot);

    /// <summary>
    /// Raised (on the session thread) when <see cref="Run"/> finds devices an earlier run left hidden
    /// and makes them visible again.
    /// </summary>
    public event Action<IReadOnlyList<string>>? LeftoversRestored;

    /// <summary>
    /// Last-chance cleanup for a process that is about to die: unhides whatever FlipFace hid.
    /// Never throws. Prefer cancelling <see cref="Run"/>, which also unplugs the virtual pad.
    /// </summary>
    public static void TryEmergencyRestore()
    {
        try
        {
            if (HidHide.TryFind() is { } hidHide)
                new HiddenDeviceLedger(hidHide).RestoreAll();
        }
        catch
        {
            // Nothing more we can do; the next start repairs it.
        }
    }

    /// <summary>Runs until <paramref name="cancel"/> fires, then unhides the pad. Blocks the calling thread.</summary>
    /// <exception cref="CannotStartException"/>
    public void Run(CancellationToken cancel)
    {
        // Two copies would fight over the same pad and each create a virtual one.
        using var instance = new Mutex(false, @"Local\FlipFace.RemapSession", out var firstInstance);
        if (!firstInstance)
            throw new CannotStartException("FlipFace is already running. Close the other copy (check the tray) and try again.");

        var hidHide = HidHide.TryFind()
            ?? throw new CannotStartException($"HidHide isn't installed. Install it from {SetupCheck.HidHideDownload}, restart your PC, and try again.");
        var ledger = new HiddenDeviceLedger(hidHide);

        // Undo a previous run that died with the pad hidden before anything else can fail.
        var leftovers = ledger.RestoreAll();
        if (leftovers.Count > 0)
        {
            foreach (var path in leftovers)
                _log($"Unhid {path}, left hidden by a previous run that didn't exit cleanly.");
            LeftoversRestored?.Invoke(leftovers);
        }

        using var output = ViGEmOutput.TryConnect()
            ?? throw new CannotStartException($"ViGEmBus isn't installed. Install it from {SetupCheck.ViGEmBusDownload} and try again.");
        hidHide.RemoveMissingApps();
        hidHide.RegisterApp(Environment.ProcessPath!);
        hidHide.CloakOn();

        using var reader = new SdlGamepadReader();
        // Windows sleeps in ~15 ms steps by default; 1 ms keeps input latency at a millisecond or two.
        timeBeginPeriod(1);
        var loop = new Loop(this, output, hidHide, ledger);
        try
        {
            var events = new List<GamepadEvent>();
            while (!cancel.IsCancellationRequested)
            {
                events.Clear();
                reader.Poll(events);
                foreach (var ev in events)
                    loop.Handle(ev);
                loop.Tick();
                Thread.Sleep(1);
            }
        }
        finally
        {
            loop.Disengage();
            Volatile.Write(ref _snapshot, SessionSnapshot.Idle);
            timeEndPeriod(1);
        }
    }

    private sealed class Loop(RemapSession session, ViGEmOutput output, HidHide hidHide, HiddenDeviceLedger ledger)
    {
        private GamepadInfo? _source;
        private string[] _hidePaths = [];
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
                case GamepadDisconnected d when d.Id == _source?.Id:
                    session._log("Controller unplugged. Waiting for it to come back.");
                    Disengage();
                    _source = null;
                    break;
                case ButtonChanged b when b.Id == _source?.Id:
                    _input.SetButton(b.Button, b.Down);
                    break;
                case AxisChanged a when a.Id == _source?.Id:
                    _input[a.Axis] = a.Value;
                    break;
            }
        }

        /// <summary>Applies on/off changes, sends the remapped state if it changed, and publishes a snapshot.</summary>
        public void Tick()
        {
            var wanted = session.Enabled && _source is not null;
            if (wanted && _virtualPad is null)
                Engage();
            else if (!wanted && _virtualPad is not null)
                Disengage();

            var mapped = session.Mapping.Apply(_input);
            if (_virtualPad is not null && mapped != _lastSent)
            {
                _virtualPad.Send(mapped);
                _lastSent = mapped;
            }

            var last = session.Snapshot;
            var remapping = _virtualPad is not null;
            if (last.Controller != _source || last.Remapping != remapping || last.Physical != _input || last.Output != mapped)
                Volatile.Write(ref session._snapshot, new SessionSnapshot(_source, remapping, _input, mapped));
        }

        /// <summary>Hides the physical pad and plugs in the virtual one.</summary>
        private void Engage()
        {
            ledger.Hide(_hidePaths);
            _lastSent = default;
            _virtualPad = output.CreateXbox360();
            session._log($"Remapping \"{_source!.Name}\". Hid from games: {string.Join(", ", _hidePaths)}");
        }

        /// <summary>Unplugs the virtual pad and makes the physical one visible to games again.</summary>
        public void Disengage()
        {
            if (_virtualPad is not null)
                session._log("Remapping off. Your controller is visible to games again.");
            _virtualPad?.Dispose();
            _virtualPad = null;
            ledger.RestoreAll();
        }

        private void OnConnected(GamepadInfo info)
        {
            var instancePath = DeviceTree.InstancePathFromInterfacePath(info.Path);
            if (instancePath is null)
            {
                session._log($"Skipping \"{info.Name}\": SDL gave no device path ({info.Path ?? "none"}), so it can't be hidden from games.");
                return;
            }
            if (DeviceTree.IsOnViGEmBus(instancePath))
                return;
            if (_source is not null)
            {
                session._log($"Already using a controller; ignoring \"{info.Name}\" ({instancePath}).");
                return;
            }

            // Hide the HID device and its root device: games read Xbox pads through either one (raw input/DirectInput vs XInput).
            var device = hidHide.GamingDevices()
                .FirstOrDefault(d => string.Equals(d.DeviceInstancePath, instancePath, StringComparison.OrdinalIgnoreCase));
            _hidePaths = (device is null
                    ? [instancePath]
                    : new[] { device.DeviceInstancePath, device.BaseContainerDeviceInstancePath })
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            _source = info;
            _input = default;
            session._log($"Found \"{info.Name}\" ({instancePath}).");
        }
    }

    [DllImport("winmm.dll")]
    private static extern uint timeBeginPeriod(uint milliseconds);

    [DllImport("winmm.dll")]
    private static extern uint timeEndPeriod(uint milliseconds);
}
