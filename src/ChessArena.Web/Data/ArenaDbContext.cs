using Microsoft.EntityFrameworkCore;

namespace ChessArena.Web.Data;

public sealed class ArenaDbContext(DbContextOptions<ArenaDbContext> options) : DbContext(options)
{
    public DbSet<PlayerEntity> Players => Set<PlayerEntity>();

    public DbSet<GameEntity> Games => Set<GameEntity>();

    public DbSet<MoveEntity> Moves => Set<MoveEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerEntity>(player =>
        {
            player.ToTable("Players");
            player.HasKey(p => p.Id);
            // Handles are matched case-insensitively: "Magnus" and "magnus" are the same player.
            player.Property(p => p.Handle).HasMaxLength(20).UseCollation("NOCASE");
            player.HasIndex(p => p.Handle).IsUnique();
            player.HasIndex(p => p.Rating);
        });

        modelBuilder.Entity<GameEntity>(game =>
        {
            game.ToTable("Games");
            game.HasKey(g => g.Id);
            game.Property(g => g.TimeControl).HasMaxLength(16);
            game.Property(g => g.StartFen).HasMaxLength(100);
            game.Property(g => g.FinalFen).HasMaxLength(100);
            game.Property(g => g.Result).HasMaxLength(7);
            game.Property(g => g.Termination).HasMaxLength(32);
            game.HasOne(g => g.WhitePlayer).WithMany().HasForeignKey(g => g.WhitePlayerId).OnDelete(DeleteBehavior.Restrict);
            game.HasOne(g => g.BlackPlayer).WithMany().HasForeignKey(g => g.BlackPlayerId).OnDelete(DeleteBehavior.Restrict);
            game.HasMany(g => g.Moves).WithOne().HasForeignKey(m => m.GameId).OnDelete(DeleteBehavior.Cascade);
            game.HasIndex(g => g.EndedAtUtc);
        });

        modelBuilder.Entity<MoveEntity>(move =>
        {
            move.ToTable("Moves");
            move.HasKey(m => m.Id);
            move.Property(m => m.Uci).HasMaxLength(5);
            move.Property(m => m.San).HasMaxLength(10);
            move.Property(m => m.FenAfter).HasMaxLength(100);
            move.HasIndex(m => new { m.GameId, m.Ply }).IsUnique();
        });
    }
}
