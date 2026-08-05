using CI.Platform.Notifications.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace CI.Platform.Notifications.Infrastructure;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : DbContext(options)
{
    public DbSet<NotificationLog>             NotificationLogs        => Set<NotificationLog>();
    public DbSet<NotificationsProcessedEvent> ProcessedEvents         => Set<NotificationsProcessedEvent>();
    public DbSet<NotificationDefinition>      NotificationDefinitions => Set<NotificationDefinition>();
    public DbSet<NotificationTemplate>        NotificationTemplates   => Set<NotificationTemplate>();
    public DbSet<NotificationInbox>           NotificationInbox       => Set<NotificationInbox>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);

        if (Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
        {
            modelBuilder.Entity<NotificationLog>()
                .Property(x => x.RowVersion)
                .HasColumnType("xid")
                .HasColumnName("xmin")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        }
    }
}
