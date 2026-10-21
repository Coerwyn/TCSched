using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TCSched.Api.Domain;

namespace TCSched.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<GameTemplate> GameTemplates => Set<GameTemplate>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Registration> Registrations => Set<Registration>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // SQLite has no time zone info, so read every DateTime back as UTC.
        builder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<GameTemplate>(t =>
        {
            t.HasKey(x => x.Id);
            t.Property(x => x.Id).HasMaxLength(32);
            t.Property(x => x.Name).HasMaxLength(GameTemplateValidator.MaxNameLength);
            // Formats is a primitive collection; EF stores it as a JSON array column.
        });

        model.Entity<Event>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(EventRules.MaxEventNameLength);
            e.Ignore(x => x.EndsAtUtc);
            e.HasIndex(x => x.StartsAtUtc);

            // Restrict: a template can't be deleted while events use it.
            e.HasOne(x => x.GameTemplate).WithMany()
                .HasForeignKey(x => x.GameTemplateId)
                .OnDelete(DeleteBehavior.Restrict);

            // Backstop: the database itself refuses to over-fill an event.
            e.ToTable(tb =>
            {
                tb.HasCheckConstraint("CK_Events_Capacity",
                    $"\"Capacity\" BETWEEN 1 AND {EventRules.MaxCapacity}");
                tb.HasCheckConstraint("CK_Events_RegisteredCount",
                    "\"RegisteredCount\" >= 0 AND \"RegisteredCount\" <= \"Capacity\"");
                tb.HasCheckConstraint("CK_Events_MinPlayers",
                    "\"MinPlayers\" >= 1 AND \"MinPlayers\" <= \"Capacity\"");
            });
        });

        model.Entity<Registration>(r =>
        {
            r.HasKey(x => x.Id);
            r.Property(x => x.DisplayName).HasMaxLength(EventRules.MaxPlayerNameLength);
            r.Property(x => x.NormalizedName).HasMaxLength(EventRules.MaxPlayerNameLength);
            r.HasOne<Event>().WithMany(x => x.Registrations)
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            // One registration per normalized name per event.
            r.HasIndex(x => new { x.EventId, x.NormalizedName }).IsUnique();
        });
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
