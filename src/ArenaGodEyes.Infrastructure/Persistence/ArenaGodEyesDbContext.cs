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

    public DbSet<AnalysisInsightEntity> AnalysisInsights => Set<AnalysisInsightEntity>();

    public DbSet<ValidationTargetEntity> ValidationTargets => Set<ValidationTargetEntity>();

    public DbSet<VideoClipEntity> VideoClips => Set<VideoClipEntity>();

    public DbSet<MatchMetricSummaryEntity> MatchMetricSummaries => Set<MatchMetricSummaryEntity>();

    public DbSet<MatchSpellMetricEntity> MatchSpellMetrics => Set<MatchSpellMetricEntity>();

    public DbSet<CoachKnowledgeParameterEntity> CoachKnowledgeParameters => Set<CoachKnowledgeParameterEntity>();

    public DbSet<CoachSkillEntity> CoachSkills => Set<CoachSkillEntity>();

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
            entity.Property(match => match.RecordingStatus).HasMaxLength(32);
            entity.Property(match => match.RecordingProvider).HasMaxLength(64);
            entity.Property(match => match.VideoCodec).HasMaxLength(64);
            entity.Property(match => match.VideoResolution).HasMaxLength(32);
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

        modelBuilder.Entity<AnalysisInsightEntity>(entity =>
        {
            entity.HasKey(insight => insight.Id);
            entity.HasIndex(insight => new { insight.MatchId, insight.VideoSecond });
            entity.Property(insight => insight.Category).HasMaxLength(64);
            entity.Property(insight => insight.Severity).HasMaxLength(32);
            entity.Property(insight => insight.Title).HasMaxLength(256);
            entity.Property(insight => insight.Source).HasMaxLength(64);
            entity.HasOne(insight => insight.Match)
                .WithMany(match => match.AnalysisInsights)
                .HasForeignKey(insight => insight.MatchRecordEntityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ValidationTargetEntity>(entity =>
        {
            entity.HasKey(target => target.Id);
            entity.HasIndex(target => new { target.MatchId, target.VideoSecond });
            entity.Property(target => target.Category).HasMaxLength(64);
            entity.Property(target => target.Metric).HasMaxLength(128);
            entity.Property(target => target.Unit).HasMaxLength(32);
            entity.Property(target => target.Source).HasMaxLength(64);
            entity.HasOne(target => target.Match)
                .WithMany(match => match.ValidationTargets)
                .HasForeignKey(target => target.MatchRecordEntityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VideoClipEntity>(entity =>
        {
            entity.HasKey(clip => clip.Id);
            entity.HasIndex(clip => new { clip.MatchId, clip.VideoSecond });
            entity.Property(clip => clip.Label).HasMaxLength(256);
            entity.Property(clip => clip.Category).HasMaxLength(64);
            entity.Property(clip => clip.Source).HasMaxLength(64);
            entity.Property(clip => clip.ClipPath).HasMaxLength(1024);
            entity.HasOne(clip => clip.Match)
                .WithMany(match => match.VideoClips)
                .HasForeignKey(clip => clip.MatchRecordEntityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MatchMetricSummaryEntity>(entity =>
        {
            entity.HasKey(summary => summary.Id);
            entity.HasIndex(summary => summary.MatchId).IsUnique();
            entity.HasOne(summary => summary.Match)
                .WithOne(match => match.MetricSummary)
                .HasForeignKey<MatchMetricSummaryEntity>(summary => summary.MatchRecordEntityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MatchSpellMetricEntity>(entity =>
        {
            entity.HasKey(metric => metric.Id);
            entity.HasIndex(metric => new { metric.MatchId, metric.SpellName });
            entity.Property(metric => metric.SpellName).HasMaxLength(256);
            entity.HasOne(metric => metric.Match)
                .WithMany(match => match.SpellMetrics)
                .HasForeignKey(metric => metric.MatchRecordEntityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CoachKnowledgeParameterEntity>(entity =>
        {
            entity.HasKey(parameter => parameter.Id);
            entity.HasIndex(parameter => new { parameter.Scope, parameter.SpecLabel, parameter.Category, parameter.Metric }).IsUnique();
            entity.Property(parameter => parameter.Scope).HasMaxLength(32);
            entity.Property(parameter => parameter.SpecLabel).HasMaxLength(128);
            entity.Property(parameter => parameter.Category).HasMaxLength(64);
            entity.Property(parameter => parameter.Metric).HasMaxLength(128);
            entity.Property(parameter => parameter.Unit).HasMaxLength(32);
            entity.Property(parameter => parameter.Source).HasMaxLength(64);
        });

        modelBuilder.Entity<CoachSkillEntity>(entity =>
        {
            entity.HasKey(skill => skill.Id);
            entity.HasIndex(skill => new { skill.Scope, skill.SpecLabel, skill.Area, skill.Goal }).IsUnique();
            entity.Property(skill => skill.Scope).HasMaxLength(32);
            entity.Property(skill => skill.SpecLabel).HasMaxLength(128);
            entity.Property(skill => skill.Area).HasMaxLength(128);
            entity.Property(skill => skill.Goal).HasMaxLength(256);
            entity.Property(skill => skill.Source).HasMaxLength(64);
        });
    }
}
