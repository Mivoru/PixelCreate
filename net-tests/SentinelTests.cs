using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>
/// The Auren sentinels on an empty field: they stand as statues until someone comes close, their front
/// is armoured and their back is not, and the Archive Warden's rune wave runs in lanes with gaps to stand in.
/// </summary>
public class SentinelTests
{
    /// <summary>The way the test camps face: towards the player, who starts at the origin.</summary>
    private const float TowardPlayer = 0f;

    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start(string id, float distance)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        // The camp lies on +Z of the player, and facing 0 looks along -Z: back at the player.
        var camp = new EnemySpawn(new Vector3(0, 0, distance), 0.3f, 1, Types: new[] { id }, Facing: TowardPlayer);
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, new[] { camp });
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 5 });
        var client = new ClientPrediction(network.Connect(1), world);
        return (server, client, network);
    }

    private static void Play(GameServer server, ClientPrediction client, SimNetwork network, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, Buttons.None);
        }
    }

    [Fact]
    public void Sentinel_StandsLikeAStatue_UntilSomeoneComesClose()
    {
        var (server, client, network) = Start("auren_sentinel", 12f);
        var statue = server.Enemies[0];
        var post = statue.Position;
        Play(server, client, network, Tuning.TickRate * 10);
        Assert.True(Vector3.Distance(post, statue.Position) < 0.05f, $"the statue wandered from {post} to {statue.Position}");
        Assert.True(statue.TargetId < 0, "a statue twelve metres off must not notice the player");
        Assert.Equal(TowardPlayer, statue.Facing, 3);
        Assert.Equal(0, server.Stats.PlayerHits);
    }

    [Fact]
    public void Sentinel_Wakes_AndStrikes_APlayerWhoComesClose()
    {
        var (server, client, network) = Start("auren_sentinel", 4f);
        Play(server, client, network, Tuning.TickRate * 15);
        Assert.True(server.Enemies[0].TargetId >= 0, "the sentinel never woke for a player four metres away");
        Assert.True(server.Stats.PlayerHits > 0, "the sentinel woke but never struck the player standing by it");
    }

    [Fact]
    public void Sentinel_TakesLittleFromTheFront_AndMoreFromBehind()
    {
        var (server, _, _) = Start("auren_sentinel", 12f);
        var statue = server.Enemies[0];
        var forward = new Vector3(-MathF.Sin(statue.Facing), 0, -MathF.Cos(statue.Facing));
        var full = statue.Health;

        statue.TakeHit(100, statue.Position + forward * 2f);
        var front = full - statue.Health;
        statue.Health = full;
        statue.TakeHit(100, statue.Position - forward * 2f);
        var back = full - statue.Health;
        statue.Health = full;
        var across = new Vector3(-forward.Z, 0, forward.X);
        statue.TakeHit(100, statue.Position + across * 2f);
        var flank = full - statue.Health;

        Assert.Equal(30, front);
        Assert.Equal(150, back);
        Assert.Equal(100, flank);
    }

    [Fact]
    public void Sentinel_DoesNotReel_OrSlide_WhenStruck()
    {
        var (server, _, _) = Start("auren_sentinel", 12f);
        var statue = server.Enemies[0];
        statue.TakeHit(10, statue.Position + Vector3.UnitX * 2f);
        Assert.Equal(Vector3.Zero, statue.Velocity with { Y = 0 });
    }

    [Fact]
    public void RuneWave_LanesLeaveGapsAPlayerFitsIn()
    {
        var gap = RuneWave.Spacing - RuneWave.HalfWidth * 2f;
        Assert.True(gap >= 1.6f, $"a gap of {gap} m is too narrow to stand in");
        Assert.Equal(-RuneWave.Spacing, RuneWave.Offset(0), 3);
        Assert.Equal(0f, RuneWave.Offset(1), 3);
        Assert.Equal(RuneWave.Spacing, RuneWave.Offset(2), 3);
    }

    [Fact]
    public void ArchiveWarden_SendsItsRuneWave_InThreeLanes()
    {
        var warden = EnemyDef.All.Single(d => d.Id == "archive_warden");
        Assert.True(warden.Rare);
        var wave = warden.Abilities!.Single(a => a.Effect == "runeWave");
        Assert.Equal("lanes", wave.Telegraph);
        Assert.True(wave.WindupTicks >= 60, "the wave covers a wide area, so it needs a long warning");

        // Seven metres off: inside the wave's reach, beyond its blade, and close enough to wake it.
        var (server, client, network) = Start("archive_warden", 6.5f);
        var waved = false;
        for (var i = 0; i < Tuning.TickRate * 30 && !waved; i++)
        {
            Play(server, client, network, 1);
            waved = server.FireWallCount == RuneWave.Lanes;
        }

        Assert.True(waved, "the Archive Warden never sent its rune wave at a player in reach");
    }
}
