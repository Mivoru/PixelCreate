using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>The days of the people of Hearthmere: where they stand by the hour, and how they get there.</summary>
public class ScheduleTests
{
    private static readonly Zone Valley = Zone.Load("meadow-valley");

    private sealed class NoStory : IStoryContext
    {
        public WorldState World { get; } = new();
        public int Rank => 0;
        public int Gold => 0;
        public int Level => 1;
        public int SaleValue => 0;
        public int ItemCount(string item) => 0;
        public int Attribute(string name) => 0;
        public bool Near(string role) => false;
        public Vector3 Position => default;
    }

    private static GameServer Start(int hour)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var server = new GameServer(network.Connect(0), Valley.World, new ServerOptions { Seed = 1 });
        server.Calendar.Now = new GameTime(3, hour * 60);
        server.Step();
        return server;
    }

    [Fact]
    public void EveryoneWithADay_IsSomeoneOfTheWorld()
    {
        var known = Actors.For(Valley.World).Select(a => a.Id).ToHashSet();
        foreach (var id in Schedules.All.Keys)
        {
            Assert.True(known.Contains(id), $"schedules.json names '{id}', who is neither an NPC of the zone nor an actor");
        }
    }

    [Fact]
    public void StandingPlaces_AreFreeGround()
    {
        foreach (var (id, blocks) in Schedules.All)
        {
            // Where the zone itself puts someone is theirs (Wren stands behind her counter): only new places are checked.
            var own = Valley.World.Npcs.Where(n => n.Id == id).Select(n => n.Position).DefaultIfEmpty(new Vector3(float.MaxValue)).First();
            foreach (var block in blocks.Where(b => b.Pose == ActorPose.Stand))
            {
                var spot = block.At ?? block.Home!.Value;
                if (Vector2.Distance(new Vector2(spot.X, spot.Z), new Vector2(own.X, own.Z)) < 0.5f)
                {
                    continue;
                }

                spot.Y = Valley.World.GroundHeight(spot.X, spot.Z) + 0.1f;
                Assert.False(Valley.World.OverlapsPlayer(spot), $"{id} at {block.FromHour}:00 stands inside something at {spot.X}, {spot.Z}");
            }
        }
    }

    [Fact]
    public void InARainyHour_ThoseWhoWorkOutside_GoIndoors()
    {
        // Find the first rainy hour of the first days and look where Lina, the merchant, is.
        var (day, hour) = Enumerable.Range(1, 30).SelectMany(d => Enumerable.Range(8, 10).Select(h => (d, h)))
            .First(p => Schedules.RainyHour(p.d, p.h));
        var story = new NoStory();
        story.World.Time = new GameTime(day, hour * 60);
        Assert.NotNull(Schedules.Now("lina", story, hour)!.Home);
        story.World.Time = new GameTime(1, 10 * 60);
        Assert.False(Schedules.RainyHour(1, 10), "the first day of a game stays dry");
        Assert.NotNull(Schedules.Now("lina", story, 10)!.At);
    }

    [Fact]
    public void BlocksOfADay_FollowTheClock()
    {
        var story = new NoStory();
        // Bram: by the well in the day, at the tavern in the evening, at home at night and before dawn.
        Assert.True(Schedules.Now("elder", story, 10)!.At!.Value.X < 0);
        Assert.Equal(ActorPose.Sit, Schedules.Now("elder", story, 19)!.Pose);
        Assert.NotNull(Schedules.Now("elder", story, 23)!.Home);
        Assert.NotNull(Schedules.Now("elder", story, 3)!.Home);
    }

    [Fact]
    public void AtDusk_BramWalksToTheTavernByTheRoads_AndSitsDown()
    {
        var server = Start(17);
        var bram = server.Actor("elder");
        Assert.True(Vector3.Distance(bram.Position, new Vector3(-2, bram.Position.Y, 11)) < 0.5f, "Bram starts the afternoon by the well");

        // An hour of game time is 26 seconds: the clock turns six and he sets off.
        server.Calendar.Now = new GameTime(3, 17 * 60 + 59);
        var walked = 0f;
        var last = bram.Position;
        for (var tick = 0; tick < 26 * Tuning.TickRate * 2 && !(bram.Pose == ActorPose.Sit && bram.Path.Count == 0); tick++)
        {
            server.Step();
            walked += Vector3.Distance(last, bram.Position);
            last = bram.Position;
            Assert.False(Valley.World.OverlapsPlayer(bram.Position with { Y = bram.Position.Y + 0.1f }) && bram.Path.Count > 1,
                $"Bram walks through something at {bram.Position.X:F1}, {bram.Position.Z:F1}");
        }

        Assert.Equal(ActorPose.Sit, bram.Pose);
        Assert.True(bram.Position.X > 20f, "Bram sits in the tavern yard");
        Assert.True(walked > 25f, $"Bram walked {walked:F0} m: the way round the tavern, not through it");
        Assert.True(bram.Active);
    }

    [Fact]
    public void AtNight_PeopleAreIndoors_AndTheWatchIsNot()
    {
        var server = Start(2);
        Assert.False(server.Actor("elder").Active);
        Assert.False(server.Actor("smith").Active);
        Assert.False(server.Actor("lina").Active);
        Assert.True(server.Actor("garrick").Active);
    }

    [Fact]
    public void InTheEvening_TheYardFills()
    {
        var server = Start(21);
        foreach (var id in new[] { "elder", "smith", "garrick", "kai" })
        {
            var who = server.Actor(id);
            Assert.True(who.Active && who.Pose == ActorPose.Sit && who.Position.X is > 20f and < 26f, $"{id} is not in the tavern yard at nine");
        }
    }

    [Fact]
    public void SomeoneFromTheGuildHall_ComesOutByItsDoor()
    {
        // Kai is out all day (indoors, as far as the world knows), then walks from the Guild door to the yard.
        var server = Start(17);
        var kai = server.Actor("kai");
        Assert.False(kai.Active);
        server.Calendar.Now = new GameTime(3, 17 * 60 + 59);
        for (var tick = 0; tick < 40; tick++)
        {
            server.Step();
        }

        Assert.True(kai.Active);
        Assert.True(Vector3.Distance(kai.Position, new Vector3(-36, kai.Position.Y, 25.2f)) < 6f, $"Kai came out at {kai.Position}, not by the Guild door");
    }

    [Fact]
    public void InTheEvening_TheBrightwaterFourShareATable_AndCorwinKeepsHis()
    {
        var server = Start(21);
        foreach (var id in new[] { "edwin", "moss", "fen", "rook", "corwin" })
        {
            var who = server.Actor(id);
            Assert.True(who.Active && who.Pose == ActorPose.Sit && who.Position.X is > 20f and < 26f, $"{id} is not in the tavern yard at nine");
        }

        // Two people never share a seat.
        var seated = Schedules.All.Keys.Select(server.Actor).Where(a => a.Active && a.Pose == ActorPose.Sit).ToArray();
        foreach (var a in seated)
        {
            foreach (var b in seated.Where(b => b != a))
            {
                Assert.True(Vector3.Distance(a.Position, b.Position) > 0.8f, $"{a.Id} and {b.Id} sit on each other");
            }
        }
    }

    /// <summary>A place that is itself inside something: a seat on a bench, Wren's place behind her counter.</summary>
    private static bool Solid(Vector3 spot) => Valley.World.OverlapsPlayer(spot with { Y = Valley.World.GroundHeight(spot.X, spot.Z) + 0.1f });

    [Fact]
    public void EveryWalkOfADay_GoesRoundWhatStandsInTheWay()
    {
        // From each place of someone's day to the next, by the road map: no leg of the way crosses anything solid.
        var world = Valley.World;
        var roads = new RoadMap(world.Roads);
        var blocked = new List<string>();
        foreach (var (id, blocks) in Schedules.All)
        {
            var day = blocks.OrderBy(b => b.FromHour).ToArray();
            for (var i = 0; i < day.Length; i++)
            {
                var from = day[(i + day.Length - 1) % day.Length];
                var to = day[i];
                var start = to.Via is { } door && from.Home is not null ? door : from.Home ?? from.At!.Value;
                var goal = to.Home ?? to.At!.Value;
                if (Vector3.Distance(start, goal) is < 0.5f or > 150f || Math.Abs(start.X) > 300f)
                {
                    // Nowhere to go, a jump between the hall and the village, or a walk inside the hall (no roads, open floor).
                    continue;
                }

                var at = new Vector2(start.X, start.Z);
                var first = true;
                var leaving = from.Home is null ? from.By : Array.Empty<Vector3>();
                foreach (var point in Schedules.Way(roads, world, start, leaving, to.By, goal))
                {
                    var next = new Vector2(point.X, point.Z);
                    // The last step may end on a seat, which is solid, and the whole of it may lie inside their own stall,
                    // where a villager fits and the player's box does not.
                    var last = next == new Vector2(goal.X, goal.Z) && (to.Pose == ActorPose.Sit || Solid(goal));
                    // So may the first begin on one: they get up from the bench they sat on.
                    var seat = MathF.Min(1f, 1.2f / MathF.Max(0.01f, Vector2.Distance(at, next)));
                    var stop = last ? Vector2.Lerp(at, next, to.Pose == ActorPose.Sit ? 1f - seat : 0f) : next;
                    var rise = first && (from.Pose == ActorPose.Sit || Solid(start)) ? Vector2.Lerp(at, next, from.Pose == ActorPose.Sit ? seat : 1f) : at;
                    first = false;
                    if (!RoadMap.Clear(rise, stop, world))
                    {
                        blocked.Add($"{id} at {to.FromHour}:00 between {at.X:F1}, {at.Y:F1} and {next.X:F1}, {next.Y:F1}");
                    }

                    at = next;
                }
            }
        }

        Assert.True(blocked.Count == 0, "People walk through something: " + string.Join("; ", blocked));
    }

    [Fact]
    public void EveryoneInTheValley_HasATemper()
    {
        foreach (var npc in Valley.World.Npcs.Where(n => n.Role is not ("storage" or "quest_board" or "anchor_stone" or "waystone" or "cache")))
        {
            Assert.True(People.Of(npc.Id) is not null, $"{npc.Id} has no entry in world/people.json");
        }
    }
}
