using FlipFace.Output;
using FlipFace.Setup;

namespace FlipFace.Runtime;

public enum RequirementState
{
    Ready,

    /// <summary>Installed, but Windows has to restart before it works.</summary>
    NeedsRestart,

    Missing,
}

public sealed record SetupStatus(RequirementState ViGEmBus, RequirementState HidHide)
{
    public bool AllReady => ViGEmBus == RequirementState.Ready && HidHide == RequirementState.Ready;
}

/// <summary>Checks the two drivers FlipFace depends on. Fast enough to run at every start.</summary>
public static class SetupCheck
{
    public const string ViGEmBusDownload = "https://github.com/nefarius/ViGEmBus/releases/latest";
    public const string HidHideDownload = "https://github.com/nefarius/HidHide/releases/latest";

    public static SetupStatus Run() => new(CheckViGEmBus(), CheckHidHide());

    private static RequirementState CheckViGEmBus()
    {
        // Connecting is the real test: it fails if the driver is missing or not loaded.
        using var output = ViGEmOutput.TryConnect();
        if (output is not null)
            return RequirementState.Ready;
        return DriverServices.Query("ViGEmBus") == DriverState.Missing ? RequirementState.Missing : RequirementState.NeedsRestart;
    }

    private static RequirementState CheckHidHide()
    {
        var cli = HidHide.TryFind();
        var driver = DriverServices.Query("HidHide");
        if (cli is null && driver == DriverState.Missing)
            return RequirementState.Missing;
        // The installer adds the filter driver, but it only attaches to devices after a restart.
        return cli is not null && driver == DriverState.Running ? RequirementState.Ready : RequirementState.NeedsRestart;
    }
}
