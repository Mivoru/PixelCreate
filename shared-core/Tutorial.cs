using System.Numerics;

namespace PixelCreate.Core;

/// <summary>
/// Rhea's training, right after the player signs with the Guild: not a test of strength but the
/// controls, one lesson at a time. Light attacks, heavy attacks, blocking her blows, dashing through
/// her heavy blow, sliding. She stands still to be struck, attacks only in the lessons that need it,
/// her blows are weak and nobody dies. Each lesson done sets its flag (<see cref="Lessons"/>), which
/// the client shows in its training panel and the scene <c>tutorial</c> answers with her next line.
/// </summary>
public static class Tutorial
{
    /// <summary>The lessons in order: their flag, what is counted, and how many times.</summary>
    public static readonly (string Flag, string Counts, int Needed)[] Lessons =
    {
        ("tutorial.light", "light", 3),
        ("tutorial.heavy", "heavy", 2),
        ("tutorial.block", "block", 3),
        ("tutorial.dodge", "dodge", 2),
        ("tutorial.slide", "slide", 2),
    };

    public const string Started = "tutorial.started";
    public const string Done = "tutorial.done";
    public const string Finished = "tutorial.finished";

    /// <summary>The most a training blow may take, and the least health it leaves.</summary>
    public const short MaxBlow = 4;

    /// <summary>Every flag the training sets, for the story registry.</summary>
    public static IEnumerable<string> Flags => Lessons.Select(l => l.Flag).Append(Started).Append(Done).Append(Finished);
}

public sealed partial class GameServer
{
    /// <summary>Starts Rhea's training on the trial ground: the player stays registered, Rhea stands ready.</summary>
    private void BeginTutorial(Player player)
    {
        if (_story.Has(Tutorial.Started) || _enemies.Any(e => e.TrialOf == player.Id))
        {
            return;
        }

        player.State.Health = MaxHealth(player);
        var ground = _world.TrialGround;
        Move(player, ground + new Vector3(0, 0, 5));
        var type = (byte)EnemyDef.All.ToList().FindIndex(d => d.Id == Guild.ExaminerId);
        var rhea = new Enemy
        {
            Id = _nextEnemyId++,
            Type = type,
            Home = new EnemySpawn(ground, 1.5f, 1, 2, 2, int.MaxValue, false, new[] { Guild.ExaminerId }),
            TrialOf = player.Id,
            TutorLesson = 1,
        };
        rhea.Reset(_random, _world);
        rhea.Position = ground with { Y = _world.GroundHeight(ground.X, ground.Z) };
        rhea.TargetId = player.Id;
        _enemies.Add(rhea);
        player.TutorCount = 0;
        player.TutorMark = (player.Blocks, player.Dodges);
        _story.SetFlag(Tutorial.Started);
    }

    /// <summary>Counts what the current lesson asks for and moves on when it is shown enough times.</summary>
    private void StepTutorial(Player player)
    {
        // After the last lesson Rhea waits for the scene's last word; nothing more is counted.
        if (_enemies.FirstOrDefault(e => e.TrialOf == player.Id && e.TutorLesson > 0) is not { } rhea || rhea.TutorLesson > Tutorial.Lessons.Length)
        {
            return;
        }

        // Slides are counted as they start.
        var sliding = player.State.SlideTicks > 0;
        var lesson = Tutorial.Lessons[rhea.TutorLesson - 1];
        if (sliding && !player.WasSliding && lesson.Counts == "slide")
        {
            player.TutorCount++;
        }

        player.WasSliding = sliding;
        var count = lesson.Counts switch
        {
            "block" => (byte)(player.Blocks - player.TutorMark.Blocks),
            "dodge" => (byte)(player.Dodges - player.TutorMark.Dodges),
            _ => player.TutorCount,
        };
        if (count < lesson.Needed)
        {
            return;
        }

        _story.SetFlag(lesson.Flag);
        player.TutorCount = 0;
        player.TutorMark = (player.Blocks, player.Dodges);
        if (rhea.TutorLesson < Tutorial.Lessons.Length)
        {
            rhea.TutorLesson++;
            return;
        }

        // Every lesson shown: she stops; the scene says her last word and sends the player back.
        rhea.TutorLesson = Tutorial.Lessons.Length + 1;
        _story.SetFlag(Tutorial.Done);
    }

    /// <summary>A blow on Rhea in a striking lesson counts and does no harm. True when she is the target of a lesson.</summary>
    private bool TutorStruck(Player player, Enemy enemy, AttackKind kind)
    {
        if (enemy.TutorLesson <= 0 || enemy.TrialOf != player.Id)
        {
            return false;
        }

        var counts = enemy.TutorLesson <= Tutorial.Lessons.Length ? Tutorial.Lessons[enemy.TutorLesson - 1].Counts : "";
        if ((counts == "light" && kind == AttackKind.Light) || (counts == "heavy" && kind == AttackKind.Heavy))
        {
            player.TutorCount++;
            enemy.HitFlash = 10;
        }

        return true;
    }

    /// <summary>Ends the training (done or skipped): Rhea goes, every lesson counts as shown, the player is back in the hall.</summary>
    private void EndTutorial(Player player)
    {
        _enemies.RemoveAll(e => e.TrialOf == player.Id && e.TutorLesson > 0);
        foreach (var flag in Tutorial.Lessons.Select(l => l.Flag).Append(Tutorial.Done).Append(Tutorial.Finished))
        {
            _story.SetFlag(flag);
        }

        player.State.Health = MaxHealth(player);
        Move(player, Guild.HallEntry);
    }
}
