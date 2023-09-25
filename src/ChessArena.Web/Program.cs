using ChessArena.Core.Matchmaking;
using ChessArena.Web.Api;
using ChessArena.Web.Components;
using ChessArena.Web.Data;
using ChessArena.Web.Hubs;
using ChessArena.Web.Live;
using ChessArena.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddSignalR();

builder.Services.AddDbContext<ArenaDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Arena") ?? "Data Source=chess-arena.db"));

builder.Services.Configure<ArenaOptions>(builder.Configuration.GetSection(ArenaOptions.Section));
builder.Services.Configure<MatchmakingOptions>(builder.Configuration.GetSection($"{ArenaOptions.Section}:Matchmaking"));

// Live state is process-wide; per-request and per-circuit services are scoped.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp => new Matchmaker(
    sp.GetRequiredService<IOptions<MatchmakingOptions>>().Value,
    sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton<IArenaNotifier, HubArenaNotifier>();
builder.Services.AddSingleton<IGameRecorder, GameRecorder>();
builder.Services.AddSingleton<GameManager>();
builder.Services.AddSingleton<LobbyService>();
builder.Services.AddSingleton<MatchmakingWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MatchmakingWorker>());
builder.Services.AddHostedService<ClockWatchdog>();

builder.Services.AddScoped<IPlayerStore, PlayerStore>();
builder.Services.AddScoped<IGameArchive, GameArchive>();
builder.Services.AddScoped<PlayerSession>();
builder.Services.AddScoped<IArenaConnection, ArenaConnection>();

var app = builder.Build();

// Schema is created on first start; see README for why migrations were left out for now.
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<ArenaDbContext>().Database.EnsureCreatedAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapHub<ArenaHub>(ArenaHub.Path);
app.MapArenaApi();

app.Run();

public partial class Program;
