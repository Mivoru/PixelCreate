using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>Herds on an empty field, stepped directly, so each behaviour can be watched on its own.</summary>
public sealed class AnimalTests
{
    private sealed class Quiet : IEnemyEvents
    {
        public void Splash(Enemy enemy) { }
        public void Spit(Enemy enemy, Target target) { }
        public void Fire(Enemy enemy, Ability ability, Target? target) { }
        public void CallForHelp(Enemy enemy, int targetId) { }
    }

    private static readonly World Field = new(Array.Empty<Aabb>(), null, Vector3.Zero, Array.Empty<EnemySpawn>());

    private static List<Enemy> Herd(string type, int count, float radius = 6f)
    {
        var home = new EnemySpawn(Vector3.Zero, radius, count, 1, 1, 1800, false, new[] { type });
        var herd = new List<Enemy>();
        var random = new Random(3);
        for (var i = 0; i < count; i++)
        {
            var animal = new Enemy { Id = i, Type = 0, Home = home, Herd = herd };
            animal.Reset(random, Field);
            herd.Add(animal);
        }

        return herd;
    }

    private static void Run(List<Enemy> herd, int ticks, params Target[] targets)
    {
        var random = new Random(9);
        var events = new Quiet();
        for (var tick = 0; tick < ticks; tick++)
        {
            foreach (var animal in herd)
            {
                animal.Step(Field, random, targets, events);
                animal.Position += animal.Velocity / Tuning.TickRate;
            }
        }
    }

    [Fact]
    public void Herd_StaysTogetherAndMostlyGrazes()
    {
        var herd = Herd("sheep", 5);
        var grazing = 0;
        for (var second = 0; second < 120; second++)
        {
            Run(herd, Tuning.TickRate);
            grazing += herd.Count(a => a.Action == EnemyAction.Graze);
            var centre = herd.Aggregate(Vector3.Zero, (sum, a) => sum + a.Position) / herd.Count;
            Assert.All(herd, a => Assert.InRange(Vector3.Distance(a.Position, centre), 0f, 9f));
        }

        Assert.True(grazing > 120 * 5 / 3, $"Animals should graze most of the time, grazed {grazing} of {120 * 5} samples.");
    }

    [Fact]
    public void Scare_SpreadsThroughTheHerd()
    {
        var herd = Herd("sheep", 5, 3f);
        Run(herd, 30);
        var victim = herd[0];
        var player = new Target(1, victim.Position + new Vector3(1f, 0, 0), Vector3.Zero);
        Run(herd, 2, player);
        Assert.All(herd, a => Assert.True(a.Fleeing, $"Sheep {a.Id} should run with the herd."));
        Run(herd, 60, player);
        Assert.All(herd, a => Assert.True(Vector3.Distance(a.Position, player.Position) > 3f));
    }

    [Fact]
    public void Cow_WatchesACalmWalkerButDoesNotRun()
    {
        var herd = Herd("cow", 1, 2f);
        Run(herd, 30);
        var cow = herd[0];
        var walker = new Target(1, cow.Position + new Vector3(3f, 0, 0), Vector3.Zero);
        Run(herd, 5, walker);
        Assert.Equal(EnemyAction.Alert, cow.Action);
        Assert.False(cow.Fleeing);
        var look = new Vector3(-MathF.Sin(cow.Facing), 0, -MathF.Cos(cow.Facing));
        Assert.True(Vector3.Dot(look, Vector3.UnitX) > 0.9f, "The cow turns to look at the walker.");
    }

    [Fact]
    public void Deer_BoltFromARunnerFurtherAwayThanFromAWalker()
    {
        var calm = Herd("deer", 1, 2f);
        Run(calm, 30);
        Run(calm, 3, new Target(1, calm[0].Position + new Vector3(14f, 0, 0), Vector3.Zero));
        Assert.False(calm[0].Fleeing);

        var hunted = Herd("deer", 1, 2f);
        Run(hunted, 30);
        Run(hunted, 3, new Target(1, hunted[0].Position + new Vector3(14f, 0, 0), new Vector3(-6f, 0, 0)));
        Assert.True(hunted[0].Fleeing);
    }

    [Fact]
    public void Boar_NeverRunsAndFightsOnlyOnceHit()
    {
        var herd = Herd("pig", 1, 2f);
        Run(herd, 30);
        var boar = herd[0];
        var player = new Target(1, boar.Position + new Vector3(1.2f, 0, 0), Vector3.Zero);
        Run(herd, 30, player);
        Assert.False(boar.Fleeing);
        Assert.Equal(-1, boar.TargetId);
        boar.TakeHit(1, player.Position, 1);
        Run(herd, 2, player);
        Assert.Equal(1, boar.TargetId);
    }

    [Fact]
    public void Boars_DefendEachOther()
    {
        var herd = Herd("pig", 3, 2f);
        Run(herd, 30);
        var player = new Target(1, herd[0].Position + new Vector3(1.2f, 0, 0), Vector3.Zero);
        herd[0].TakeHit(1, player.Position, 1);
        Run(herd, 3, player);
        Assert.All(herd, boar => Assert.Equal(1, boar.TargetId));
    }
}
