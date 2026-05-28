using ArenaGodEyes.Core.Application.CombatLog.Models;

namespace ArenaGodEyes.Core.Application.CombatLog.Abstractions;

public interface ILiveArenaSessionMonitor
{
    LiveArenaSessionStatus GetStatus();
}
