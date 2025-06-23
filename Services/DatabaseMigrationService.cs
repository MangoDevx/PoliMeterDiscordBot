using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PoliMeterDiscordBot.Database;

namespace PoliMeterDiscordBot.Services;

public sealed class DatabaseMigrationService(IServiceProvider services, IDbContextFactory<AppDbContext> contextFactory)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}