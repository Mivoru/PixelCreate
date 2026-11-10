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
