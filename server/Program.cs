using System.Diagnostics;
using PixelCreate.Core;
using PixelCreate.Transport;

var port = args.Length > 0 ? int.Parse(args[0]) : 7777;
// The zone is built before listening, so no client waits on a server that is still loading.
var zone = Zone.Load("meadow-valley");
using var transport = UdpTransport.Listen(port);
var spawn = Environment.GetEnvironmentVariable("PIXEL_SPAWN")?.Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
var testMode = Environment.GetEnvironmentVariable("PIXEL_TEST") == "1";

// The game saves to a slot each morning and when the player quits. Test runs neither load nor save.
var slot = Environment.GetEnvironmentVariable("PIXEL_SLOT") is { Length: > 0 } named ? named : "main";
var save = testMode ? null : SaveFile.Read(slot);
var server = new GameServer(transport, zone.World, new ServerOptions
{
    SpawnOverride = spawn is null ? null : new System.Numerics.Vector3(spawn[0], 0, spawn[1]),
    TestMode = testMode,
    FrailExaminer = Environment.GetEnvironmentVariable("PIXEL_FRAIL") == "1",
    Save = save,
});
if (!testMode)
{
    server.Autosave = data =>
    {
        SaveFile.Write(slot, data);
        Console.WriteLine($"Saved {SaveFile.PathFor(slot)} ({server.Calendar.Now}).");
    };
}

Console.WriteLine(save is null
    ? $"Zone {zone.Name}. New game in slot {slot}. Listening on UDP {port} at {Tuning.TickRate} Hz."
    : $"Zone {zone.Name}. Continuing slot {slot} on {server.Calendar.Now}. Listening on UDP {port} at {Tuning.TickRate} Hz.");

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
