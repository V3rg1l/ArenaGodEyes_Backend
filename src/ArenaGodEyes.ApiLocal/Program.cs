using ArenaGodEyes.ApiLocal.Endpoints;
using ArenaGodEyes.ApiLocal.HostedServices;
using ArenaGodEyes.Infrastructure.DependencyInjection;
using ArenaGodEyes.Infrastructure.Settings;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var apiProjectRootPath = builder.Environment.ContentRootPath;
var backendRootPath = Path.GetFullPath(Path.Combine(apiProjectRootPath, "..", ".."));
var workspaceRootPath = Path.GetFullPath(Path.Combine(apiProjectRootPath, "..", "..", ".."));

builder.Services.AddArenaGodEyesInfrastructure(new WorkspacePaths(
    apiProjectRootPath,
    backendRootPath,
    workspaceRootPath));
builder.Services.AddHostedService<MatchLogWatcherHostedService>();
builder.Services.AddHostedService<FirstRunBootstrapHostedService>();
builder.Services.AddHostedService<ArenaGodEyes.Infrastructure.Persistence.DatabaseInitializerHostedService>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowAnyOrigin();
    });
});

var app = builder.Build();

app.UseCors();
app.MapImportEndpoints();
app.MapMatchesEndpoints();
app.MapSystemEndpoints();
app.MapSettingsEndpoints();
app.MapVideoEndpoints();
app.Run();
