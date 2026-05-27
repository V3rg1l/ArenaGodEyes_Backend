using ArenaGodEyes.Core.Application.Abstractions.Time;

namespace ArenaGodEyes.Infrastructure.Time;

public sealed class SystemUtcNowProvider : IUtcNowProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
