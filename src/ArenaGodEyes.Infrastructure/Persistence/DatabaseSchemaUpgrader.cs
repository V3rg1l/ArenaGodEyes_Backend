using Microsoft.Data.Sqlite;

namespace ArenaGodEyes.Infrastructure.Persistence;

internal static class DatabaseSchemaUpgrader
{
    public static async Task EnsureLatestAsync(string databasePath, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync(cancellationToken);

        await EnsureColumnAsync(connection, "Matches", "RecordingStatus", "TEXT", cancellationToken);
        await EnsureColumnAsync(connection, "Matches", "RecordingProvider", "TEXT", cancellationToken);
        await EnsureColumnAsync(connection, "Matches", "RecordingStartedAt", "TEXT", cancellationToken);
        await EnsureColumnAsync(connection, "Matches", "RecordingStoppedAt", "TEXT", cancellationToken);
        await EnsureColumnAsync(connection, "Matches", "VideoDurationSeconds", "REAL", cancellationToken);
        await EnsureColumnAsync(connection, "Matches", "VideoFileSizeBytes", "INTEGER", cancellationToken);
        await EnsureColumnAsync(connection, "Matches", "VideoFramesPerSecond", "REAL", cancellationToken);
        await EnsureColumnAsync(connection, "Matches", "VideoCodec", "TEXT", cancellationToken);
        await EnsureColumnAsync(connection, "Matches", "VideoResolution", "TEXT", cancellationToken);
        await EnsureColumnAsync(connection, "Matches", "LastVideoProcessedAt", "TEXT", cancellationToken);

        await ExecuteNonQueryAsync(connection, """
            CREATE TABLE IF NOT EXISTS AnalysisInsights (
                Id INTEGER NOT NULL CONSTRAINT PK_AnalysisInsights PRIMARY KEY AUTOINCREMENT,
                MatchId TEXT NOT NULL,
                VideoSecond INTEGER NULL,
                Category TEXT NOT NULL,
                Severity TEXT NOT NULL,
                Title TEXT NOT NULL,
                Summary TEXT NOT NULL,
                Evidence TEXT NULL,
                Recommendation TEXT NULL,
                Source TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                MatchRecordEntityId INTEGER NOT NULL,
                CONSTRAINT FK_AnalysisInsights_Matches_MatchRecordEntityId
                    FOREIGN KEY (MatchRecordEntityId) REFERENCES Matches (Id) ON DELETE CASCADE
            );
            """, cancellationToken);

        await ExecuteNonQueryAsync(connection, """
            CREATE INDEX IF NOT EXISTS IX_AnalysisInsights_MatchId_VideoSecond
            ON AnalysisInsights (MatchId, VideoSecond);
            """, cancellationToken);

        await ExecuteNonQueryAsync(connection, """
            CREATE TABLE IF NOT EXISTS ValidationTargets (
                Id INTEGER NOT NULL CONSTRAINT PK_ValidationTargets PRIMARY KEY AUTOINCREMENT,
                MatchId TEXT NOT NULL,
                VideoSecond INTEGER NULL,
                Category TEXT NOT NULL,
                Metric TEXT NOT NULL,
                CurrentValue TEXT NULL,
                ExpectedValue TEXT NULL,
                Unit TEXT NULL,
                Note TEXT NULL,
                Source TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                MatchRecordEntityId INTEGER NOT NULL,
                CONSTRAINT FK_ValidationTargets_Matches_MatchRecordEntityId
                    FOREIGN KEY (MatchRecordEntityId) REFERENCES Matches (Id) ON DELETE CASCADE
            );
            """, cancellationToken);

        await ExecuteNonQueryAsync(connection, """
            CREATE INDEX IF NOT EXISTS IX_ValidationTargets_MatchId_VideoSecond
            ON ValidationTargets (MatchId, VideoSecond);
            """, cancellationToken);

        await ExecuteNonQueryAsync(connection, """
            CREATE TABLE IF NOT EXISTS VideoClips (
                Id INTEGER NOT NULL CONSTRAINT PK_VideoClips PRIMARY KEY AUTOINCREMENT,
                MatchId TEXT NOT NULL,
                VideoSecond INTEGER NOT NULL,
                StartSecond INTEGER NOT NULL,
                EndSecond INTEGER NOT NULL,
                Label TEXT NOT NULL,
                Category TEXT NOT NULL,
                Source TEXT NOT NULL,
                ClipPath TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                MatchRecordEntityId INTEGER NOT NULL,
                CONSTRAINT FK_VideoClips_Matches_MatchRecordEntityId
                    FOREIGN KEY (MatchRecordEntityId) REFERENCES Matches (Id) ON DELETE CASCADE
            );
            """, cancellationToken);

        await ExecuteNonQueryAsync(connection, """
            CREATE INDEX IF NOT EXISTS IX_VideoClips_MatchId_VideoSecond
            ON VideoClips (MatchId, VideoSecond);
            """, cancellationToken);

        await ExecuteNonQueryAsync(connection, """
            CREATE TABLE IF NOT EXISTS MatchMetricSummaries (
                Id INTEGER NOT NULL CONSTRAINT PK_MatchMetricSummaries PRIMARY KEY AUTOINCREMENT,
                MatchId TEXT NOT NULL,
                TotalCasts INTEGER NOT NULL,
                TotalDamage INTEGER NOT NULL,
                TotalHealing INTEGER NOT NULL,
                InterruptCount INTEGER NOT NULL,
                DeathCount INTEGER NOT NULL,
                DamagePerSecond REAL NOT NULL,
                HealingPerSecond REAL NOT NULL,
                CastsPerMinute REAL NOT NULL,
                CreatedAt TEXT NOT NULL,
                MatchRecordEntityId INTEGER NOT NULL,
                CONSTRAINT FK_MatchMetricSummaries_Matches_MatchRecordEntityId
                    FOREIGN KEY (MatchRecordEntityId) REFERENCES Matches (Id) ON DELETE CASCADE
            );
            """, cancellationToken);

        await ExecuteNonQueryAsync(connection, """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_MatchMetricSummaries_MatchId
            ON MatchMetricSummaries (MatchId);
            """, cancellationToken);

        await ExecuteNonQueryAsync(connection, """
            CREATE TABLE IF NOT EXISTS MatchSpellMetrics (
                Id INTEGER NOT NULL CONSTRAINT PK_MatchSpellMetrics PRIMARY KEY AUTOINCREMENT,
                MatchId TEXT NOT NULL,
                SpellName TEXT NOT NULL,
                CastCount INTEGER NOT NULL,
                TotalDamage INTEGER NOT NULL,
                TotalHealing INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                MatchRecordEntityId INTEGER NOT NULL,
                CONSTRAINT FK_MatchSpellMetrics_Matches_MatchRecordEntityId
                    FOREIGN KEY (MatchRecordEntityId) REFERENCES Matches (Id) ON DELETE CASCADE
            );
            """, cancellationToken);

        await ExecuteNonQueryAsync(connection, """
            CREATE INDEX IF NOT EXISTS IX_MatchSpellMetrics_MatchId_SpellName
            ON MatchSpellMetrics (MatchId, SpellName);
            """, cancellationToken);

        await ExecuteNonQueryAsync(connection, """
            CREATE TABLE IF NOT EXISTS CoachKnowledgeParameters (
                Id INTEGER NOT NULL CONSTRAINT PK_CoachKnowledgeParameters PRIMARY KEY AUTOINCREMENT,
                Scope TEXT NOT NULL,
                SpecLabel TEXT NULL,
                Category TEXT NOT NULL,
                Metric TEXT NOT NULL,
                TargetValue TEXT NULL,
                Unit TEXT NULL,
                Note TEXT NULL,
                Source TEXT NOT NULL,
                EvidenceCount INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            """, cancellationToken);

        await ExecuteNonQueryAsync(connection, """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_CoachKnowledgeParameters_Scope_SpecLabel_Category_Metric
            ON CoachKnowledgeParameters (Scope, SpecLabel, Category, Metric);
            """, cancellationToken);

        await ExecuteNonQueryAsync(connection, """
            CREATE TABLE IF NOT EXISTS CoachSkills (
                Id INTEGER NOT NULL CONSTRAINT PK_CoachSkills PRIMARY KEY AUTOINCREMENT,
                Scope TEXT NOT NULL,
                SpecLabel TEXT NULL,
                Area TEXT NOT NULL,
                Goal TEXT NOT NULL,
                Drill TEXT NULL,
                Source TEXT NOT NULL,
                EvidenceCount INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            """, cancellationToken);

        await ExecuteNonQueryAsync(connection, """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_CoachSkills_Scope_SpecLabel_Area_Goal
            ON CoachSkills (Scope, SpecLabel, Area, Goal);
            """, cancellationToken);
    }

    private static async Task EnsureColumnAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string columnType,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        var exists = false;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                exists = true;
                break;
            }
        }

        if (exists)
        {
            return;
        }

        await ExecuteNonQueryAsync(
            connection,
            $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType} NULL;",
            cancellationToken);
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
