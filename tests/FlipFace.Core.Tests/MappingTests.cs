using FlipFace.Core;

namespace FlipFace.Core.Tests;

public class MappingTests
{
    private static PadState Pressed(params PadButton[] buttons)
    {
        var state = new PadState();
        foreach (var button in buttons)
            state.SetButton(button, true);
        return state;
    }

    private static Mapping Map(params (PadControl Source, PadControl? Target)[] buttons) =>
        new(new Profile { Name = "test", Buttons = buttons.ToDictionary(b => b.Source, b => b.Target) });

    [Fact]
    public void PadControl_mirrors_PadButton()
    {
        foreach (var button in Enum.GetValues<PadButton>())
            Assert.Equal(button.ToString(), ((PadControl)button).ToString());
    }

    [Theory]
    [InlineData(PadButton.East, PadButton.South)]  // printed A -> Xbox A
    [InlineData(PadButton.South, PadButton.East)]  // printed B -> Xbox B
    [InlineData(PadButton.North, PadButton.West)]  // printed X -> Xbox X
    [InlineData(PadButton.West, PadButton.North)]  // printed Y -> Xbox Y
    public void MatchLabels_swaps_face_buttons(PadButton physical, PadButton expected)
    {
        var output = new Mapping(Presets.MatchLabels).Apply(Pressed(physical));

        Assert.Equal(Pressed(expected), output);
    }

    [Fact]
    public void Default_passes_everything_through()
    {
        var input = Pressed(PadButton.South, PadButton.LeftShoulder, PadButton.DPadUp) with
        {
            LeftX = -12000, LeftY = 32767, RightX = 5, RightY = -32768, LeftTrigger = 100, RightTrigger = 32767,
        };

        Assert.Equal(input, new Mapping(Presets.Default).Apply(input));
    }

    [Fact]
    public void Two_sources_on_one_target_hold_it_until_both_release()
    {
        var map = Map((PadControl.West, PadControl.South));

        Assert.Equal(Pressed(PadButton.South), map.Apply(Pressed(PadButton.South, PadButton.West)));
        Assert.Equal(Pressed(PadButton.South), map.Apply(Pressed(PadButton.West)));
        Assert.Equal(new PadState(), map.Apply(new PadState()));
    }

    [Fact]
    public void Null_target_turns_a_button_off()
    {
        var map = Map((PadControl.Guide, null));

        Assert.Equal(new PadState(), map.Apply(Pressed(PadButton.Guide)));
    }

    [Fact]
    public void Trigger_mapped_to_button_presses_it_past_the_threshold()
    {
        var map = new Mapping(new Profile
        {
            Name = "test",
            Buttons = { [PadControl.RightTrigger] = PadControl.South },
            TriggerThreshold = 0.5,
        });

        Assert.Equal(new PadState(), map.Apply(new PadState { RightTrigger = 16000 }));
        Assert.Equal(Pressed(PadButton.South), map.Apply(new PadState { RightTrigger = 17000 }));
    }

    [Fact]
    public void Button_mapped_to_trigger_pulls_it_fully()
    {
        var map = Map((PadControl.RightShoulder, PadControl.RightTrigger));

        Assert.Equal(new PadState { RightTrigger = short.MaxValue }, map.Apply(Pressed(PadButton.RightShoulder)));
    }

    [Fact]
    public void Swapped_triggers_keep_half_pulls()
    {
        var map = Map((PadControl.LeftTrigger, PadControl.RightTrigger), (PadControl.RightTrigger, PadControl.LeftTrigger));

        Assert.Equal(new PadState { LeftTrigger = 300, RightTrigger = 9000 },
            map.Apply(new PadState { LeftTrigger = 9000, RightTrigger = 300 }));
    }

    [Fact]
    public void Trigger_shared_by_button_and_trigger_takes_the_deepest_pull()
    {
        var map = Map((PadControl.RightShoulder, PadControl.RightTrigger));
        var input = Pressed(PadButton.RightShoulder) with { RightTrigger = 1000 };

        Assert.Equal(short.MaxValue, map.Apply(input).RightTrigger);
    }

    [Fact]
    public void SwapSticks_exchanges_left_and_right()
    {
        var map = new Mapping(new Profile { Name = "test", SwapSticks = true });

        Assert.Equal(new PadState { LeftX = 3, LeftY = 4, RightX = 1, RightY = 2 },
            map.Apply(new PadState { LeftX = 1, LeftY = 2, RightX = 3, RightY = 4 }));
    }

    [Fact]
    public void Invert_flips_an_axis_without_overflowing()
    {
        var map = new Mapping(new Profile { Name = "test", LeftStick = new() { InvertX = true, InvertY = true } });

        Assert.Equal(new PadState { LeftX = short.MaxValue, LeftY = -20000 },
            map.Apply(new PadState { LeftX = short.MinValue, LeftY = 20000 }));
    }

    [Fact]
    public void Deadzone_centers_small_moves_and_keeps_full_travel()
    {
        var map = new Mapping(new Profile { Name = "test", RightStick = new() { Deadzone = 0.2 } });

        Assert.Equal(new PadState(), map.Apply(new PadState { RightX = 3000, RightY = -3000 }));
        Assert.Equal(new PadState { RightX = short.MaxValue }, map.Apply(new PadState { RightX = short.MaxValue }));

        // Halfway between the deadzone edge (0.2) and full (1.0) is 0.6 of travel.
        var halfway = map.Apply(new PadState { RightY = (short)(0.6 * short.MaxValue) }).RightY;
        Assert.InRange(halfway, 0.49 * short.MaxValue, 0.51 * short.MaxValue);
    }
}
