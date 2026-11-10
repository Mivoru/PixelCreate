using PixelCreate.Core;
using Xunit;

namespace PixelCreate.NetTests;

/// <summary>
/// The rules of "Done means" in CLAUDE.md that the data itself can prove: a scene never waits for
/// ever on a player who has the controls, and no line names someone the world cannot show is there.
/// </summary>
public class StoryRulesTests
{
    /// <summary>A name a line may say only under a condition that proves the person is there, by the flags that do.</summary>
    private static readonly Dictionary<string, string[]> Proofs = new()
    {
        ["Nell"] = new[] { GameServer.WithFlag("nell"), "nell.hidden" },
    };

    private static readonly string[] Waits = { "waitFor", "waitForKills" };

    [Fact]
    public void EveryWaitWhileThePlayerHasTheControls_HasATimeoutOrAnUntil()
    {
        var stuck = new List<string>();
        foreach (var scene in Story.Scenes)
        {
            // A scene starts unlocked: one that follows another locks input itself.
            var locked = false;
            for (var i = 0; i < scene.Steps.Length; i++)
            {
                var step = scene.Steps[i];
                locked = step.Kind switch
                {
                    "lockInput" => true,
                    "unlockInput" => false,
                    _ => locked,
                };
                if (!locked && Waits.Contains(step.Kind) && step.Timeout <= 0 && scene.Until is null)
                {
                    stuck.Add($"{scene.Id} step {i} ({step.Kind})");
                }
            }
        }

        Assert.True(stuck.Count == 0, "Waits with the controls free and no timeout or until: " + string.Join(", ", stuck));
    }

    [Fact]
    public void StepGuardsTheClientReplays_ReadOnlyStoryState()
    {
        // The client works out locks, fades and cameras from the steps behind the current one, and
        // checks their guards against its own copy of the story: it cannot know where the player stood.
        var bad = Story.Scenes.SelectMany(s => s.Steps.Select((step, i) => (s, step, i)))
            .Where(x => x.step.Kind != "effects" && x.step.If is { ReadsPlace: true })
            .Select(x => $"{x.s.Id} step {x.i}").ToList();
        Assert.True(bad.Count == 0, "Guards that read the player's place on a shown step: " + string.Join(", ", bad));
    }

    [Fact]
    public void NoLineNamesSomeone_WithoutAConditionThatProvesTheyAreThere()
    {
        var bad = new List<string>();
        foreach (var dialogue in Story.Dialogues.Values)
        {
            foreach (var entry in dialogue.Entries)
            {
                Walk(dialogue, entry.Node, new List<Condition> { entry.When }, new HashSet<string>(), bad, $"{dialogue.Npc} entry {entry.Node}");
            }
        }

        foreach (var scene in Story.Scenes)
        {
            var guards = new List<Condition>();
            if (scene.Trigger is { } trigger)
            {
                guards.Add(trigger.When);
            }

            foreach (var (step, i) in scene.Steps.Select((s, i) => (s, i)))
            {
                var here = step.If is { } only ? guards.Append(only).ToList() : guards;
                if (step.Kind == "talk" && Story.Dialogues.TryGetValue(step.Actor, out var dialogue))
                {
                    Walk(dialogue, step.Node, here, new HashSet<string>(), bad, $"{scene.Id} step {i}");
                }
                else if (step.Kind == "say")
                {
                    Check(step.Actor.Length > 0 ? step.Actor : step.Name.ToLowerInvariant(), step.Text, here, bad, $"{scene.Id} step {i}");
                }
            }
        }

        Assert.True(bad.Count == 0, "Lines that name someone who may not be there:\n" + string.Join("\n", bad));
    }

    private static void Walk(DialogueDef dialogue, string node, List<Condition> guards, HashSet<string> seen, List<string> bad, string path)
    {
        if (!seen.Add(node) || !dialogue.Nodes.TryGetValue(node, out var here))
        {
            return;
        }

        Check(dialogue.Npc, here.Text, guards, bad, $"{path} > {node}");
        foreach (var option in here.Options)
        {
            var next = guards.Append(option.When).ToList();
            Check(dialogue.Npc, option.Text, next, bad, $"{path} > {node} [{option.Text}]");
            if (option.Next is { } target)
            {
                Walk(dialogue, target, next, new HashSet<string>(seen), bad, $"{path} > {node}");
            }
        }
    }

    private static void Check(string speaker, string text, List<Condition> guards, List<string> bad, string where)
    {
        foreach (var (name, flags) in Proofs)
        {
            if (speaker == name.ToLowerInvariant() || !text.Contains(name, StringComparison.Ordinal))
            {
                continue;
            }

            if (!guards.Any(g => flags.Any(g.Requires)))
            {
                bad.Add($"{where}: \"{text}\" needs one of {string.Join(", ", flags)}");
            }
        }
    }
}
