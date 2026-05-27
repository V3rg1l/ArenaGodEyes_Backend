namespace ArenaGodEyes.Core.Application.Abstractions.Time;

public interface IUtcNowProvider
{
    DateTimeOffset UtcNow { get; }
}
