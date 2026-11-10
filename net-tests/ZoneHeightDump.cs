using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>
/// Writes the real ground heights of Meadow Valley (one metre a cell, float32, row by row from the
/// north-west corner) to export/previews/zone-height.bin, for tools/zone_view.py to draw. It runs only
/// when PIXEL_DUMP=1, so the normal test run stays quick.
/// </summary>
public class ZoneHeightDump
{
    [Fact]
    public void DumpHeights()
    {
        if (Environment.GetEnvironmentVariable("PIXEL_DUMP") != "1")
        {
            return;
        }

        var zone = Zone.Load("meadow-valley");
        var size = (int)zone.Size;
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var path = Path.Combine(root, "export", "previews", "zone-height.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(size);
        for (var z = 0; z < size; z++)
        {
            for (var x = 0; x < size; x++)
            {
                var wx = x - size / 2f + 0.5f;
                var wz = z - size / 2f + 0.5f;
                writer.Write(zone.World.GroundHeight(wx, wz));
            }
        }
    }
}
