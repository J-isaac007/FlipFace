using FlipFace.Core;

namespace FlipFace.Core.Tests;

public class RemapEngineTests
{
    private static PadState Pressed(params PadButton[] buttons)
    {
        var state = new PadState();
        foreach (var button in buttons)
            state.SetButton(button, true);
        return state;
    }

    [Theory]
    [InlineData(PadButton.East, PadButton.South)]  // printed A -> Xbox A
    [InlineData(PadButton.South, PadButton.East)]  // printed B -> Xbox B
    [InlineData(PadButton.North, PadButton.West)]  // printed X -> Xbox X
    [InlineData(PadButton.West, PadButton.North)]  // printed Y -> Xbox Y
    public void MatchLabels_swaps_face_buttons(PadButton physical, PadButton expected)
    {
        var output = RemapEngine.Apply(Pressed(physical), Presets.MatchLabels);

        Assert.Equal(Pressed(expected), output);
    }

    [Fact]
    public void Unmapped_buttons_pass_through()
    {
        var input = Pressed(PadButton.LeftShoulder, PadButton.DPadUp, PadButton.Start);

        Assert.Equal(input, RemapEngine.Apply(input, Presets.MatchLabels));
    }

    [Fact]
    public void Axes_pass_through_unchanged()
    {
        var input = new PadState { LeftX = -12000, LeftY = 32767, RightX = 5, RightY = -32768, LeftTrigger = 100, RightTrigger = 32767 };

        Assert.Equal(input, RemapEngine.Apply(input, Presets.MatchLabels));
    }

    [Fact]
    public void Two_sources_on_one_target_hold_it_until_both_release()
    {
        var map = new ButtonMap(new Dictionary<PadButton, PadButton> { [PadButton.West] = PadButton.South });

        Assert.Equal(Pressed(PadButton.South), RemapEngine.Apply(Pressed(PadButton.South, PadButton.West), map));
        Assert.Equal(Pressed(PadButton.South), RemapEngine.Apply(Pressed(PadButton.West), map));
        Assert.Equal(new PadState(), RemapEngine.Apply(new PadState(), map));
    }

    [Fact]
    public void Swapped_pair_pressed_together_presses_both_targets()
    {
        var output = RemapEngine.Apply(Pressed(PadButton.East, PadButton.South), Presets.MatchLabels);

        Assert.Equal(Pressed(PadButton.South, PadButton.East), output);
    }
}
