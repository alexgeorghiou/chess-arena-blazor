namespace ChessArena.Web.Services;

public sealed class ArenaOptions
{
    public const string Section = "Arena";

    /// <summary>
    /// Base address interactive components use to reach the hub. Defaults to the browser-facing
    /// base URI; override when the server cannot reach itself on that address (e.g. behind a proxy).
    /// </summary>
    public string? HubBaseUrl { get; set; }
}
