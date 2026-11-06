using Godot;

namespace PixelCreate;

/// <summary>Placeholder figures for players and enemies.</summary>
public static class ArenaView
{
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
