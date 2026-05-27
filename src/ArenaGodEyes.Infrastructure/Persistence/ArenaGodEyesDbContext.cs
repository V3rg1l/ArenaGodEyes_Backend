using ArenaGodEyes.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ArenaGodEyes.Infrastructure.Persistence;

public sealed class ArenaGodEyesDbContext : DbContext
{
    public ArenaGodEyesDbContext(DbContextOptions<ArenaGodEyesDbContext> options)
        : base(options)
    {
    }

    public DbSet<MatchRecordEntity> Matches => Set<MatchRecordEntity>();

    public DbSet<ManualAnalysisEntity> ManualAnalyses => Set<ManualAnalysisEntity>();

    public DbSet<TimelineMarkerEntity> TimelineMarkers => Set<TimelineMarkerEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MatchRecordEntity>(entity =>
        {
            entity.HasKey(match => match.Id);
            entity.HasIndex(match => match.MatchId).IsUnique();
            entity.Property(match => match.MatchId).HasMaxLength(120);
            entity.Property(match => match.Bracket).HasMaxLength(32);
            entity.Property(match => match.MatchType).HasMaxLength(32);
            entity.Property(match => match.MapName).HasMaxLength(128);
            entity.Property(match => match.PlayerName).HasMaxLength(128);
            entity.Property(match => match.PlayerSpecLabel).HasMaxLength(128);
            entity.Property(match => match.ResultForPlayer).HasMaxLength(32);
        });

        modelBuilder.Entity<ManualAnalysisEntity>(entity =>
        {
            entity.HasKey(analysis => analysis.Id);
            entity.HasOne(analysis => analysis.Match)
                .WithMany(match => match.ManualAnalyses)
                .HasForeignKey(analysis => analysis.MatchRecordEntityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TimelineMarkerEntity>(entity =>
        {
            entity.HasKey(marker => marker.Id);
            entity.HasIndex(marker => new { marker.MatchId, marker.VideoSecond });
            entity.HasOne(marker => marker.Match)
                .WithMany(match => match.TimelineMarkers)
                .HasForeignKey(marker => marker.MatchRecordEntityId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
