using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using PoliMeterDiscordBot.Extensions;
using PoliMeterDiscordBot.Models;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole();

builder.Configuration
    .AddUserSecrets<BotSettings>()
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

// Configure BotSettings
builder.Services.Configure<BotSettings>(builder.Configuration.GetSection(nameof(BotSettings)));

// Add singletons
AddSingletonsExt.RegisterSingletons(builder.Services);

// Add Hosted services
AddServicesExt.RegisterServices(builder.Services);

await builder.Build().RunAsync();