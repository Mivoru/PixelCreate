using System.Numerics;
using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>Groups fighting one player on an empty field: spacing, turns and roles.</summary>
public class TacticsTests
{
    private static (GameServer Server, ClientPrediction Client, SimNetwork Network) Start(params EnemySpawn[] camps)
    {
        var network = new SimNetwork(1, 0, 0, 0);
        var world = new World(Array.Empty<Aabb>(), null, Vector3.Zero, camps);
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 11 });
        var client = new ClientPrediction(network.Connect(1), world);
        return (server, client, network);
    }

    private static EnemySpawn Camp(string id, int count, float distance, float radius = 2f) =>
        new(new Vector3(0, 0, distance), radius, count, Types: new[] { id });

    private static void Play(GameServer server, ClientPrediction client, SimNetwork network, int ticks, Action? each = null)
    {
        for (var i = 0; i < ticks; i++)
        {
            network.Advance(1000.0 / Tuning.TickRate);
            server.Step();
            client.Step(0, 0, Buttons.None);
            each?.Invoke();
        }
    }

    private static Vector3 Player(GameServer server) => server.GetPlayer(1)?.Position ?? Vector3.Zero;

    [Fact]
    public void Group_SpreadsInsteadOfPiling()
    {
        var (server, client, network) = Start(Camp("goblin_brute", 5, 8f, 1f));
        Play(server, client, network, 60 * 6);
        var alive = server.Enemies.Where(e => e.Alive).ToList();
        var closest = alive.SelectMany(a => alive.Where(b => b != a).Select(b => Vector3.Distance(a.Position with { Y = 0 }, b.Position with { Y = 0 }))).Min();
        Assert.True(closest > 1.0f, $"Fighters must keep apart; two stood {closest:F2} m from each other.");
    }

    [Fact]
    public void OnlyTwoComeInCloseAtOnce_AndTheyTakeTurns()
    {
        var (server, client, network) = Start(Camp("goblin_brute", 6, 7f, 2f));
        var most = 0;
        var striking = 0;
        var hadTurns = new HashSet<int>();
        Play(server, client, network, 60 * 25, () =>
        {
            most = Math.Max(most, server.CloseAttackers(1));
            var now = server.Enemies.Count(e => e.Alive && e.Action is EnemyAction.Cast or EnemyAction.Strike && e.CurrentAbility is { } a && Enemy.IsClose(a));
            striking = Math.Max(striking, now);
            foreach (var e in server.Enemies.Where(e => e.HasTurn))
            {
                hadTurns.Add(e.Id);
            }
        });

        Assert.InRange(most, 1, GameServer.MaxCloseAttackers);
        Assert.InRange(striking, 1, GameServer.MaxCloseAttackers);
        Assert.True(hadTurns.Count >= 4, $"Fighters must swap in; only {hadTurns.Count} had a turn.");
    }

    [Fact]
    public void Skirmisher_FallsBackAfterABlow()
    {
        var (server, client, network) = Start(Camp("goblin_skirmisher", 1, 5f, 0.5f));
        var goblin = server.Enemies[0];
        var last = goblin.Action;
        var struck = false;
        for (var i = 0; i < 60 * 15 && !struck; i++)
        {
            Play(server, client, network, 1);
            struck = last == EnemyAction.Strike && goblin.Action != EnemyAction.Strike;
            last = goblin.Action;
        }

        Assert.True(struck, "The skirmisher must strike.");
        var farthest = 0f;
        Play(server, client, network, 60, () => farthest = MathF.Max(farthest, Vector3.Distance(goblin.Position with { Y = 0 }, Player(server) with { Y = 0 })));
        Assert.True(farthest > 3.5f, $"A skirmisher must fall back after a blow; it got {farthest:F2} m away.");
    }

    [Fact]
    public void Pack_SurroundsThePlayer()
    {
        var (server, client, network) = Start(Camp("wolf", 5, 9f, 2f));
        var widest = 0f;
        Play(server, client, network, 60 * 12, () =>
        {
            var player = Player(server);
            var angles = server.Enemies.Where(e => e.Alive && Vector3.Distance(e.Position, player) < 9f)
                .Select(e => MathF.Atan2(e.Position.X - player.X, e.Position.Z - player.Z)).OrderBy(a => a).ToList();
            if (angles.Count < 4)
            {
                return;
            }

            // The largest empty arc round the player: small when the pack surrounds them.
            var gap = angles.Zip(angles.Skip(1), (a, b) => b - a).Append(angles[0] + MathF.Tau - angles[^1]).Max();
            widest = MathF.Max(widest, MathF.Tau - gap);
        });

        Assert.True(widest > MathF.PI, $"The pack must spread round the player; it covered {widest * 180 / MathF.PI:F0} degrees at most.");
    }

    [Fact]
    public void Support_StaysBehindItsFrontLine()
    {
        var (server, client, network) = Start(Camp("goblin_brute", 1, 6f, 0.5f), Camp("goblin_shaman", 1, 12f, 0.5f));
        var behind = 0;
        var samples = 0;
        Play(server, client, network, 60 * 8);
        Play(server, client, network, 60 * 6, () =>
        {
            var player = Player(server);
            var brute = server.Enemies.FirstOrDefault(e => e.Def.Id == "goblin_brute" && e.Alive);
            var shaman = server.Enemies.First(e => e.Def.Id == "goblin_shaman");
            if (brute is null || !shaman.Alive)
            {
                return;
            }

            samples++;
            behind += Vector3.Distance(shaman.Position, player) > Vector3.Distance(brute.Position, player) ? 1 : 0;
        });

        Assert.True(samples > 0 && behind > samples * 0.8f, $"The shaman must keep behind the brute; it did {behind} of {samples} ticks.");
    }

    [Fact]
    public void Patrol_WalksItsRouteAndBack()
    {
        // No player near: the patrol walks out to the far point, rests, and comes back.
        var route = new[] { new Vector3(40, 0, 60), new Vector3(80, 0, 60) };
        var camp = new EnemySpawn(new Vector3(0, 0, 60), Patrol.PointRadius, 2, Types: new[] { "goblin_skirmisher" }, Route: route);
        var network = new SimNetwork(1, 0, 0, 0);
        var world = new World(Array.Empty<Aabb>(), null, new Vector3(0, 0, -200), new[] { camp });
        var server = new GameServer(network.Connect(0), world, new ServerOptions { Seed = 3 });
        var farthest = 0f;
        var back = false;
        for (var i = 0; i < 60 * 120 && !back; i++)
        {
            server.Step();
            var x = server.Enemies.Average(e => e.Position.X);
            farthest = MathF.Max(farthest, x);
            back = farthest > 75f && x < 8f;
        }

        Assert.True(farthest > 75f, $"The patrol must reach the far point; it got to x {farthest:F0}.");
        Assert.True(back, "The patrol must turn round and walk back to its start.");
    }

    [Fact]
    public void PackLeader_CallsARush()
    {
        var (server, client, network) = Start(Camp("alpha_wolf", 1, 8f, 0.5f), Camp("wolf", 5, 9f, 2f));
        var most = 0;
        Play(server, client, network, 60 * 6, () => most = Math.Max(most, server.CloseAttackers(1)));
        Assert.True(most > GameServer.MaxCloseAttackers, $"In the leader's rush more wolves must come in at once; at most {most} did.");
    }
}
