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

public sealed class NotificationDefinitionConfiguration : IEntityTypeConfiguration<NotificationDefinition>
{
    public void Configure(EntityTypeBuilder<NotificationDefinition> b)
    {
        b.ToTable("NotificationDefinitions");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Code).HasMaxLength(100).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.HasMany(x => x.Templates)
            .WithOne()
            .HasForeignKey(x => x.DefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> b)
    {
        b.ToTable("NotificationTemplates");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.Code, x.Channel, x.LanguageCode }).IsUnique();
        b.Property(x => x.Code).HasMaxLength(100).IsRequired();
        b.Property(x => x.Channel).HasMaxLength(20).IsRequired();
        b.Property(x => x.LanguageCode).HasMaxLength(10).IsRequired();
    }
}

public sealed class NotificationInboxConfiguration : IEntityTypeConfiguration<NotificationInbox>
{
    public void Configure(EntityTypeBuilder<NotificationInbox> b)
    {
        b.ToTable("NotificationInbox");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.TenantId, x.UserId, x.IsRead });
        b.Property(x => x.UserId).HasMaxLength(200).IsRequired();
        b.Property(x => x.Code).HasMaxLength(100).IsRequired();
        b.Property(x => x.Title).HasMaxLength(500).IsRequired();
    }
}
