// FlipFace probe: shows what SDL sees for each connected controller.
//   (no args)    print every button and stick event live until Ctrl+C
//   --list       list connected controllers and exit
//   --identify   ask you to press the printed A, B, X, Y buttons and report where each one really sits
using FlipFace.Core;
using FlipFace.Input;

var mode = args.FirstOrDefault() ?? "--live";
using var reader = new SdlGamepadReader();
var events = new List<GamepadEvent>();

var pads = WaitForPads(reader, events, TimeSpan.FromMilliseconds(750));
if (pads.Count == 0)
{
    Console.WriteLine("No controllers found. Check the cable, then run joy.cpl to see whether Windows sees it.");
    return 1;
}

Console.WriteLine($"Found {pads.Count} controller(s):");
foreach (var pad in pads)
{
    Console.WriteLine($"  #{pad.Id}  {pad.Name}");
    Console.WriteLine($"       type {pad.Type}, VID {pad.VendorId:X4}, PID {pad.ProductId:X4}");
    Console.WriteLine($"       path {pad.Path ?? "(none)"}");
}

switch (mode)
{
    case "--list":
        return 0;
    case "--identify":
        Identify(reader, events);
        return 0;
    default:
        Live(reader, events);
        return 0;
}

static List<GamepadInfo> WaitForPads(SdlGamepadReader reader, List<GamepadEvent> events, TimeSpan window)
{
    var pads = new List<GamepadInfo>();
    var until = DateTime.UtcNow + window;
    while (DateTime.UtcNow < until)
    {
        events.Clear();
        reader.Poll(events);
        pads.AddRange(events.OfType<GamepadConnected>().Select(c => c.Info));
        Thread.Sleep(5);
    }
    return pads;
}

static void Live(SdlGamepadReader reader, List<GamepadEvent> events)
{
    Console.WriteLine();
    Console.WriteLine("Press buttons and move sticks. Ctrl+C to quit.");
    var stop = false;
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop = true; };

    // Sticks fire constantly; only print when an axis moves by a noticeable amount.
    var lastPrinted = new Dictionary<(uint, PadAxis), short>();
    while (!stop)
    {
        events.Clear();
        reader.Poll(events);
        foreach (var ev in events)
        {
            switch (ev)
            {
                case ButtonChanged b:
                    Console.WriteLine($"#{b.Id}  {b.Button,-13} (Xbox {b.Button.XboxLabel(),-5}) {(b.Down ? "down" : "up")}");
                    break;
                case AxisChanged a:
                    var key = (a.Id, a.Axis);
                    if (Math.Abs(a.Value - lastPrinted.GetValueOrDefault(key)) < 6000)
                        break;
                    lastPrinted[key] = a.Value;
                    Console.WriteLine($"#{a.Id}  {a.Axis,-13} {a.Value,6}");
                    break;
                case GamepadConnected c:
                    Console.WriteLine($"#{c.Id}  connected: {c.Info.Name}");
                    break;
                case GamepadDisconnected d:
                    Console.WriteLine($"#{d.Id}  disconnected");
                    break;
            }
        }
        Thread.Sleep(1);
    }
}

static void Identify(SdlGamepadReader reader, List<GamepadEvent> events)
{
    Console.WriteLine();
    var found = new List<(string Printed, PadButton Position)>();
    foreach (var printed in new[] { "A", "B", "X", "Y" })
    {
        Console.Write($"Press the button printed \"{printed}\" on your controller... ");
        var position = WaitForButtonDown(reader, events);
        found.Add((printed, position));
        Console.WriteLine($"{position} (games see Xbox {position.XboxLabel()})");
    }

    Console.WriteLine();
    Console.WriteLine("Printed  Position  Games see");
    foreach (var (printed, position) in found)
    {
        var mismatch = printed == position.XboxLabel() ? "" : "  <- mismatch";
        Console.WriteLine($"{printed,-8} {position,-9} Xbox {position.XboxLabel()}{mismatch}");
    }
}

static PadButton WaitForButtonDown(SdlGamepadReader reader, List<GamepadEvent> events)
{
    while (true)
    {
        events.Clear();
        reader.Poll(events);
        foreach (var ev in events)
        {
            if (ev is ButtonChanged { Down: true } b)
                return b.Button;
        }
        Thread.Sleep(1);
    }
}
