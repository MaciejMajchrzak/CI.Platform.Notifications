using CI.Platform.Notifications.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace CI.Platform.Notifications.Infrastructure.Configurations;

public sealed class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> b)
    {
        b.ToTable("NotificationLogs");
        b.HasIndex(x => x.TenantId);
        b.HasIndex(x => new { x.TenantId, x.Status });
        b.HasIndex(x => new { x.TenantId, x.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");
        b.Property(x => x.Channel).HasMaxLength(20);
        b.Property(x => x.Status).HasMaxLength(20);
        b.Property(x => x.TemplateKey).HasMaxLength(200);
    }
}

public sealed class NotificationsProcessedEventConfiguration : IEntityTypeConfiguration<NotificationsProcessedEvent>
{
    public void Configure(EntityTypeBuilder<NotificationsProcessedEvent> b)
    {
        b.ToTable("NotificationsProcessedEvents");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.MessageId).IsUnique();
    }
}
