using System.Collections.Concurrent;
using ChessArena.Core.Clocks;
using ChessArena.Core.Matchmaking;
using ChessArena.Web.Hubs;

namespace ChessArena.Web.Live;

/// <summary>Seeks, presence and the lobby view built from them.</summary>
public sealed class LobbyService(Matchmaker matchmaker, GameManager games, TimeProvider time)
{
    private readonly ConcurrentDictionary<string, Guid> _connections = new();

    public Matchmaker Matchmaker => matchmaker;

    public void Connected(string connectionId, Guid playerId) => _connections[connectionId] = playerId;

    /// <summary>Forgets the connection; cancels the player's seek if this was their last connection.</summary>
    public void Disconnected(string connectionId)
    {
        if (_connections.TryRemove(connectionId, out var playerId) && !_connections.Values.Contains(playerId))
        {
            matchmaker.Cancel(playerId);
        }
    }

    public int OnlinePlayers => _connections.Values.Distinct().Count();

    public string? TrySeek(Guid playerId, string handle, int rating, string timeControl)
    {
        if (!TimeControl.TryParse(timeControl, out var control))
        {
            return $"'{timeControl}' is not a valid time control.";
        }

        if (games.ActiveGameFor(playerId) is not null)
        {
            return "Finish your current game first.";
        }

        matchmaker.Enqueue(playerId, handle, rating, control);
        return null;
    }

    public LobbySnapshotDto Snapshot()
    {
        var now = time.GetUtcNow();
        var seeks = matchmaker.Pending
            .Select(s => new SeekDto(s.Handle, s.Rating, s.TimeControl.ToString(), (int)(now - s.EnqueuedAt).TotalSeconds))
            .ToList();

        var live = games.Active
            .Select(g => new LiveGameSummaryDto(
                g.Id, g.White.Handle, g.Black.Handle, g.White.Rating, g.Black.Rating,
                g.TimeControl.ToString(), g.Game.Moves.Count, g.SpectatorCount))
            .ToList();

        return new LobbySnapshotDto(seeks, live, OnlinePlayers);
    }
}
