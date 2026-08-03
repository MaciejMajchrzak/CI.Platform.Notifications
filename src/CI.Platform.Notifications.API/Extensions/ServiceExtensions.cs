using CI.Kernel;
using CI.Kernel.InMemory;
using CI.Kernel.Redis;
using CI.Platform.Notifications.Core;
using CI.Platform.Notifications.Core.Commands;
using CI.Platform.Notifications.Core.DTOs;
using CI.Platform.Notifications.Core.Handlers;
using CI.Platform.Notifications.Infrastructure;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CI.Platform.Notifications.API.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddNotificationsServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<NotificationsDbContext>(opts =>
            opts.UseNpgsql(config.GetConnectionString("Notifications")));

        services.AddScoped<INotificationsRepository, NotificationsRepository>();

        services.AddScoped<ICommandHandler<SendNotificationCommand, Guid>, SendNotificationHandler>();
        services.AddScoped<ICommandHandler<GetNotificationLogQuery, NotificationLogDto>, GetNotificationLogHandler>();
        services.AddScoped<ICommandHandler<ListNotificationLogsQuery, PagedResult<NotificationLogDto>>, ListNotificationLogsHandler>();
        services.AddScoped<ICommandBus, HandlerDispatcher>();

        services.AddSingleton<IModuleManifest, NotificationsModuleManifest>();

        var redis = config.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redis))
            services.AddRedisKernel(redis);
        else
            services.AddSingleton<IDistributedLock, NullDistributedLock>();

        return services;
    }

    public static IServiceCollection AddOutboxPublisher(this IServiceCollection services, IConfiguration config)
    {
        var rabbitHost = config["RabbitMQ:Host"];
        if (string.IsNullOrEmpty(rabbitHost))
            return services;

        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(rabbitHost, h =>
                {
                    h.Username(config["RabbitMQ:Username"] ?? "ci");
                    h.Password(config["RabbitMQ:Password"] ?? "ci");
                });
                cfg.ConfigureEndpoints(ctx);
            });
        });
        return services;
    }

    public static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration config)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
                opts.Authority = config["Keycloak:Authority"];
                opts.Audience  = config["Keycloak:Audience"] ?? "account";
                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = !string.IsNullOrEmpty(config["Keycloak:Authority"]),
                    ValidateAudience         = false,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                };
                opts.RequireHttpsMetadata = false;
            });
        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration config)
    {
        var otlpEndpoint = config["OTEL_EXPORTER_OTLP_ENDPOINT"];
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("ci-platform-notifications"))
            .WithTracing(t =>
            {
                t.AddAspNetCoreInstrumentation().AddEntityFrameworkCoreInstrumentation();
                if (!string.IsNullOrEmpty(otlpEndpoint))
                    t.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
            });
        return services;
    }
}
