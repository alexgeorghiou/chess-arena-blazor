using ChessArena.Core.Clocks;

namespace ChessArena.Core.Matchmaking;

public sealed record Seek(Guid PlayerId, string Handle, int Rating, TimeControl TimeControl, DateTimeOffset EnqueuedAt);

public sealed record Pairing(Seek White, Seek Black, TimeControl TimeControl);

public sealed class MatchmakingOptions
{
    /// <summary>Rating difference accepted the moment a seek is created.</summary>
    public int InitialWindow { get; set; } = 50;

    /// <summary>How much the acceptable difference grows for every second spent waiting.</summary>
    public double WindowGrowthPerSecond { get; set; } = 15;

    /// <summary>Upper bound so a long wait never produces wildly lopsided games.</summary>
    public int MaxWindow { get; set; } = 600;
}

/// <summary>
/// Rating-based matchmaking queue. Each seek accepts opponents within a window that widens the longer
/// it waits; two seeks are compatible when they share a time control and each falls inside the other's
/// window, so a newcomer is never forced onto someone who has not waited long enough to accept them.
/// Oldest seeks are served first and paired with their closest-rated compatible opponent.
/// </summary>
public sealed class Matchmaker
{
    private readonly MatchmakingOptions _options;
    private readonly TimeProvider _time;
    private readonly Random _random;
    private readonly Dictionary<Guid, Seek> _seeks = [];
    private readonly object _gate = new();

    public Matchmaker(MatchmakingOptions options, TimeProvider? timeProvider = null, Random? random = null)
    {
        _options = options;
        _time = timeProvider ?? TimeProvider.System;
        _random = random ?? Random.Shared;
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _seeks.Count;
            }
        }
    }

    public IReadOnlyList<Seek> Pending
    {
        get
        {
            lock (_gate)
            {
                return [.. _seeks.Values.OrderBy(s => s.EnqueuedAt)];
            }
        }
    }

    /// <summary>Adds or replaces the player's seek. Re-seeking resets the waiting time.</summary>
    public Seek Enqueue(Guid playerId, string handle, int rating, TimeControl timeControl)
    {
        var seek = new Seek(playerId, handle, rating, timeControl, _time.GetUtcNow());
        lock (_gate)
        {
            _seeks[playerId] = seek;
        }

        return seek;
    }

    public bool Cancel(Guid playerId)
    {
        lock (_gate)
        {
            return _seeks.Remove(playerId);
        }
    }

    public bool IsSeeking(Guid playerId)
    {
        lock (_gate)
        {
            return _seeks.ContainsKey(playerId);
        }
    }

    public int WindowFor(Seek seek)
    {
        var waited = (_time.GetUtcNow() - seek.EnqueuedAt).TotalSeconds;
        var window = _options.InitialWindow + (int)(Math.Max(0, waited) * _options.WindowGrowthPerSecond);
        return Math.Min(window, _options.MaxWindow);
    }

    /// <summary>Pairs every compatible seek and removes paired players from the queue.</summary>
    public IReadOnlyList<Pairing> MatchPending()
    {
        lock (_gate)
        {
            var queue = _seeks.Values.OrderBy(s => s.EnqueuedAt).ToList();
            var windows = queue.ToDictionary(s => s.PlayerId, WindowFor);
            var paired = new HashSet<Guid>();
            var pairings = new List<Pairing>();

            foreach (var seek in queue)
            {
                if (paired.Contains(seek.PlayerId))
                {
                    continue;
                }

                Seek? best = null;
                foreach (var candidate in queue)
                {
                    if (candidate.PlayerId == seek.PlayerId
                        || paired.Contains(candidate.PlayerId)
                        || candidate.TimeControl != seek.TimeControl)
                    {
                        continue;
                    }

                    var gap = Math.Abs(candidate.Rating - seek.Rating);
                    if (gap > windows[seek.PlayerId] || gap > windows[candidate.PlayerId])
                    {
                        continue;
                    }

                    if (best is null || gap < Math.Abs(best.Rating - seek.Rating))
                    {
                        best = candidate;
                    }
                }

                if (best is null)
                {
                    continue;
                }

                paired.Add(seek.PlayerId);
                paired.Add(best.PlayerId);
                _seeks.Remove(seek.PlayerId);
                _seeks.Remove(best.PlayerId);
                pairings.Add(_random.Next(2) == 0
                    ? new Pairing(seek, best, seek.TimeControl)
                    : new Pairing(best, seek, seek.TimeControl));
            }

            return pairings;
        }
    }
}
