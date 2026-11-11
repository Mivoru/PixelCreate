using Godot;
using PixelCreate.Core;

namespace PixelCreate;

/// <summary>
/// Rhea's training on screen: a card on the left with the five lessons (done ones ticked, the current
/// one lit, the key for each) and, above the hotbar, a large prompt for what to do now with its key.
/// It reads the training flags from the story copy (<see cref="Tutorial.Lessons"/>); Backspace skips.
/// </summary>
public partial class Hud
{
    private static readonly (string Title, string Prompt, string Key)[] TutorialLessons =
    {
        ("Light attack", "Strike Rhea with quick light blows", "Left mouse"),
        ("Heavy attack", "Put your weight into a heavy blow", "Right mouse"),
        ("Block", "Hold your guard and take her blows on it", "Hold mouse wheel"),
        ("Dash", "When the ground goes red, wait, then dash just as the blow falls", "Shift"),
        ("Slide", "Run, then slide low and fast", "Ctrl while running"),
    };

    /// <summary>Whether the training is on: started and not finished.</summary>
    public bool TutorialShown => Story is { } story && story.World.Has(Tutorial.Started) && !story.World.Has(Tutorial.Finished);

    private void DrawTutorial()
    {
        if (!TutorialShown || Story is not { } story)
        {
            return;
        }

        var current = System.Array.FindIndex(Tutorial.Lessons, l => !story.World.Has(l.Flag));
        var card = new Rect2(28, Size.Y * 0.28f, 330, 64 + TutorialLessons.Length * 46 + 34);
        Card(card, 16, GlassDeep, true);
        Label("Training with Rhea", card.Position + new Vector2(22, 38), 20, Text, _bold);
        for (var i = 0; i < TutorialLessons.Length; i++)
        {
            var (title, _, key) = TutorialLessons[i];
            var row = new Rect2(card.Position.X + 14, card.Position.Y + 56 + i * 46, card.Size.X - 28, 40);
            var done = current < 0 || i < current;
            var now = i == current;
            if (now)
            {
                Rounded(row, 10, new Color(1, 1, 1, 0.08f), Accent, 2);
            }

            // A tick for a lesson done, a lit dot for the one now, a dim one for those to come.
            var mark = row.Position + new Vector2(18, 20);
            DrawCircle(mark, 9f, done ? Accent : now ? new Color(1, 1, 1, 0.9f) : new Color(1, 1, 1, 0.18f));
            if (done)
            {
                DrawPolyline(new[] { mark + new Vector2(-4, 0), mark + new Vector2(-1, 4), mark + new Vector2(5, -4) }, new Color("181425"), 2.5f, true);
            }

            Label(title, row.Position + new Vector2(38, 26), 17, done ? Accent : now ? Text : new Color(1, 1, 1, 0.45f), _bold);
            var keyWidth = Width(key, 13, _bold) + 14;
            var keyBox = new Rect2(row.End.X - keyWidth - 6, row.Position.Y + 9, keyWidth, 22);
            Rounded(keyBox, 6, new Color(0, 0, 0, 0.35f), new Color(1, 1, 1, done ? 0.12f : 0.3f), 1);
            Label(key, keyBox.Position + new Vector2(7, 16), 13, done ? new Color(1, 1, 1, 0.4f) : Text, _bold);
        }

        Label("Backspace: skip the training", new Vector2(card.Position.X + 22, card.End.Y - 14), 13, new Color(1, 1, 1, 0.5f), _font);

        if (current >= 0 && !DialogOpen)
        {
            // What to do now, large, above the hotbar.
            var (_, prompt, key) = TutorialLessons[current];
            var text = $"{prompt}";
            var width = Width(text, 24, _bold) + Width(key, 18, _bold) + 70;
            var box = new Rect2((Size.X - width) / 2f, Size.Y - 210, width, 52);
            Card(box, 14, GlassDeep, true);
            Label(text, box.Position + new Vector2(22, 34), 24, Text, _bold);
            var keyBox = new Rect2(box.End.X - Width(key, 18, _bold) - 36, box.Position.Y + 11, Width(key, 18, _bold) + 18, 30);
            Rounded(keyBox, 8, Accent with { A = 0.25f }, Accent, 2);
            Label(key, keyBox.Position + new Vector2(9, 22), 18, Accent, _bold);
        }
    }
}
