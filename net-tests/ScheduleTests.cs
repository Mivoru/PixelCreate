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
}
