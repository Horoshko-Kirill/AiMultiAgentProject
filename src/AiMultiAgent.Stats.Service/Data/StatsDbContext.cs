using Microsoft.EntityFrameworkCore;

namespace AiMultiAgent.Stats.Service.Data;

public sealed class StatsDbContext(DbContextOptions<StatsDbContext> options) : DbContext(options)
{
    public DbSet<StatsRunEntity> Runs => Set<StatsRunEntity>();
    public DbSet<StatsFileEntity> Files => Set<StatsFileEntity>();
    public DbSet<StatsToolExecutionEntity> Tools => Set<StatsToolExecutionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StatsRunEntity>(entity =>
        {
            entity.ToTable("stats_runs");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ComponentName).HasMaxLength(300);
            entity.Property(x => x.AggregationMode).HasMaxLength(100);
            entity.Property(x => x.Summary).HasMaxLength(4000);
            entity.Property(x => x.FailureReason).HasMaxLength(4000);

            entity.HasIndex(x => x.CreatedAtUtc);
            entity.HasIndex(x => x.ComponentName);
            entity.HasIndex(x => x.AggregationMode);
            entity.HasIndex(x => x.IsGatewayFailure);
            entity.HasIndex(x => x.UsedAnyFallback);
        });

        modelBuilder.Entity<StatsFileEntity>(entity =>
        {
            entity.ToTable("stats_files");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.FileName).HasMaxLength(600);

            entity.HasIndex(x => x.RunId);
            entity.HasIndex(x => x.FileName);

            entity.HasOne(x => x.Run)
                .WithMany(x => x.Files)
                .HasForeignKey(x => x.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StatsToolExecutionEntity>(entity =>
        {
            entity.ToTable("stats_tool_executions");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ToolName).HasMaxLength(100);
            entity.Property(x => x.Label).HasMaxLength(600);
            entity.Property(x => x.Details).HasMaxLength(4000);

            entity.HasIndex(x => x.RunId);
            entity.HasIndex(x => x.ToolName);

            entity.HasOne(x => x.Run)
                .WithMany(x => x.Tools)
                .HasForeignKey(x => x.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
