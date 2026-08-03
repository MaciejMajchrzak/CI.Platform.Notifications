using CI.Platform.Notifications.API.Extensions;
using CI.Platform.Notifications.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNotificationsServices(builder.Configuration);
builder.Services.AddOutboxPublisher(builder.Configuration);
builder.Services.AddAuth(builder.Configuration);
builder.Services.AddObservability(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddHealthChecks().AddDbContextCheck<NotificationsDbContext>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
    var retries = 0;
    while (retries < 10)
    {
        try { await db.Database.MigrateAsync(); break; }
        catch { retries++; await Task.Delay(3000); }
    }
}

app.MapHealthChecks("/health");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }
