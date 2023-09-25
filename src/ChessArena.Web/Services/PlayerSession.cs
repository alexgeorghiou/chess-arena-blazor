using ChessArena.Web.Data;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace ChessArena.Web.Services;

/// <summary>
/// The signed-in player for one Blazor circuit. The player id is kept in protected (encrypted)
/// local storage so a page refresh or a new tab restores the same identity.
/// </summary>
public sealed class PlayerSession(IPlayerStore players, ProtectedLocalStorage storage, ILogger<PlayerSession> logger)
{
    private const string StorageKey = "arena.player";
    private bool _restored;

    public PlayerSummary? Current { get; private set; }

    public bool IsSignedIn => Current is not null;

    public event Action? Changed;

    /// <summary>Restores the stored identity. Must be called after the first render (JS interop).</summary>
    public async Task RestoreAsync()
    {
        if (_restored)
        {
            return;
        }

        _restored = true;
        try
        {
            var stored = await storage.GetAsync<Guid>(StorageKey);
            if (stored.Success && await players.FindAsync(stored.Value) is { } player)
            {
                Current = player;
                Changed?.Invoke();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Security.Cryptography.CryptographicException)
        {
            // Prerendering, or a value written with keys this server no longer has.
            logger.LogDebug(ex, "Could not restore player session");
        }
    }

    public async Task SignInAsync(string handle)
    {
        Current = await players.GetOrCreateAsync(handle);
        _restored = true;
        await storage.SetAsync(StorageKey, Current.Id);
        Changed?.Invoke();
    }

    public async Task RefreshAsync()
    {
        if (Current is not null)
        {
            Current = await players.FindAsync(Current.Id) ?? Current;
            Changed?.Invoke();
        }
    }

    public async Task SignOutAsync()
    {
        Current = null;
        await storage.DeleteAsync(StorageKey);
        Changed?.Invoke();
    }
}
