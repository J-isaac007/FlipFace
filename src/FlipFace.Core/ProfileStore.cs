using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlipFace.Core;

/// <summary>Reads and writes profiles as JSON files, one per profile, in a folder.</summary>
public sealed class ProfileStore(string directory)
{
    public static string DefaultDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FlipFace", "profiles");

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() },
    };

    public string Directory => directory;

    public static string Serialize(Profile profile) => JsonSerializer.Serialize(profile, Json);

    /// <exception cref="InvalidDataException">The JSON isn't a valid profile; the message says why.</exception>
    public static Profile Deserialize(string json, string source)
    {
        Profile? profile;
        try
        {
            profile = JsonSerializer.Deserialize<Profile>(json, Json);
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"{source}: {e.Message}", e);
        }
        if (profile is null)
            throw new InvalidDataException($"{source}: file is empty.");
        // Hand-edited files may set sections to null; treat that like leaving them out.
        return profile with
        {
            Buttons = profile.Buttons ?? [],
            LeftStick = profile.LeftStick ?? new(),
            RightStick = profile.RightStick ?? new(),
        };
    }

    /// <summary>
    /// Built-in presets plus every saved profile; a saved profile replaces a preset with the same name.
    /// Files that can't be read are skipped and described in <paramref name="problems"/>.
    /// </summary>
    public IReadOnlyList<Profile> List(out IReadOnlyList<string> problems)
    {
        var found = new List<string>();
        var byName = new Dictionary<string, Profile>(StringComparer.OrdinalIgnoreCase);
        foreach (var preset in Presets.All)
            byName[preset.Name] = preset;

        if (System.IO.Directory.Exists(directory))
        {
            foreach (var file in System.IO.Directory.EnumerateFiles(directory, "*.json").Order())
            {
                try
                {
                    var profile = Deserialize(File.ReadAllText(file), Path.GetFileName(file));
                    byName[profile.Name] = profile;
                }
                catch (Exception e) when (e is InvalidDataException or IOException)
                {
                    found.Add(e.Message);
                }
            }
        }

        problems = found;
        return byName.Values.ToList();
    }

    public Profile? Find(string name) =>
        List(out _).FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Writes <paramref name="profile"/> to its file, replacing any earlier save. Returns the file path.</summary>
    public string Save(Profile profile)
    {
        System.IO.Directory.CreateDirectory(directory);
        var invalid = Path.GetInvalidFileNameChars();
        var fileName = new string(profile.Name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()) + ".json";
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, Serialize(profile));
        return path;
    }
}
