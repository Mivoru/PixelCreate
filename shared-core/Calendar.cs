namespace PixelCreate.Core;

public enum Season : byte
{
    Thaw = 0,
    Highsun = 1,
    Harvest = 2,
    Frost = 3,
}

public enum DayPart : byte
{
    Morning = 0,
    Afternoon = 1,
    Evening = 2,
    Night = 3,
}

/// <summary>
/// A moment of the story calendar: the day from 1 and the minute of that day. Act I runs 70 days,
/// from the thaw into the first frost. Time flows while the player plays and jumps when they sleep.
/// </summary>
public readonly record struct GameTime(int Day, int Minute) : IComparable<GameTime>
{
    public const int MinutesPerDay = 24 * 60;

    /// <summary>Real ticks for one minute of the day: a game hour lasts 26 seconds.</summary>
    public const int TicksPerMinute = Tuning.TickRate * 26 / 60;

    /// <summary>Waking from sleep and the start of a new game day: six in the morning.</summary>
    public const int DayStartMinute = 6 * 60;

    /// <summary>From eight in the evening the player may go to bed.</summary>
    public const int SleepFromMinute = 20 * 60;

    public static readonly GameTime Start = new(1, 9 * 60);

    public Season Season => Day <= 20 ? Season.Thaw : Day <= 40 ? Season.Highsun : Day <= 58 ? Season.Harvest : Season.Frost;

    public DayPart Part => Minute switch
    {
        >= 6 * 60 and < 12 * 60 => DayPart.Morning,
        >= 12 * 60 and < 18 * 60 => DayPart.Afternoon,
        >= 18 * 60 and < 22 * 60 => DayPart.Evening,
        _ => DayPart.Night,
    };

    public int Hour => Minute / 60;

    /// <summary>Minutes since the first midnight, for comparing and adding.</summary>
    public long Total => (long)(Day - 1) * MinutesPerDay + Minute;

    /// <summary>
    /// How hard it rains, from 0 (dry) to 1: worked out from the day and hour alone, so every player sees the same
    /// weather. About one day in three has a shower of a few hours, which comes on and clears over an hour.
    /// </summary>
    public float Rain
    {
        get
        {
            var seed = (uint)Day * 2654435761u;
            if (seed % 3u != 0u)
            {
                return 0f;
            }

            var start = 7f + seed / 7u % 11u;
            var length = 2f + seed / 77u % 4u;
            var hour = Minute / 60f;
            var into = hour - start;
            if (into < 0f || into > length)
            {
                return 0f;
            }

            var ramp = MathF.Min(1f, MathF.Min(into, length - into));
            return MathF.Min(1f, ramp) * (0.6f + seed / 777u % 5u * 0.1f);
        }
    }

    /// <summary>The dark hours, from eight in the evening to six in the morning, when night creatures are out.</summary>
    public bool IsDark => Minute >= SleepFromMinute || Minute < DayStartMinute;

    public bool CanSleep => Minute >= SleepFromMinute || Minute < DayStartMinute;

    public GameTime AddMinutes(int minutes)
    {
        var total = Total + minutes;
        return new GameTime((int)(total / MinutesPerDay) + 1, (int)(total % MinutesPerDay));
    }

    /// <summary>Sleeping wakes the player at six: the same morning when it is still before six, else the next.</summary>
    public GameTime AfterSleep() => Minute < DayStartMinute ? new GameTime(Day, DayStartMinute) : new GameTime(Day + 1, DayStartMinute);

    public int CompareTo(GameTime other) => Total.CompareTo(other.Total);

    public override string ToString() => $"Day {Day}, {Hour:00}:{Minute % 60:00}";
}

/// <summary>Moves the calendar on with the server's ticks.</summary>
public sealed class Calendar
{
    private int _ticks;

    public GameTime Now { get; set; } = GameTime.Start;

    /// <summary>Set while time must stand still, for example while every player reads a menu or a dialogue.</summary>
    public bool Paused { get; set; }

    /// <summary>Advances one tick and returns how many whole minutes passed (0 or 1).</summary>
    public int Step()
    {
        if (Paused)
        {
            return 0;
        }

        if (++_ticks < GameTime.TicksPerMinute)
        {
            return 0;
        }

        _ticks = 0;
        Now = Now.AddMinutes(1);
        return 1;
    }
}
