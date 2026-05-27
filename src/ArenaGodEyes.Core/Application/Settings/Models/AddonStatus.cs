namespace ArenaGodEyes.Core.Application.Settings.Models;

public sealed record AddonStatus(
    bool Installed,
    bool TocFound,
    bool LuaFound,
    string? Version,
    string? Path);
