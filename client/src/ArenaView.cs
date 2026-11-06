using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>Builds what the player sees. The arena comes from the same data as its collision.</summary>
public static class ArenaView
{
    public static Node3D Build(World world)
    {
        var root = new Node3D { Name = "Arena" };
        root.AddChild(Box(new Vector3(0, -0.5f, 0), new Vector3(44, 1, 44), new Color("3e8948")));
        foreach (var box in world.Boxes)
        {
            var min = new Vector3(box.Min.X, box.Min.Y, box.Min.Z);
            var max = new Vector3(box.Max.X, box.Max.Y, box.Max.Z);
            root.AddChild(Box((min + max) / 2, max - min, new Color("8b9bb4")));
        }

        return root;
    }

    /// <summary>A figure 1.8 m tall with its origin at the feet. The hat brim shows which way it faces.</summary>
    public static Node3D BuildPlayer(Color tunic)
    {
        var root = new Node3D();
        root.AddChild(Box(new Vector3(0, 0.4f, 0), new Vector3(0.42f, 0.8f, 0.34f), new Color("5a6988")));
        root.AddChild(Box(new Vector3(0, 1.05f, 0), new Vector3(0.6f, 0.6f, 0.42f), tunic));
        root.AddChild(Box(new Vector3(0, 1.1f, 0.3f), new Vector3(0.5f, 0.6f, 0.26f), new Color("733e39")));
        root.AddChild(Box(new Vector3(0, 1.55f, 0), new Vector3(0.46f, 0.42f, 0.46f), new Color("e8b796")));
        root.AddChild(Box(new Vector3(0, 1.8f, -0.05f), new Vector3(0.86f, 0.08f, 0.9f), new Color("e4a672")));
        return root;
    }

    public static Node3D BuildEnemy()
    {
        var root = new Node3D();
        root.AddChild(Box(new Vector3(0, 0.5f, 0), new Vector3(1, 1, 1), new Color("a22633")));
        root.AddChild(Box(new Vector3(-0.2f, 0.72f, -0.5f), new Vector3(0.18f, 0.18f, 0.06f), new Color("fee761")));
        root.AddChild(Box(new Vector3(0.2f, 0.72f, -0.5f), new Vector3(0.18f, 0.18f, 0.06f), new Color("fee761")));
        return root;
    }

    private static MeshInstance3D Box(Vector3 center, Vector3 size, Color color) => new()
    {
        Mesh = new BoxMesh { Size = size },
        Position = center,
        MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 1 },
    };
}
