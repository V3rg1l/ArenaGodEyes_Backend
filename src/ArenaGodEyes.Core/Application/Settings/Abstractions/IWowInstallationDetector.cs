namespace ArenaGodEyes.Core.Application.Settings.Abstractions;

public interface IWowInstallationDetector
{
    Task<string?> DetectWowRetailPathAsync(CancellationToken cancellationToken = default);
}
