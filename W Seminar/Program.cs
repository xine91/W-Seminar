using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PokerSimPlatform.Core.Simulation;
using PokerSimPlatform.Web.Services;
using System;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// PokerSim-spezifische Services
var dataDir = builder.Configuration["PokerSim:DataDirectory"] ?? "Data";
var resultFile = builder.Configuration["PokerSim:ResultFileName"] ?? "simulation_results.json";
var dataPath = Path.Combine(builder.Environment.ContentRootPath, dataDir, resultFile);

builder.Services.AddSingleton(_ => new ResultStore(dataPath));
builder.Services.AddTransient<SeedDataService>();

var app = builder.Build();

// Seed-Daten beim Start (falls konfiguriert und Datei leer)
if (builder.Configuration.GetValue("PokerSim:SeedOnStartup", true))
{
    using (var scope = app.Services.CreateScope())
    {
        var seed = scope.ServiceProvider.GetRequiredService<SeedDataService>();
        try
        {
            seed.EnsureSeed();
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Seed-Initialisierung fehlgeschlagen.");
        }
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
