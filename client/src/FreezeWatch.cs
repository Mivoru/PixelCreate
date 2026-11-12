using System;
using System.Diagnostics;
using System.Threading;

namespace PixelCreate;

/// <summary>
/// Watches the main thread for freezes. The game marks where it is in each frame (<see cref="Mark"/>, a plain string
/// write, so it costs nothing), and a background thread notices when no frame has begun for a few seconds. It then
/// writes the last mark to the error output, which the scripts send to export/previews/client.log, and writes again
/// when the game moves on. A freeze once hit the Guild hall and was never pinned down: this says where the next one
/// sits, or that it sits outside the game's own frame (rendering, the engine, another node) when the mark is "end".
/// </summary>
public static class FreezeWatch
{
    /// <summary>A frame that has not finished after this long is a freeze worth writing down.</summary>
    private const int StuckMs = 4000;

    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static long _beatMs;
    private static long _frames;
    private static volatile string _phase = "start";
    private static Thread? _thread;

    /// <summary>Where the main thread is now, for the report if it never gets further.</summary>
    public static void Mark(string phase) => _phase = phase;

    /// <summary>
    /// A new frame of the game begins: the game is alive. The first one starts the watch, so the minute of
    /// loading before it (the server building the zone) is not taken for a freeze.
    /// </summary>
    public static void Beat(string phase)
    {
        Interlocked.Exchange(ref _beatMs, Clock.ElapsedMilliseconds);
        Interlocked.Increment(ref _frames);
        _phase = phase;
        if (_thread is null)
        {
            Start();
        }
    }

    private static void Start()
    {
        _thread = new Thread(Watch) { IsBackground = true, Name = "FreezeWatch", Priority = ThreadPriority.BelowNormal };
        _thread.Start();
    }

    private static void Watch()
    {
        var reported = false;
        var stuckSince = 0L;
        var stuckPhase = "";
        while (true)
        {
            Thread.Sleep(500);
            var now = Clock.ElapsedMilliseconds;
            var beat = Interlocked.Read(ref _beatMs);
            if (now - beat > StuckMs)
            {
                if (!reported)
                {
                    reported = true;
                    stuckSince = beat;
                    stuckPhase = _phase;
                    Console.Error.WriteLine($"FREEZE the main thread has not begun a frame for {(now - beat) / 1000f:F1} s; last mark: {stuckPhase} (frame {Interlocked.Read(ref _frames)}, {DateTime.Now:HH:mm:ss})");
                    Console.Error.Flush();
                }
            }
            else if (reported)
            {
                reported = false;
                Console.Error.WriteLine($"FREEZE over after {(beat - stuckSince) / 1000f:F1} s (it was stuck at: {stuckPhase})");
                Console.Error.Flush();
            }
        }
    }
}
