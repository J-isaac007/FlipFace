// FlipFace headless: remaps the first physical controller to a virtual Xbox 360 pad until you press Ctrl+C.
using System.Runtime.InteropServices;
using FlipFace.Core;
using FlipFace.Runtime;

Console.WriteLine("FlipFace: printed A/B/X/Y now act as Xbox A/B/X/Y. Press Ctrl+C to stop.");

using var cancel = new CancellationTokenSource();
using var finished = new ManualResetEventSlim();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancel.Cancel();
};
// Closing the console window kills the process shortly after this returns, so hold it open until the
// controller has been unhidden. (If that still fails, the next run unhides it at startup.)
using var onClose = PosixSignalRegistration.Create(PosixSignal.SIGHUP, _ =>
{
    cancel.Cancel();
    finished.Wait(TimeSpan.FromSeconds(4));
});

try
{
    new RemapSession(Presets.MatchLabels, message => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}"))
        .Run(cancel.Token);
    Console.WriteLine("Stopped. Your controller is visible to games again.");
    return 0;
}
catch (SetupMissingException e)
{
    Console.Error.WriteLine(e.Message);
    return 2;
}
finally
{
    finished.Set();
}
