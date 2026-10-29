using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlipFace.Setup;

/// <param name="DeviceInstancePath">The HID device games read from.</param>
/// <param name="BaseContainerDeviceInstancePath">Its root device (for Xbox pads, the one XInput reads from).</param>
public sealed record GamingDevice(
    [property: JsonPropertyName("deviceInstancePath")] string DeviceInstancePath,
    [property: JsonPropertyName("baseContainerDeviceInstancePath")] string BaseContainerDeviceInstancePath,
    [property: JsonPropertyName("present")] bool Present);

/// <summary>Drives HidHide through its command-line client, which ships with the driver and needs no admin rights.</summary>
public sealed class HidHide
{
    private static readonly string DefaultCliPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Nefarius Software Solutions", "HidHide", "x64", "HidHideCLI.exe");

    private readonly string _cliPath;

    private HidHide(string cliPath) => _cliPath = cliPath;

    /// <summary>Returns null when HidHide isn't installed.</summary>
    public static HidHide? TryFind() => File.Exists(DefaultCliPath) ? new HidHide(DefaultCliPath) : null;

    /// <summary>Lets <paramref name="exePath"/> keep seeing hidden devices. HidHide remembers this across restarts.</summary>
    public void RegisterApp(string exePath) => Run("--app-reg", exePath);

    /// <summary>Forgets registered apps whose files no longer exist (old builds, moved installs).</summary>
    public void RemoveMissingApps() => Run("--app-clean");

    public void CloakOn() => Run("--cloak-on");

    public void Hide(string instancePath) => Run("--dev-hide", instancePath);

    public void Unhide(string instancePath) => Run("--dev-unhide", instancePath);

    public IReadOnlyList<GamingDevice> GamingDevices()
    {
        var groups = JsonSerializer.Deserialize<List<DeviceGroup>>(Run("--dev-gaming")) ?? [];
        return groups.SelectMany(g => g.Devices).ToList();
    }

    private sealed record DeviceGroup([property: JsonPropertyName("devices")] List<GamingDevice> Devices);

    private string Run(params string[] args)
    {
        var start = new ProcessStartInfo(_cliPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
            start.ArgumentList.Add(arg);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start HidHideCLI.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"HidHideCLI {string.Join(' ', args)} failed ({process.ExitCode}): {error}{output}".Trim());
        return output;
    }
}
