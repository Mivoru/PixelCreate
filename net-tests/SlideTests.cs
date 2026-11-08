using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>The slide on Ctrl: a fast burst from a run that slows, and a jump out of it that keeps the speed.</summary>
public class SlideTests
{
    private static readonly World Field = new(Array.Empty<Aabb>(), null, Vector3.Zero, Array.Empty<EnemySpawn>());

    private static PlayerState Run(int ticks, ref uint tick)
    {
        var state = PlayerState.Spawn(Vector3.Zero);
        for (var i = 0; i < ticks; i++)
        {
            Movement.Step(ref state, new InputCmd { Tick = tick++, MoveZ = -127 }, Field);
        }

        return state;
    }

    private static float Speed(in PlayerState state) => new Vector2(state.Velocity.X, state.Velocity.Z).Length();

    [Fact]
    public void Slide_BurstsPastRunningSpeedThenSlows()
    {
        uint tick = 0;
        var state = Run(60, ref tick);
        var running = Speed(state);
        var stamina = state.Stamina;
        Movement.Step(ref state, new InputCmd { Tick = tick++, MoveZ = -127, Buttons = Buttons.Slide }, Field);
        Assert.Equal(Tuning.SlideTicks - 1, state.SlideTicks);
        Assert.True(Speed(state) > running * 1.6f, $"A slide must be much faster than a run; {Speed(state):F1} against {running:F1}.");
        Assert.True(state.Stamina < stamina);
        for (var i = 0; i < Tuning.SlideTicks; i++)
        {
            Movement.Step(ref state, new InputCmd { Tick = tick++, MoveZ = -127 }, Field);
        }

        Assert.Equal(0, state.SlideTicks);
        Assert.True(Speed(state) <= running + 0.5f);
    }

    [Fact]
    public void Slide_NeedsARunningStart()
    {
        var state = PlayerState.Spawn(Vector3.Zero);
        Movement.Step(ref state, new InputCmd { Tick = 1, Buttons = Buttons.Slide }, Field);
        Assert.Equal(0, state.SlideTicks);
    }

    [Fact]
    public void JumpOutOfASlide_KeepsTheSpeed()
    {
        uint tick = 0;
        var state = Run(60, ref tick);
        Movement.Step(ref state, new InputCmd { Tick = tick++, MoveZ = -127, Buttons = Buttons.Slide }, Field);
        Movement.Step(ref state, new InputCmd { Tick = tick++, MoveZ = -127 }, Field);
        var sliding = Speed(state);
        Movement.Step(ref state, new InputCmd { Tick = tick++, MoveZ = -127, Buttons = Buttons.Jump }, Field);
        Assert.Equal(0, state.SlideTicks);
        Assert.True(state.Velocity.Y > 0f);
        for (var i = 0; i < 10; i++)
        {
            Movement.Step(ref state, new InputCmd { Tick = tick++, MoveZ = -127 }, Field);
        }

        Assert.True(Speed(state) > Tuning.MoveSpeed * 1.2f, $"In the air the slide's speed must carry on; it was {Speed(state):F1} after {sliding:F1}.");
    }
}
