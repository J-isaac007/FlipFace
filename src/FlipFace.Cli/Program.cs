// FlipFace headless: remaps the first physical controller to a virtual Xbox 360 pad until you press Ctrl+C.
//   (no args)                   use the "Match labels" profile
//   --profile "<name>"          use another profile
//   --profiles                  list profiles and where they're saved
//   --new-profile "<name>"      save a copy of "Match labels" under a new name, ready to edit
//   --restore                   make a controller FlipFace left hidden visible to games again
using System.Runtime.InteropServices;
using FlipFace.Core;
using FlipFace.Runtime;

var store = new ProfileStore(ProfileStore.DefaultDirectory);

switch (args)
{
    case ["--profiles"]:
        var profiles = store.List(out var problems);
        Console.WriteLine($"Profiles (saved ones live in {store.Directory}):");
        foreach (var p in profiles)
            Console.WriteLine($"  {p.Name}");
        foreach (var problem in problems)
            Console.WriteLine($"  Skipped {problem}");
        return 0;

    case ["--new-profile", var newName]:
        if (store.Find(newName) is not null && !Presets.All.Any(p => p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
        {
            Console.Error.WriteLine($"A profile named \"{newName}\" already exists.");
            return 1;
        }
        var path = store.Save(Presets.MatchLabels with { Name = newName });
        Console.WriteLine($"Saved {path}");
        Console.WriteLine($"Edit it, then run with --profile \"{newName}\".");
        return 0;

    case ["--restore"]:
        RemapSession.TryEmergencyRestore();
        Console.WriteLine("Any controller FlipFace had hidden is visible to games again.");
        return 0;

    case [] or ["--profile", _]:
        break;

    default:
        Console.Error.WriteLine("Usage: FlipFace.Cli [--profile \"<name>\" | --profiles | --new-profile \"<name>\" | --restore]");
        return 1;
}

var profileName = args is ["--profile", var chosen] ? chosen : Presets.MatchLabels.Name;
Profile? profile;
try
{
    profile = store.Find(profileName);
}
catch (InvalidDataException e)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}
if (profile is null)
{
    Console.Error.WriteLine($"No profile named \"{profileName}\". Run with --profiles to see them.");
    return 1;
}

Console.WriteLine($"FlipFace: using profile \"{profile.Name}\". Press Ctrl+C to stop.");

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
    new RemapSession(new Mapping(profile), message => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}"))
        .Run(cancel.Token);
    Console.WriteLine("Stopped. Your controller is visible to games again.");
    return 0;
}
catch (CannotStartException e)
{
    Console.Error.WriteLine(e.Message);
    return 2;
}
catch (Exception e)
{
    // Catching here matters: an unhandled exception can end the process without running the
    // session's finally block, which is what unhides the controller.
    Console.Error.WriteLine($"FlipFace stopped unexpectedly: {e}");
    return 3;
}
finally
{
    finished.Set();
}
