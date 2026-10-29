using System.Runtime.InteropServices;

namespace FlipFace.Setup;

/// <summary>Looks up devices in the Windows device tree (Configuration Manager API).</summary>
public static class DeviceTree
{
    /// <summary>
    /// Turns a device interface path such as <c>\\?\HID#VID_045E&amp;PID_028E&amp;IG_00#8&amp;1a79&amp;0&amp;0000#{guid}</c>
    /// into its instance path <c>HID\VID_045E&amp;PID_028E&amp;IG_00\8&amp;1a79&amp;0&amp;0000</c>. Returns null for anything else.
    /// </summary>
    public static string? InstancePathFromInterfacePath(string? interfacePath)
    {
        if (interfacePath is null || !interfacePath.StartsWith(@"\\?\", StringComparison.Ordinal))
            return null;
        var body = interfacePath[4..];
        var guidStart = body.LastIndexOf("#{", StringComparison.Ordinal);
        if (guidStart < 0)
            return null;
        return body[..guidStart].Replace('#', '\\');
    }

    /// <summary>True when the device sits under the ViGEmBus driver, i.e. it is a virtual pad (ours or another app's).</summary>
    public static bool IsOnViGEmBus(string instancePath)
    {
        if (CM_Locate_DevNodeW(out var node, instancePath, 0) != CR_SUCCESS)
            return false;
        while (CM_Get_Parent(out var parent, node, 0) == CR_SUCCESS)
        {
            if (string.Equals(GetService(parent), "ViGEmBus", StringComparison.OrdinalIgnoreCase))
                return true;
            node = parent;
        }
        return false;
    }

    private static string? GetService(uint node)
    {
        var buffer = new char[256];
        var length = (uint)(buffer.Length * sizeof(char));
        if (CM_Get_DevNode_Registry_PropertyW(node, CM_DRP_SERVICE, out _, buffer, ref length, 0) != CR_SUCCESS)
            return null;
        return new string(buffer).TrimEnd('\0');
    }

    private const uint CR_SUCCESS = 0;
    private const uint CM_DRP_SERVICE = 0x5;

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Locate_DevNodeW(out uint devInst, string deviceId, uint flags);

    [DllImport("cfgmgr32.dll")]
    private static extern uint CM_Get_Parent(out uint parent, uint devInst, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_DevNode_Registry_PropertyW(
        uint devInst, uint property, out uint regDataType, char[] buffer, ref uint length, uint flags);
}
