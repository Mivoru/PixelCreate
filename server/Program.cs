using System.Diagnostics;
using PixelCreate.Core;
using PixelCreate.Transport;

var port = args.Length > 0 ? int.Parse(args[0]) : 7777;
using var transport = UdpTransport.Listen(port);
var zone = Zone.Load("meadow-valley");
var server = new GameServer(transport, zone.World, new ServerOptions());
Console.WriteLine($"Zone {zone.Name}. Listening on UDP {port} at {Tuning.TickRate} Hz.");

var clock = Stopwatch.StartNew();
var tickLength = TimeSpan.FromSeconds(Tuning.TickSeconds);
var nextTick = clock.Elapsed;
while (true)
{
    server.Step();
    if (server.Tick % (uint)(Tuning.TickRate * 10) == 0)
    {
        var stats = server.Stats;
        Console.WriteLine($"tick {server.Tick}: missed inputs {stats.MissedInputs}, hits {stats.PlayerHits}, kills {stats.EnemiesKilled}");
    }

    nextTick += tickLength;
    var wait = nextTick - clock.Elapsed;
    if (wait > TimeSpan.Zero)
    {
        Thread.Sleep(wait);
    }
}
