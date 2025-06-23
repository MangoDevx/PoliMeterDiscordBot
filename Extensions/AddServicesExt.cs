using Microsoft.Extensions.DependencyInjection;
using PoliMeterDiscordBot.Jobs;
using PoliMeterDiscordBot.Services;
using Quartz;

namespace PoliMeterDiscordBot.Extensions;

public static class AddServicesExt
{
    public static void RegisterServices(IServiceCollection services)
    {
        AddServices(services);
        AddBackgroundServices(services);
        AddHostedServices(services);
    }

    private static void AddServices(IServiceCollection services)
    {
        services.AddQuartz(q =>
        {
            var jobKey = new JobKey("DailyReportJob");
            q.AddJob<WeeklyJobReport>(opts => { opts.WithIdentity(jobKey); });
            q.AddTrigger(opts =>
            {
                opts.ForJob(jobKey)
                    .WithIdentity("DailyReportTrigger")
                    .WithCronSchedule("0 0 0 ? * SUN", cron => { cron.InTimeZone(TimeZoneInfo.Utc); });
            });
        });
    }

    private static void AddBackgroundServices(IServiceCollection services)
    {
    }

    private static void AddHostedServices(IServiceCollection services)
    {
        services.AddHostedService<DatabaseMigrationService>();
        services.AddHostedService<BotHostService>();

        services.AddQuartzHostedService(opt => { opt.WaitForJobsToComplete = true; });
    }
}