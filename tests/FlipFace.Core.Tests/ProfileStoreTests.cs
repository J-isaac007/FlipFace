using FlipFace.Core;

namespace FlipFace.Core.Tests;

public sealed class ProfileStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "FlipFaceTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void Profile_survives_a_save_and_reload()
    {
        var profile = new Profile
        {
            Name = "Shooter",
            Buttons = { [PadControl.East] = PadControl.South, [PadControl.Guide] = null, [PadControl.LeftTrigger] = PadControl.LeftShoulder },
            RightStick = new() { Deadzone = 0.1, InvertY = true },
            SwapSticks = true,
            TriggerThreshold = 0.3,
        };
        var store = new ProfileStore(_directory);

        store.Save(profile);
        var loaded = store.Find("shooter");

        Assert.NotNull(loaded);
        Assert.Equal(profile.Buttons, loaded.Buttons);
        Assert.Equal(profile with { Buttons = loaded.Buttons }, loaded);
    }

    [Fact]
    public void Hand_written_json_uses_control_names()
    {
        var profile = ProfileStore.Deserialize("""
            {
              // comments and trailing commas are fine in hand-edited files
              "name": "Mine",
              "buttons": { "East": "South", "Guide": null, },
            }
            """, "mine.json");

        Assert.Equal(PadControl.South, profile.Buttons[PadControl.East]);
        Assert.Null(profile.Buttons[PadControl.Guide]);
        Assert.Equal(new StickSettings(), profile.LeftStick);
    }

    [Fact]
    public void Unknown_control_name_is_reported_with_the_file_name()
    {
        var error = Assert.Throws<InvalidDataException>(() =>
            ProfileStore.Deserialize("""{ "name": "Bad", "buttons": { "East": "Jump" } }""", "bad.json"));

        Assert.StartsWith("bad.json:", error.Message);
    }

    [Fact]
    public void Deleting_a_saved_preset_override_brings_the_preset_back()
    {
        var store = new ProfileStore(_directory);
        store.Save(Presets.MatchLabels with { SwapSticks = true });
        store.Save(new Profile { Name = "Mine" });

        Assert.NotNull(store.FileFor("match LABELS"));
        Assert.Null(store.FileFor("Default"));
        Assert.True(store.Delete("Match labels"));
        Assert.True(store.Delete("Mine"));
        Assert.False(store.Delete("Mine"));

        Assert.False(store.Find("Match labels")!.SwapSticks);
        Assert.Null(store.Find("Mine"));
    }

    [Fact]
    public void Saved_profile_replaces_preset_and_broken_files_are_skipped()
    {
        var store = new ProfileStore(_directory);
        store.Save(Presets.MatchLabels with { SwapSticks = true });
        File.WriteAllText(Path.Combine(_directory, "broken.json"), "{ not json");

        var profiles = store.List(out var problems);

        Assert.True(profiles.Single(p => p.Name == "Match labels").SwapSticks);
        Assert.Contains(profiles, p => p.Name == "Default");
        Assert.Single(problems);
        Assert.StartsWith("broken.json:", problems[0]);
    }
}
