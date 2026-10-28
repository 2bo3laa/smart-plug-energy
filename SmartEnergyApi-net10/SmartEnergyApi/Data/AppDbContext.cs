using Microsoft.EntityFrameworkCore;
using SmartEnergyApi.Models;

namespace SmartEnergyApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<Reading> Readings => Set<Reading>();
    public DbSet<Threshold> Thresholds => Set<Threshold>();
    public DbSet<TariffTier> TariffTiers => Set<TariffTier>();
    public DbSet<PasswordReset> PasswordResets => Set<PasswordReset>();
    public DbSet<ApplianceType> ApplianceTypes => Set<ApplianceType>();
    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<ApplianceType>().HasIndex(t => t.Key).IsUnique();
        modelBuilder.Entity<CatalogItem>().HasIndex(c => new { c.TypeKey, c.Brand, c.Model }).IsUnique();

        modelBuilder.Entity<Device>()
            .HasIndex(d => d.DeviceKey)
            .IsUnique();

        modelBuilder.Entity<Device>()
            .HasOne(d => d.User)
            .WithMany(u => u.Devices)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Schedule>()
            .HasOne(s => s.Device)
            .WithMany(d => d.Schedules)
            .HasForeignKey(s => s.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Reading>()
            .HasOne(r => r.Device)
            .WithMany(d => d.Readings)
            .HasForeignKey(r => r.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Reading>()
            .HasIndex(r => new { r.DeviceId, r.Timestamp });

        modelBuilder.Entity<Threshold>()
            .HasOne(t => t.Device)
            .WithMany(d => d.Thresholds)
            .HasForeignKey(t => t.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Alert>()
            .HasOne(a => a.User)
            .WithMany(u => u.Alerts)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Alert>()
            .HasOne(a => a.Device)
            .WithMany(d => d.Alerts)
            .HasForeignKey(a => a.DeviceId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<PasswordReset>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
