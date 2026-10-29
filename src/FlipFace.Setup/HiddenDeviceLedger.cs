namespace FlipFace.Setup;

/// <summary>
/// Hides devices through HidHide and writes down what it hid, so a crash can never leave your controller
/// invisible to every game: the next start calls <see cref="RestoreAll"/> and unhides whatever is still listed.
/// </summary>
public sealed class HiddenDeviceLedger(HidHide hidHide)
{
    private static readonly string LedgerPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlipFace", "hidden-devices.txt");

    public void Hide(IEnumerable<string> instancePaths)
    {
        var all = Read().Union(instancePaths, StringComparer.OrdinalIgnoreCase).ToList();
        // Record first: if we crash between the two steps, the device is listed and gets unhidden next time.
        Write(all);
        foreach (var path in instancePaths)
            hidHide.Hide(path);
    }

    /// <summary>Unhides everything this ledger hid. Returns the paths that were restored.</summary>
    public IReadOnlyList<string> RestoreAll()
    {
        var paths = Read();
        foreach (var path in paths)
            hidHide.Unhide(path);
        Write([]);
        return paths;
    }

    private static List<string> Read() =>
        File.Exists(LedgerPath)
            ? File.ReadAllLines(LedgerPath).Where(l => l.Length > 0).ToList()
            : [];

    private static void Write(IEnumerable<string> paths)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LedgerPath)!);
        File.WriteAllLines(LedgerPath, paths);
    }
}
