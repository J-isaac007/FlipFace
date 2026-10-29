using System.ServiceProcess;

namespace FlipFace.Setup;

public enum DriverState
{
    /// <summary>No such driver is installed.</summary>
    Missing,

    /// <summary>Installed but not loaded, which usually means Windows hasn't restarted since installing it.</summary>
    Stopped,

    Running,
}

/// <summary>Looks up kernel driver services (ViGEmBus, HidHide) with the Service Control Manager.</summary>
public static class DriverServices
{
    public static DriverState Query(string serviceName)
    {
        var driver = ServiceController.GetDevices()
            .FirstOrDefault(s => string.Equals(s.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase));
        if (driver is null)
            return DriverState.Missing;
        using (driver)
            return driver.Status == ServiceControllerStatus.Running ? DriverState.Running : DriverState.Stopped;
    }
}
