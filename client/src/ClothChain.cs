using System.Collections.Generic;
using Godot;

namespace PixelCreate;

/// <summary>How a piece of cloth or hair hangs and reacts.</summary>
public sealed record ClothShape(
    float Length, float HalfWidth, float BackAtTop, float BackAtEnd, float Drag, float Gravity, float Stiffness,
    float BodyWidth, float BodyDepth, float BackBias = 0f);

/// <summary>
/// Cloth and hair as a small grid of Verlet points. The grid lives in a frame that moves with the
/// character but does not turn with it, so running, turning, jumping and stopping all pull on it
/// through inertia and air drag. The vertex stage of toon.gdshader bends the mesh along the grid.
/// </summary>
public sealed class ClothChain
{
    public const int Columns = 3;
    public const int Rows = 6;

    private const float Substep = 1f / 120f;
    private const int MaxSubsteps = 5;
    private const int Relaxations = 4;

    /// <summary>Limits how fast the felt speed changes, so an instant stop does not whip the cloth.</summary>
    private const float MaxAcceleration = 26f;

    private readonly ClothShape _shape;
    private readonly Node3D _anchor;
    private readonly Node3D _root;
    private readonly Vector3[] _rest = new Vector3[Columns * Rows];
    private readonly Vector3[] _now = new Vector3[Columns * Rows];
    private readonly Vector3[] _old = new Vector3[Columns * Rows];
    private readonly Vector3[] _offsets = new Vector3[Columns * Rows];
    private readonly float _segment;
    private readonly float _span;
    private Vector3 _lastVelocity;
    private float _time;
    private float _leftover;
    private bool _started;

    public List<ShaderMaterial> Materials { get; } = new();

    public ClothChain(ClothShape shape, Node3D anchor, Node3D root)
    {
        _shape = shape;
        _anchor = anchor;
        _root = root;
        _segment = shape.Length / (Rows - 1);
        _span = shape.HalfWidth;
        for (var row = 0; row < Rows; row++)
        {
            var t = row / (float)(Rows - 1);
            for (var column = 0; column < Columns; column++)
            {
                var side = column - 1;
                var back = Mathf.Lerp(shape.BackAtTop, shape.BackAtEnd, t) * (side == 0 ? 1f : 0.55f);
                _rest[Index(column, row)] = new Vector3(side * shape.HalfWidth, -shape.Length * t, back);
            }
        }
    }

    private static int Index(int column, int row) => column * Rows + row;

    /// <summary>Forgets all motion, for when a character is placed rather than moved.</summary>
    public void Reset() => _started = false;

    /// <summary>Moves the points on and hands the bend to the shader. The velocity is the body's own.</summary>
    public void Step(Vector3 bodyVelocity, float delta)
    {
        if (!_anchor.IsInsideTree())
        {
            return;
        }

        var frame = _anchor.GlobalTransform;
        frame.Origin -= _root.GlobalPosition;
        if (!_started)
        {
            Settle(frame);
            _lastVelocity = bodyVelocity;
            _started = true;
        }

        _leftover += delta;
        var steps = Mathf.Min(Mathf.FloorToInt(_leftover / Substep), MaxSubsteps);
        _leftover = Mathf.Min(_leftover - steps * Substep, Substep);
        for (var i = 0; i < steps; i++)
        {
            var velocityStep = (bodyVelocity - _lastVelocity).LimitLength(MaxAcceleration * Substep);
            _lastVelocity += velocityStep;
            Advance(frame, _lastVelocity, velocityStep);
        }

        Publish(frame);
    }

    private void Settle(Transform3D frame)
    {
        for (var i = 0; i < _now.Length; i++)
        {
            _now[i] = frame * _rest[i];
            _old[i] = _now[i];
        }
    }

    private void Advance(Transform3D frame, Vector3 velocity, Vector3 velocityStep)
    {
        _time += Substep;
        var inverse = frame.AffineInverse();
        for (var column = 0; column < Columns; column++)
        {
            for (var row = 1; row < Rows; row++)
            {
                var i = Index(column, row);
                var moving = (_now[i] - _old[i]) / Substep - velocityStep;
                var breeze = new Vector3(
                    Mathf.Sin(_time * 2.3f + row * 0.9f + column * 1.7f),
                    0f,
                    Mathf.Sin(_time * 3.1f + row * 1.3f - column)) * (0.35f + velocity.Length() * 0.14f);
                var air = (breeze - velocity - moving) * _shape.Drag;
                air.Y *= 0.3f;
                var force = new Vector3(0, -_shape.Gravity, 0) + air;
                _old[i] = _now[i];
                _now[i] += (moving * 0.995f + force * Substep) * Substep;

                // Cloth has some body of its own: it leans back towards the shape it was cut in.
                _now[i] += (frame * _rest[i] - _now[i]) * _shape.Stiffness;
            }

            _now[Index(column, 0)] = frame * _rest[Index(column, 0)];
            _old[Index(column, 0)] = _now[Index(column, 0)];
        }

        for (var pass = 0; pass < Relaxations; pass++)
        {
            for (var column = 0; column < Columns; column++)
            {
                for (var row = 1; row < Rows; row++)
                {
                    Tie(Index(column, row - 1), Index(column, row), _segment, row == 1 ? 0f : 0.5f);
                }
            }

            for (var row = 1; row < Rows; row++)
            {
                Tie(Index(0, row), Index(1, row), _span, 0.5f, 0.6f);
                Tie(Index(1, row), Index(2, row), _span, 0.5f, 0.6f);
            }
        }

        for (var i = 0; i < _now.Length; i++)
        {
            if (i % Rows != 0)
            {
                _now[i] = Collide(_now[i], frame, inverse, i % Rows);
            }
        }
    }

    /// <summary>Pulls two points to a distance. Share is how much of the move the first point takes.</summary>
    private void Tie(int a, int b, float length, float share, float strength = 1f)
    {
        var gap = _now[b] - _now[a];
        var distance = gap.Length();
        if (distance < 0.0001f)
        {
            return;
        }

        var error = gap * ((distance - length) / distance) * strength;
        _now[a] += error * share;
        _now[b] -= error * (1f - share);
    }

    /// <summary>
    /// Keeps a point outside the body, which is an upright ellipse around the anchor, and above the
    /// ground; and keeps it hanging: a point may swing out, but never rise above 45 % of its
    /// drop below the anchor, so hair cannot fly up over the head when the body lands.
    /// </summary>
    private Vector3 Collide(Vector3 point, Transform3D frame, Transform3D inverse, int row)
    {
        var local = inverse * point;
        local.Y = Mathf.Min(local.Y, -_shape.Length * row / (Rows - 1) * 0.55f);
        var reach = new Vector2(local.X / _shape.BodyWidth, local.Z / _shape.BodyDepth).Length();
        if (reach < 1f)
        {
            if (reach < 0.001f)
            {
                local.Z = _shape.BodyDepth;
            }
            else
            {
                local.X /= reach;
                local.Z /= reach;
            }

            point = frame * local;
        }

        // The frame moves with the character, whose feet are at y = 0.
        point.Y = Mathf.Max(point.Y, 0.04f);
        return point;
    }

    private void Publish(Transform3D frame)
    {
        var inverse = frame.AffineInverse();
        for (var i = 0; i < _now.Length; i++)
        {
            _offsets[i] = inverse * _now[i] - _rest[i];
        }

        foreach (var material in Materials)
        {
            material.SetShaderParameter("chain", _offsets);
            material.SetShaderParameter("chain_on", 1f);
            material.SetShaderParameter("chain_width", _shape.HalfWidth);
            material.SetShaderParameter("back_bias", _shape.BackBias);
        }
    }
}
