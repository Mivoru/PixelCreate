using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Godot;

namespace PixelCreate;

/// <summary>
/// How the game connects: to the server the main menu started for a save, or to the host and port
/// given on the command line (scripts, bots, a server elsewhere).
/// </summary>
public static class Launch
{
    public static string Host { get; set; } = "127.0.0.1";
    public static int Port { get; set; } = 7777;

    /// <summary>Set when the main menu started the game: Esc then offers to save and go back to it.</summary>
    public static bool FromMenu { get; set; }

    /// <summary>The save slot playing, when the menu started the game.</summary>
    public static string Slot { get; set; } = "";
}

/// <summary>
/// The game's own server, started by the main menu for one save slot. The server stays a process of
/// its own, as it always was: it owns the save and could take a second player later. It stops itself
/// after the quit save, or when this game's process is gone (PIXEL_PARENT).
/// </summary>
public static class LocalServer
{
    private static Process? _process;
    private static readonly object Lock = new();

    /// <summary>Where the server's lines go: the export previews folder in the repository, else the user folder.</summary>
    private static string LogPath
    {
        get
        {
            var previews = Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "export", "previews");
            return Directory.Exists(previews) ? Path.Combine(previews, "server.log") : ProjectSettings.GlobalizePath("user://server.log");
        }
    }

    /// <summary>The server program: PIXEL_SERVER_EXE (play.ps1 sets it), else the Release or Debug build in the repository, else beside the game.</summary>
    public static string? ExecutablePath()
    {
        var candidates = new[]
        {
            OS.GetEnvironment("PIXEL_SERVER_EXE"),
            Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "server", "bin", "Release", "net8.0", "Server.exe"),
            Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "server", "bin", "Debug", "net8.0", "Server.exe"),
            Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath()) ?? "", "Server.exe"),
        };
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    /// <summary>
    /// Starts the server for a slot and waits until it listens. Returns an error to show, or null.
    /// A slot with no save file starts a new game there.
    /// </summary>
    public static async Task<string?> Start(string slot, int port)
    {
        Stop();
        var path = ExecutablePath();
        if (path is null)
        {
            return "The game server was not found (Server.exe).";
        }

        var info = new ProcessStartInfo(path, port.ToString())
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(path)!,
        };
        info.Environment["PIXEL_SLOT"] = slot;
        info.Environment["PIXEL_PARENT"] = OS.GetProcessId().ToString();

        // A test run or a bot from the shell must not leak into a game started here.
        foreach (var name in new[] { "PIXEL_TEST", "PIXEL_SPAWN", "PIXEL_FLAGS", "PIXEL_DAY", "PIXEL_TIME", "PIXEL_FRAIL", "PIXEL_LOOK", "PIXEL_SKIP_PROLOGUE" })
        {
            info.Environment.Remove(name);
        }

        var listening = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        // Each line is appended on its own: lines may still arrive after the process has exited.
        var logPath = LogPath;
        File.WriteAllText(logPath, "");
        void Write(string line)
        {
            lock (Lock)
            {
                try
                {
                    File.AppendAllText(logPath, line + System.Environment.NewLine);
                }
                catch (IOException)
                {
                    // A log line is not worth failing the game for.
                }
            }
        }

        var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            Write(e.Data);
            if (e.Data.Contains("Listening on UDP", StringComparison.Ordinal))
            {
                listening.TrySetResult(true);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                Write(e.Data);
            }
        };
        process.Exited += (_, _) => listening.TrySetResult(false);

        try
        {
            process.Start();
        }
        catch (Exception error)
        {
            return $"The game server did not start: {error.Message}";
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _process = process;
        var finished = await Task.WhenAny(listening.Task, Task.Delay(TimeSpan.FromSeconds(120)));
        if (finished != listening.Task || !listening.Task.Result)
        {
            Stop();
            return "The game server stopped while loading. See server.log.";
        }

        return null;
    }

    /// <summary>Waits for the server to stop by itself (after the quit save), then makes sure it has.</summary>
    public static async Task StopAfterSave(int milliseconds = 4000)
    {
        if (_process is { } process)
        {
            var waited = 0;
            while (!process.HasExited && waited < milliseconds)
            {
                await Task.Delay(50);
                waited += 50;
            }
        }

        Stop();
    }

    /// <summary>Stops the server at once if it still runs.</summary>
    public static void Stop()
    {
        if (_process is { } process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (InvalidOperationException)
            {
                // Already gone.
            }

            process.Dispose();
            _process = null;
        }
    }
}
