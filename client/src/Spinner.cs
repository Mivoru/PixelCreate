using Godot;

namespace PixelCreate;

/// <summary>Turns its parent slowly around the parent's own forward axis, like mill sails in a breeze.</summary>
public partial class Spinner : Node
{
    /// <summary>Turns per second, in radians.</summary>
    public float Speed { get; set; } = 0.55f;

    public override void _Process(double delta)
    {
        if (GetParent() is Node3D target)
        {
            target.RotateObjectLocal(Vector3.Back, Speed * (float)delta);
        }
    }
}
