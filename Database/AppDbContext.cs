﻿using Microsoft.EntityFrameworkCore;
using PoliMeterDiscordBot.Models;

namespace PoliMeterDiscordBot.Database;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<DatabaseRecords.RegisteredChannel> RegisteredChannels { get; set; } = null!;
    public DbSet<DatabaseRecords.MessageData> MessageDatas { get; set; } = null!;
    public DbSet<DatabaseRecords.UserStat> UserStats { get; set; } = null!;
    public DbSet<DatabaseRecords.ChannelStat> ChannelStats { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
    }
}