using System.Globalization;

namespace ChessArena.Core.Clocks;

public enum TimeControlCategory
{
    Bullet,
    Blitz,
    Rapid,
    Classical,
}

/// <summary>Fischer time control: a base time plus an increment added after every move.</summary>
public readonly record struct TimeControl(TimeSpan Initial, TimeSpan Increment)
{
    public static readonly TimeControl Bullet1Plus0 = FromMinutes(1, 0);
    public static readonly TimeControl Blitz3Plus2 = FromMinutes(3, 2);
    public static readonly TimeControl Blitz5Plus0 = FromMinutes(5, 0);
    public static readonly TimeControl Rapid10Plus5 = FromMinutes(10, 5);

    public static IReadOnlyList<TimeControl> Presets { get; } = [Bullet1Plus0, Blitz3Plus2, Blitz5Plus0, Rapid10Plus5];

    public static TimeControl FromMinutes(double minutes, int incrementSeconds) =>
        new(TimeSpan.FromMinutes(minutes), TimeSpan.FromSeconds(incrementSeconds));

    /// <summary>Expected game duration assuming forty moves, the usual basis for categorising controls.</summary>
    public TimeSpan EstimatedDuration => Initial + Increment * 40;

    public TimeControlCategory Category => EstimatedDuration.TotalSeconds switch
    {
        < 180 => TimeControlCategory.Bullet,
        < 480 => TimeControlCategory.Blitz,
        < 1500 => TimeControlCategory.Rapid,
        _ => TimeControlCategory.Classical,
    };

    /// <summary>PGN TimeControl tag value, e.g. <c>180+2</c>.</summary>
    public string ToPgn() => $"{(int)Initial.TotalSeconds}+{(int)Increment.TotalSeconds}";

    /// <summary>Parses the human form "minutes+seconds", e.g. <c>3+2</c> or <c>0.5+0</c>.</summary>
    public static TimeControl Parse(string text) =>
        TryParse(text, out var control) ? control : throw new FormatException($"'{text}' is not a time control like '3+2'.");

    public static bool TryParse(string? text, out TimeControl control)
    {
        control = default;
        var parts = text?.Split('+', StringSplitOptions.TrimEntries);
        if (parts is not { Length: 2 }
            || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var increment)
            || minutes <= 0 || minutes > 180 || increment > 180)
        {
            return false;
        }

        control = FromMinutes(minutes, increment);
        return true;
    }

    public override string ToString() =>
        $"{Initial.TotalMinutes.ToString("0.##", CultureInfo.InvariantCulture)}+{(int)Increment.TotalSeconds}";
}
