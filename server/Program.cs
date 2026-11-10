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

    // A new game opens the character creator; test runs and PIXEL_LOOK=default skip it.
    ChosenLook = testMode || Environment.GetEnvironmentVariable("PIXEL_LOOK") == "default",

    // A new game starts with the prologue; PIXEL_SKIP_PROLOGUE=1 starts in the village as before.
    Prologue = save is null && !testMode && Environment.GetEnvironmentVariable("PIXEL_SKIP_PROLOGUE") != "1",
});
// PIXEL_TIME=21 starts a new game at nine in the evening, to check night and dusk.
if (save is null && int.TryParse(Environment.GetEnvironmentVariable("PIXEL_TIME"), out var hour))
{
    server.Calendar.Now = new GameTime(1, Math.Clamp(hour, 0, 23) * 60);
}

// PIXEL_DAY=6 starts a new game on that day, a minute before the hour, so the plans of the days before run at once.
if (save is null && int.TryParse(Environment.GetEnvironmentVariable("PIXEL_DAY"), out var day))
{
    server.Calendar.Now = new GameTime(Math.Max(1, day), server.Calendar.Now.Minute).AddMinutes(-1);
}

// PIXEL_FLAGS=prologue.started,prologue.free sets story flags on a new game, to start a check partway through the story.
if (save is null && Environment.GetEnvironmentVariable("PIXEL_FLAGS") is { Length: > 0 } flags)
{
    foreach (var flag in flags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        server.StoryState.SetFlag(flag);
    }
}

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

// A server the game's main menu started (PIXEL_PARENT = the game's process id) stops when the player
// quits, after the save, and when the game is gone, so no server is left running after a crash.
Process? parent = int.TryParse(Environment.GetEnvironmentVariable("PIXEL_PARENT"), out var parentId)
    ? Process.GetProcesses().FirstOrDefault(p => p.Id == parentId)
    : null;

var clock = Stopwatch.StartNew();
var tickLength = TimeSpan.FromSeconds(Tuning.TickSeconds);
var nextTick = clock.Elapsed;
while (true)
{
    server.Step();
    if (parent is not null && (server.QuitRequested || server.Tick % (uint)Tuning.TickRate == 0 && parent.HasExited))
    {
        if (!server.QuitRequested)
        {
            // The game closed without quitting (a crash): keep what was played.
            server.Autosave?.Invoke(server.Export());
        }

        Console.WriteLine(server.QuitRequested ? "The player quit; the game is saved. Stopping." : "The game is gone; saved. Stopping.");
        break;
    }

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
