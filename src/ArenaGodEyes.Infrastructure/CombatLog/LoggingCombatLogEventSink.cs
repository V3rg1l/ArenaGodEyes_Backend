using ArenaGodEyes.Core.Application.CombatLog.Abstractions;
using ArenaGodEyes.Core.Application.CombatLog.Models;
using Microsoft.Extensions.Logging;

namespace ArenaGodEyes.Infrastructure.CombatLog;

public sealed class LoggingCombatLogEventSink : ICombatLogEventSink
{
    private readonly ILogger<LoggingCombatLogEventSink> _logger;

    public LoggingCombatLogEventSink(ILogger<LoggingCombatLogEventSink> logger)
    {
        _logger = logger;
    }

    public Task OnNewCombatLogFileDetectedAsync(NewCombatLogFileDetected @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("New combat log file detected: {Path} at {DetectedAt}", @event.Path, @event.DetectedAt);
        return Task.CompletedTask;
    }

    public Task OnCombatLogLineReadAsync(CombatLogLineRead @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Combat log line read from {Path} line {LineNumber}", @event.SourceFile, @event.LineNumber);
        return Task.CompletedTask;
    }
}
