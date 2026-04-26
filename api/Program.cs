using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Maestro.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5000");

builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });

builder.Services.AddOpenApi();

builder.Services.AddSingleton<WorkspaceStore>();
builder.Services.AddSingleton<GitService>();
builder.Services.AddSingleton<WorktreeService>();
builder.Services.AddSingleton<EventBus>();
builder.Services.AddSingleton<AgentProcessService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AgentProcessService>());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.MapFallbackToFile("index.html");

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStarted.Register(() =>
{
    if (app.Environment.IsDevelopment())
    {
        return;
    }

    const string url = "http://localhost:5000";
    try
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", url);
        }
        else
        {
            Process.Start("xdg-open", url);
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not auto-launch browser; open {Url} manually.", url);
    }
});

app.Run();
