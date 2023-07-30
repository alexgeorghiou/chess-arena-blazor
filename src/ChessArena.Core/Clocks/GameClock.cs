using ChessArena.Engine;

namespace ChessArena.Core.Clocks;

/// <summary>Point-in-time view of a clock, suitable for sending to clients that tick locally.</summary>
public sealed record ClockSnapshot(TimeSpan White, TimeSpan Black, Color? Running, DateTimeOffset TakenAt)
{
    public TimeSpan For(Color color) => color == Color.White ? White : Black;
}

public enum PressResult
{
    Accepted,
    Flagged,
}

/// <summary>
/// Two-sided chess clock with Fischer increment. Time is measured with an injected
/// <see cref="TimeProvider"/> so tests can drive it deterministically. The clock is passive:
/// a flag only "falls" when someone presses or calls <see cref="CheckFlag"/>, which keeps it free
/// of timers; the server polls it from a single background loop.
/// </summary>
public sealed class GameClock
{
    private readonly TimeProvider _time;
    private readonly TimeSpan[] _remaining;
    private readonly object _gate = new();
    private long _turnStartedAt;

    public GameClock(TimeControl control, TimeProvider? timeProvider = null)
    {
        if (control.Initial <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(control), "Initial time must be positive.");
        }

        Control = control;
        _time = timeProvider ?? TimeProvider.System;
        _remaining = [control.Initial, control.Initial];
    }

    public TimeControl Control { get; }

    public Color? Running { get; private set; }

    public Color? Flagged { get; private set; }

    public bool IsStarted { get; private set; }

    public void Start(Color toMove = Color.White)
    {
        lock (_gate)
        {
            if (IsStarted)
            {
                throw new InvalidOperationException("The clock is already running.");
            }

            IsStarted = true;
            Running = toMove;
            _turnStartedAt = _time.GetTimestamp();
        }
    }

    /// <summary>
    /// Called after <paramref name="mover"/> completes a move. Deducts thinking time, adds the
    /// increment and hands the turn over, unless the mover's flag fell first.
    /// </summary>
    public PressResult Press(Color mover)
    {
        lock (_gate)
        {
            if (Running != mover)
            {
                throw new InvalidOperationException($"It is not {mover}'s clock that is running.");
            }

            var left = _remaining[(int)mover] - Elapsed();
            if (left <= TimeSpan.Zero)
            {
                FlagLocked(mover);
                return PressResult.Flagged;
            }

            _remaining[(int)mover] = left + Control.Increment;
            Running = mover.Opponent();
            _turnStartedAt = _time.GetTimestamp();
            return PressResult.Accepted;
        }
    }

    /// <summary>Returns the colour whose flag has fallen, stopping the clock if that just happened.</summary>
    public Color? CheckFlag()
    {
        lock (_gate)
        {
            if (Running is { } running && _remaining[(int)running] - Elapsed() <= TimeSpan.Zero)
            {
                FlagLocked(running);
            }

            return Flagged;
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (Running is { } running)
            {
                _remaining[(int)running] = Max(TimeSpan.Zero, _remaining[(int)running] - Elapsed());
                Running = null;
            }
        }
    }

    public TimeSpan Remaining(Color color)
    {
        lock (_gate)
        {
            return RemainingLocked(color);
        }
    }

    public ClockSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new ClockSnapshot(RemainingLocked(Color.White), RemainingLocked(Color.Black), Running, _time.GetUtcNow());
        }
    }

    private TimeSpan RemainingLocked(Color color)
    {
        var stored = _remaining[(int)color];
        return Running == color ? Max(TimeSpan.Zero, stored - Elapsed()) : stored;
    }

    private void FlagLocked(Color color)
    {
        _remaining[(int)color] = TimeSpan.Zero;
        Flagged = color;
        Running = null;
    }

    private TimeSpan Elapsed() => _time.GetElapsedTime(_turnStartedAt);

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
