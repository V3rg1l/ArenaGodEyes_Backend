using ArenaGodEyes.Core.Application.CombatLog.Models;

namespace ArenaGodEyes.Core.Application.CombatLog.Abstractions;

public interface ICombatLogEventSink
{
    Task OnNewCombatLogFileDetectedAsync(NewCombatLogFileDetected @event, CancellationToken cancellationToken = default);

    Task OnCombatLogLineReadAsync(CombatLogLineRead @event, CancellationToken cancellationToken = default);
}
