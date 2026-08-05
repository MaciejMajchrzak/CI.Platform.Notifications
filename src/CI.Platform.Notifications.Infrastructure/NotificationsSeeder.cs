using CI.Platform.Notifications.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace CI.Platform.Notifications.Infrastructure;

public static class NotificationsSeeder
{
    public static async Task SeedAsync(NotificationsDbContext db, CancellationToken ct = default)
    {
        var definitions = BuildDefinitions();

        foreach (var def in definitions)
        {
            if (await db.NotificationDefinitions.AnyAsync(x => x.Code == def.Code, ct))
            {
                // Upsert templates only — never overwrite admin-customized ones
                var existing = await db.NotificationDefinitions
                    .Include(x => x.Templates)
                    .FirstAsync(x => x.Code == def.Code, ct);

                foreach (var tpl in def.Templates)
                {
                    if (!existing.Templates.Any(t => t.Channel == tpl.Channel && t.LanguageCode == tpl.LanguageCode))
                    {
                        existing.Templates.Add(new NotificationTemplate
                        {
                            Id              = Guid.NewGuid(),
                            DefinitionId    = existing.Id,
                            Code            = existing.Code,
                            Channel         = tpl.Channel,
                            LanguageCode    = tpl.LanguageCode,
                            SubjectTemplate = tpl.SubjectTemplate,
                            BodyTemplate    = tpl.BodyTemplate,
                        });
                    }
                }
            }
            else
            {
                await db.NotificationDefinitions.AddAsync(def, ct);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static List<NotificationDefinition> BuildDefinitions() =>
    [
        Def("booking.confirmed", "Booking Confirmed",
            (NotificationChannel.Email, "en",
                "Booking confirmed – {{serviceName}}",
                """
                <h2>Booking Confirmed</h2>
                <p>Hi {{customerName}},</p>
                <p>Your booking for <strong>{{serviceName}}</strong> on <strong>{{dateTime}}</strong>
                at <strong>{{businessName}}</strong> is confirmed.</p>
                <p>Booking reference: <strong>{{bookingRef}}</strong></p>
                """),
            (NotificationChannel.InApp, "en",
                "Booking confirmed: {{serviceName}}",
                "Your booking for {{serviceName}} on {{dateTime}} at {{businessName}} is confirmed. Ref: {{bookingRef}}")),

        Def("booking.reminder.24h", "Booking Reminder (24 h)",
            (NotificationChannel.Email, "en",
                "Reminder: {{serviceName}} tomorrow",
                """
                <h2>Upcoming Appointment</h2>
                <p>Hi {{customerName}},</p>
                <p>This is a reminder that your booking for <strong>{{serviceName}}</strong>
                is tomorrow at <strong>{{time}}</strong>.</p>
                <p>Location: {{location}}</p>
                """),
            (NotificationChannel.InApp, "en",
                "Reminder: {{serviceName}} tomorrow at {{time}}",
                "Your appointment for {{serviceName}} is tomorrow at {{time}}. Location: {{location}}")),

        Def("booking.cancelled", "Booking Cancelled",
            (NotificationChannel.Email, "en",
                "Booking cancelled – {{serviceName}}",
                """
                <h2>Booking Cancelled</h2>
                <p>Hi {{customerName}},</p>
                <p>Your booking for <strong>{{serviceName}}</strong> on <strong>{{dateTime}}</strong>
                has been cancelled.</p>
                <p>If you did not request this, please contact us.</p>
                """),
            (NotificationChannel.InApp, "en",
                "Booking cancelled: {{serviceName}}",
                "Your booking for {{serviceName}} on {{dateTime}} has been cancelled.")),

        Def("invoice.overdue", "Invoice Overdue",
            (NotificationChannel.Email, "en",
                "Invoice {{invoiceNumber}} is overdue",
                """
                <h2>Invoice Overdue</h2>
                <p>Hi {{customerName}},</p>
                <p>Invoice <strong>{{invoiceNumber}}</strong> for <strong>{{amount}}</strong>
                was due on <strong>{{dueDate}}</strong> and is now overdue.</p>
                <p>Please arrange payment at your earliest convenience.</p>
                """),
            (NotificationChannel.InApp, "en",
                "Invoice {{invoiceNumber}} overdue",
                "Invoice {{invoiceNumber}} for {{amount}} was due on {{dueDate}} and is now overdue.")),

        Def("account.past_due", "Account Payment Past Due",
            (NotificationChannel.Email, "en",
                "Your account payment is past due",
                """
                <h2>Payment Past Due</h2>
                <p>Hi {{name}},</p>
                <p>Your <strong>{{planName}}</strong> subscription payment is past due.
                Please update your payment method to avoid service interruption.</p>
                """),
            (NotificationChannel.InApp, "en",
                "Payment past due",
                "Your {{planName}} subscription payment is past due. Update your payment method.")),

        Def("account.suspended", "Account Suspended",
            (NotificationChannel.Email, "en",
                "Your account has been suspended",
                """
                <h2>Account Suspended</h2>
                <p>Hi {{name}},</p>
                <p>Your account has been suspended due to a failed payment.
                Please update your billing details or contact support to restore access.</p>
                """),
            (NotificationChannel.InApp, "en",
                "Account suspended",
                "Your account has been suspended. Please update your billing details to restore access.")),
    ];

    private static NotificationDefinition Def(
        string code, string name,
        params (string Channel, string Lang, string? Subject, string Body)[] templates)
    {
        var def = new NotificationDefinition
        {
            Code            = code,
            Name            = name,
            DefaultChannels = (int)(NotificationChannelFlags.Email | NotificationChannelFlags.InApp),
            IsSystem        = true,
        };

        foreach (var (channel, lang, subject, body) in templates)
        {
            def.Templates.Add(new NotificationTemplate
            {
                Id              = Guid.NewGuid(),
                DefinitionId    = def.Id,
                Code            = code,
                Channel         = channel,
                LanguageCode    = lang,
                SubjectTemplate = subject,
                BodyTemplate    = body,
            });
        }

        return def;
    }
}
