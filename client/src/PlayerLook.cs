using System.Collections.Generic;
using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>Turns the player's chosen look (one byte per <see cref="AppearanceField"/>) into a rig look.</summary>
public static class PlayerLook
{
    public static RigLook From(byte[] look)
    {
        if (!Appearance.IsChosen(look))
        {
            look = Appearance.Default;
        }

        var options = Appearance.Options;
        byte Pick(AppearanceField field) => (byte)Mathf.Clamp(look[(int)field], 0, Appearance.Choices(field) - 1);
        var hairColor = new Color(options.HairColors[Pick(AppearanceField.HairColor)]);
        var skin = new Color(options.Skin[Pick(AppearanceField.Skin)]);
        var beard = options.Beards[Pick(AppearanceField.Beard)].Id;
        var extras = new List<string>();
        if (beard is not "none" and not "stubble")
        {
            extras.Add(beard);
        }

        return RigLook.Player with
        {
            Skin = skin,
            Scale = options.Heights[Pick(AppearanceField.Height)],
            Hair = options.Hair[Pick(AppearanceField.Hair)].Id,
            Eyes = new Color(options.Eyes[Pick(AppearanceField.Eyes)]),
            Brows = hairColor.Darkened(0.25f),
            FaceWidth = Pick(AppearanceField.Face) switch { 0 => 0.94f, 2 => 1.06f, _ => 1f },
            Extras = extras.ToArray(),
            Palette = new Dictionary<string, Color> { ["anime_hair"] = hairColor },
            FaceStyle = new FaceStyle(
                Pick(AppearanceField.Nose), Pick(AppearanceField.EyeShape), Pick(AppearanceField.Brows),
                options.Scars[Pick(AppearanceField.Scar)].Id, beard == "stubble", hairColor.Darkened(0.2f)),
        };
    }
}
