using System.Collections.Generic;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>
/// A face built from small flat discs on the head: eyes with lashes and lids, brows, a nose, a set
/// of mouth shapes, blush, a tear and a drop of sweat. Each frame it eases towards an expression
/// from world/faces.json, blinks now and then, and moves the mouth while the character talks.
/// The face looks along -Z of the head.
/// </summary>
public sealed class FaceRig
{
    private const float EyeHalf = 0.031f;

    private sealed class Eye
    {
        public required float Side;
        public required Node3D Root;
        public required Node3D Ball;
        public required Node3D Iris;
        public required Node3D Lash;
        public required Node3D Happy;
        public required Node3D Brow;
        public required Vector3 BrowBase;
    }

    private readonly List<Eye> _eyes = new();
    private readonly Dictionary<string, Node3D> _mouths = new();
    private readonly List<Node3D> _blush = new();
    private readonly Node3D _tear;
    private readonly Node3D _sweat;
    private readonly bool _feminine;

    // The face as it is now, easing towards the preset.
    private float _browRaise;
    private float _browTilt;
    private float _lidUpper;
    private float _lidLower;
    private float _wide;
    private string _mouth = "line";
    private float _blink = 3f;
    private float _talk;
    private FacePreset _target = Faces.Neutral;
    private string _expression = "neutral";
    private string? _flash;
    private float _flashTime;

    /// <summary>Where the eyes look, from -1 to 1 on each axis: right and up are positive.</summary>
    public Vector2 Gaze { get; set; }

    /// <summary>Off for photographs, so no picture catches a blink.</summary>
    public bool Blinks { get; set; } = true;

    /// <summary>Set while the character speaks: the mouth opens and closes.</summary>
    public bool Talking { get; set; }

    /// <summary>The expression the face holds, by name in world/faces.json.</summary>
    public string Expression
    {
        get => _expression;
        set
        {
            _expression = Faces.All.ContainsKey(value) ? value : "neutral";
            _target = Faces.Get(_expression);
        }
    }

    /// <summary>Shows an expression for a moment, then goes back to <see cref="Expression"/>: pain on a hit, effort on a heavy blow.</summary>
    public void Flash(string expression, float seconds)
    {
        _flash = expression;
        _flashTime = seconds;
    }

    public FaceRig(Node3D head, Color skin, Color eyes, Color brows, bool feminine)
    {
        _feminine = feminine;
        var lash = new Color("1c1a24");
        foreach (var side in new[] { -1f, 1f })
        {
            var root = new Node3D { Position = new Vector3(side * 0.057f, -0.018f, -0.118f), Rotation = new Vector3(0, side * -0.32f, 0) };
            var ball = new Node3D();
            ball.AddChild(Flat(new Vector3(0.03f, EyeHalf, 0.006f), new Color("f8f4ee"), Vector3.Zero));
            var iris = new Node3D();
            iris.AddChild(Flat(new Vector3(0.024f, 0.03f, 0.006f), eyes, new Vector3(side * -0.002f, -0.002f, -0.005f)));
            iris.AddChild(Flat(new Vector3(0.024f, 0.012f, 0.0055f), eyes.Darkened(0.4f), new Vector3(side * -0.002f, 0.014f, -0.0065f)));
            iris.AddChild(Flat(new Vector3(0.012f, 0.017f, 0.005f), new Color("15202c"), new Vector3(side * -0.002f, 0.001f, -0.009f)));
            iris.AddChild(Flat(new Vector3(0.0085f, 0.0085f, 0.004f), Colors.White, new Vector3(side * 0.008f, 0.012f, -0.0125f)));
            iris.AddChild(Flat(new Vector3(0.0045f, 0.0045f, 0.003f), Colors.White, new Vector3(side * -0.009f, -0.014f, -0.0125f)));
            ball.AddChild(iris);
            root.AddChild(ball);

            // The lash rides on the upper lid, so it follows the lid down when the eye closes.
            var lashNode = new Node3D();
            var lashBar = Flat(new Vector3(0.037f, 0.0075f, 0.008f), lash, new Vector3(side * 0.002f, 0f, -0.006f));
            lashBar.Rotation = new Vector3(0, 0, side * 0.07f);
            lashNode.AddChild(lashBar);
            if (feminine)
            {
                var wing = Flat(new Vector3(0.012f, 0.004f, 0.006f), lash, new Vector3(side * 0.037f, 0f, -0.003f));
                wing.Rotation = new Vector3(0, 0, side * 0.6f);
                lashNode.AddChild(wing);
            }

            root.AddChild(lashNode);

            // A laughing eye is a closed arc: two short strokes meeting at the top.
            var happy = new Node3D { Visible = false };
            foreach (var half in new[] { -1f, 1f })
            {
                var stroke = Flat(new Vector3(0.019f, 0.0055f, 0.007f), lash, new Vector3(half * 0.014f, 0.004f, -0.008f));
                stroke.Rotation = new Vector3(0, 0, -half * 0.45f);
                happy.AddChild(stroke);
            }

            root.AddChild(happy);
            head.AddChild(root);

            // Brows sit in front of the fringe, as anime draws them over the hair.
            var browBase = new Vector3(side * 0.06f, 0.056f, -0.158f);
            var brow = Flat(new Vector3(0.03f, feminine ? 0.0035f : 0.0055f, 0.004f), brows, browBase);
            head.AddChild(brow);
            _eyes.Add(new Eye { Side = side, Root = root, Ball = ball, Iris = iris, Lash = lashNode, Happy = happy, Brow = brow, BrowBase = browBase });

            var blush = Flat(new Vector3(0.026f, 0.012f, 0.004f), new Color("f4a0a0"), new Vector3(side * 0.07f, -0.062f, -0.106f));
            blush.Visible = feminine;
            head.AddChild(blush);
            _blush.Add(blush);
        }

        head.AddChild(Flat(new Vector3(0.0065f, 0.004f, 0.005f), skin.Darkened(0.12f), new Vector3(0, -0.058f, -0.14f)));
        BuildMouths(head);
        _tear = Flat(new Vector3(0.006f, 0.011f, 0.004f), new Color("8fd3ff"), new Vector3(0.062f, -0.06f, -0.112f));
        _tear.Visible = false;
        head.AddChild(_tear);
        _sweat = Flat(new Vector3(0.008f, 0.013f, 0.004f), new Color("b8e6ff"), new Vector3(-0.1f, 0.05f, -0.09f));
        _sweat.Visible = false;
        head.AddChild(_sweat);
        Apply();
    }

    private void BuildMouths(Node3D head)
    {
        var lip = new Color("b05a58");
        var inside = new Color("6e2a34");
        var teeth = new Color("fbf6ec");
        var at = new Vector3(0, -0.093f, -0.118f);

        // A third bigger than the old mouth, so a smile still reads at the game's distance.
        Node3D Shape(string name)
        {
            var node = new Node3D { Position = at, Visible = false, Scale = Vector3.One * 1.35f };
            head.AddChild(node);
            _mouths[name] = node;
            return node;
        }

        Shape("line").AddChild(Flat(new Vector3(0.017f, 0.0035f, 0.004f), lip, Vector3.Zero));

        var smile = Shape("smile");
        foreach (var side in new[] { -1f, 1f })
        {
            var half = Flat(new Vector3(0.012f, 0.0036f, 0.004f), lip, new Vector3(side * 0.0095f, 0.0022f, 0));
            half.Rotation = new Vector3(0, 0, side * 0.5f);
            smile.AddChild(half);
        }

        var grin = Shape("grin");
        grin.AddChild(Flat(new Vector3(0.019f, 0.012f, 0.004f), inside, new Vector3(0, -0.005f, 0)));
        grin.AddChild(Flat(new Vector3(0.016f, 0.0045f, 0.003f), teeth, new Vector3(0, 0.0025f, -0.002f)));
        grin.AddChild(Flat(new Vector3(0.01f, 0.004f, 0.003f), new Color("e07080"), new Vector3(0, -0.011f, -0.002f)));

        Shape("open").AddChild(Flat(new Vector3(0.011f, 0.009f, 0.004f), inside, new Vector3(0, -0.003f, 0)));
        Shape("o").AddChild(Flat(new Vector3(0.0065f, 0.0085f, 0.004f), inside, new Vector3(0, -0.002f, 0)));

        var frown = Shape("frown");
        foreach (var side in new[] { -1f, 1f })
        {
            var half = Flat(new Vector3(0.011f, 0.0034f, 0.004f), lip, new Vector3(side * 0.009f, -0.0015f, 0));
            half.Rotation = new Vector3(0, 0, -side * 0.4f);
            frown.AddChild(half);
        }

        var clench = Shape("teeth");
        clench.AddChild(Flat(new Vector3(0.019f, 0.0075f, 0.004f), inside, new Vector3(0, -0.002f, 0)));
        clench.AddChild(Flat(new Vector3(0.016f, 0.0055f, 0.003f), teeth, new Vector3(0, -0.002f, -0.002f)));

        var smirk = Shape("smirk");
        smirk.AddChild(Flat(new Vector3(0.011f, 0.0034f, 0.004f), lip, new Vector3(-0.006f, 0, 0)));
        var corner = Flat(new Vector3(0.009f, 0.0034f, 0.004f), lip, new Vector3(0.011f, 0.003f, 0));
        corner.Rotation = new Vector3(0, 0, 0.55f);
        smirk.AddChild(corner);
    }

    /// <summary>Eases the face towards the expression, blinks and talks. Call once a frame.</summary>
    public void Step(float delta)
    {
        if (_flash is not null && (_flashTime -= delta) <= 0f)
        {
            _flash = null;
        }

        var target = _flash is not null ? Faces.Get(_flash) : _target;
        var rate = 1f - Mathf.Exp(-delta * 14f);
        _browRaise = Mathf.Lerp(_browRaise, target.BrowRaise, rate);
        _browTilt = Mathf.Lerp(_browTilt, target.BrowTilt, rate);
        _lidUpper = Mathf.Lerp(_lidUpper, target.LidUpper, rate);
        _lidLower = Mathf.Lerp(_lidLower, target.LidLower, rate);
        _wide = Mathf.Lerp(_wide, target.Wide ? 1f : 0f, rate);
        _mouth = target.Mouth;

        _blink -= delta;
        if (_blink < -0.12f)
        {
            _blink = (float)GD.RandRange(2.0, 5.0);
        }

        if (Talking)
        {
            _talk += delta;
        }
        else
        {
            _talk = 0f;
        }

        Apply(target);
    }

    private void Apply(FacePreset? preset = null)
    {
        var target = preset ?? _target;
        var blinking = Blinks && _blink < 0f;
        var happy = target.HappyEyes && !blinking;
        var upper = blinking ? 1f : _lidUpper;

        // The open part of the eye runs from the lower lid to the upper lid, in -1 (bottom) to 1 (top).
        var top = 1f - 2f * Mathf.Clamp(upper, 0f, 1f) + _wide * 0.15f;
        var bottom = -1f + 1.2f * Mathf.Clamp(_lidLower, 0f, 1f);
        var open = Mathf.Max(top - bottom, 0.12f);
        foreach (var eye in _eyes)
        {
            eye.Ball.Visible = !happy;
            eye.Happy.Visible = happy;
            eye.Ball.Scale = new Vector3(1f + _wide * 0.08f, open / 2f, 1f);
            eye.Ball.Position = new Vector3(0, (top + bottom) / 2f * EyeHalf, 0);
            eye.Iris.Position = new Vector3(Gaze.X * 0.008f, Gaze.Y * 0.006f, 0);
            eye.Lash.Visible = !happy;
            eye.Lash.Position = new Vector3(0, Mathf.Max(top, bottom + 0.12f) * EyeHalf, 0);

            // The lash slants with the brow: down to the nose in anger, up in sorrow. It reads even
            // where the fringe hides the brows.
            eye.Lash.Rotation = new Vector3(0, 0, eye.Side * -_browTilt * 0.32f);

            // A tilted brow lifts its inner end (sorrow) or drops it (anger). The inner end lies towards the nose.
            eye.Brow.Position = eye.BrowBase + new Vector3(0, _browRaise * 0.012f + _wide * 0.006f, 0);
            eye.Brow.Rotation = new Vector3(0, eye.Side * -0.3f, eye.Side * (-0.08f - _browTilt * 0.38f));
        }

        // Talking: the mouth opens and closes about eight times a second, wider now and then.
        var mouth = _mouth;
        if (Talking)
        {
            var beat = (int)(_talk * 8f);
            mouth = beat % 2 == 0 ? (beat % 6 == 0 ? "open" : "o") : _mouth is "open" or "o" ? "line" : _mouth;
        }

        foreach (var (name, node) in _mouths)
        {
            node.Visible = name == mouth;
        }

        foreach (var blush in _blush)
        {
            blush.Visible = _feminine || target.Blush;
        }

        _tear.Visible = target.Tear;
        _sweat.Visible = target.Sweat;
    }

    private static MeshInstance3D Flat(Vector3 radius, Color color, Vector3 position) => new()
    {
        Mesh = new SphereMesh { Radius = 1f, Height = 2f, RadialSegments = 10, Rings = 6 },
        Position = position,
        Scale = radius,
        MaterialOverride = new StandardMaterial3D { AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
    };
}
