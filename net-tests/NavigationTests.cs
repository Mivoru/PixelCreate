using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>Where the map and the trail on the ground lead: the target of the tracked quest step, through doors, along roads.</summary>
public class NavigationTests
{
    private static readonly Zone Valley = Zone.Load("meadow-valley");

    private sealed class Playing(Vector3 at, Dictionary<string, int>? bag = null) : IStoryContext
    {
        public WorldState World { get; } = new();
        public int Rank => 1;
        public int Gold => 0;
        public int Level => 1;
        public int SaleValue => 0;
        public int ItemCount(string item) => bag?.GetValueOrDefault(item) ?? 0;
        public int Attribute(string name) => 0;
        public bool Near(string role) => false;
        public Vector3 Position => at;
    }

    private static readonly Vector3 Road = new(52, 0, 44);
    private static readonly Vector3 GuildDoor = new(-36, 0, 23.5f);

    [Fact]
    public void TheWayToMira_LeadsToTheGuildDoorFromOutside()
    {
        var player = new Playing(Road);
        player.World.SetQuest("prologue_fallen", new QuestState { Status = QuestStatus.Active, Step = 2 });
        var target = Navigation.QuestTarget(player, Valley.World);
        Assert.NotNull(target);
        Assert.Equal("Mira", target!.Value.Label);
        var walk = Navigation.Reachable(Valley.World, player.Position, target.Value.At);
        Assert.True(Vector2.Distance(new Vector2(walk.X, walk.Z), new Vector2(GuildDoor.X, GuildDoor.Z)) < 1.5f, $"led to {walk}, not the Guild door");
    }

    [Fact]
    public void InTheCage_NothingIsShownToWalkTo()
    {
        var player = new Playing(new Vector3(69.9f, 0, 101));
        player.World.SetQuest("prologue_fallen", new QuestState { Status = QuestStatus.Active, Step = 0 });
        Assert.Null(Navigation.QuestTarget(player, Valley.World));
    }

    [Fact]
    public void ADeliveryJob_LeadsToWhereTheItemDrops_ThenOnlySaysReturn()
    {
        var empty = new Playing(Road);
        empty.World.SetQuest("gel_for_alchemist", new QuestState { Status = QuestStatus.Active, Step = 0 });
        var source = Navigation.QuestTarget(empty, Valley.World);
        Assert.NotNull(source);
        Assert.NotEqual("Mira", source!.Value.Label);

        var full = new Playing(Road, new Dictionary<string, int> { ["slime_gel"] = 6 });
        full.World.SetQuest("gel_for_alchemist", new QuestState { Status = QuestStatus.Active, Step = 0 });
        // With everything in the bag the objective says to return to the Guild; no way is drawn.
        Assert.Null(Navigation.QuestTarget(full, Valley.World));
        Assert.True(source.Value.Radius > 0f, "the place where the item drops is an area");
    }

    [Fact]
    public void EveryStepThatLeadsSomewhere_HasAPlaceTheMapCanMark()
    {
        // "The player always knows what to do": a step with a route must resolve to a place.
        var missing = new List<string>();
        foreach (var quest in Story.Quests)
        {
            for (var i = 0; i < quest.Steps.Length; i++)
            {
                if (!quest.Steps[i].Route)
                {
                    continue;
                }

                var player = new Playing(Road);
                player.World.SetQuest(quest.Id, new QuestState { Status = QuestStatus.Active, Step = (byte)i });
                if (Navigation.QuestTarget(player, Valley.World) is null)
                {
                    missing.Add($"{quest.Id} step {i}");
                }
            }
        }

        Assert.True(missing.Count == 0, "Steps with nowhere to lead: " + string.Join(", ", missing));
    }

    [Fact]
    public void AHunt_MarksTheArea_AndTheReportStepDrawsNoWay()
    {
        var hunting = new Playing(Road);
        hunting.World.SetQuest("slime_cull", new QuestState { Status = QuestStatus.Active, Step = 0 });
        var area = Navigation.QuestTarget(hunting, Valley.World);
        Assert.NotNull(area);
        Assert.True(area!.Value.Radius >= Navigation.CampAreaMargin);

        var done = new Playing(Road);
        done.World.SetQuest("slime_cull", new QuestState { Status = QuestStatus.Active, Step = 1 });
        Assert.Null(Navigation.QuestTarget(done, Valley.World));
    }

    private static readonly World Open = new(Array.Empty<Aabb>());

    // A way that bends: 40 m east, then 40 m north.
    private static readonly Vector3[] Bend = { new(0, 0, 0), new(40, 0, 0), new(40, 0, 40) };

    [Fact]
    public void APlayerWhoCutsTheBend_IsLedOnAhead_NotBackToTheRoad()
    {
        // Off the way, near the far leg: the join lies on that leg, ahead, not on the first leg behind.
        var join = Navigation.Rejoin(Bend, new Vector3(30, 0, 25), Open);
        Assert.NotNull(join);
        Assert.Equal(1, join!.Value.Leg);
        Assert.True(join.Value.Point.Z >= 25f, $"joined at {join.Value.Point}");
    }

    [Fact]
    public void AClearLineToTheGoal_IsTakenStraight()
    {
        // The end of the way is part of the way: with nothing in between, the shortest trip goes straight to it.
        var join = Navigation.Rejoin(Bend, new Vector3(5, 0, 30), Open);
        Assert.NotNull(join);
        var straightToGoal = Vector2.Distance(new Vector2(5, 30), new Vector2(40, 40));
        var trip = Vector2.Distance(new Vector2(5, 30), new Vector2(join!.Value.Point.X, join.Value.Point.Z))
                   + Vector2.Distance(new Vector2(join.Value.Point.X, join.Value.Point.Z), new Vector2(40, 40));
        Assert.True(trip <= straightToGoal + 2.5f, $"trip {trip} against straight {straightToGoal}");
    }

    [Fact]
    public void AWallBetween_KeepsThePlayerToTheWay()
    {
        // A wall across the straight line to the far leg: the join must be one the player can walk to.
        var wall = new World(new[] { new Aabb(new Vector3(32, -1, 5), new Vector3(34, 5, 45)) });
        var join = Navigation.Rejoin(Bend, new Vector3(20, 0, 20), wall);
        Assert.NotNull(join);
        Assert.True(join!.Value.Point.X < 32f || join.Value.Point.Z < 5f, $"joined through the wall at {join.Value.Point}");
    }

    [Fact]
    public void TheRouteToTheGuild_RunsAlongTheRoads_AndEndsAtTheDoor()
    {
        var roads = new RoadMap(Valley.World.Roads);
        var from = Road with { Y = Valley.World.GroundHeight(Road.X, Road.Z) };
        var door = GuildDoor with { Y = Valley.World.GroundHeight(GuildDoor.X, GuildDoor.Z) };
        var way = roads.Route(from, door, Valley.World);
        Assert.True(way.Count >= 2);
        Assert.True(Vector3.Distance(way[^1] with { Y = 0 }, door with { Y = 0 }) < 0.5f);
    }
}
