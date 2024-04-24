using MassTransit;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Templates;
using Serilog.Templates.Themes;

namespace App;

public static class ApplicationFactory
{
    public static WebApplication Build<TDbContext>(
        string pgConnectionString,
        string rabbitMqUrl,
        string listenUrl,
        string serviceName)
        where TDbContext : DbContext
    {
        var builder = WebApplication.CreateBuilder();
        
        // Configure logging to include service name
        var log = new LoggerConfiguration()
            .WriteTo.Console(new ExpressionTemplate("[{@t:HH:mm:ss} {@l:u3} {Service,-8}] {@m}\n{@x}", theme: TemplateTheme.Code))
            .CreateLogger()
            .ForContext("Service", serviceName);
        
        builder.Services.AddSingleton<Serilog.ILogger>(log);
        builder.Host.UseSerilog(log);
        
        // Add application specific DB context
        builder.Services.AddDbContext<TDbContext>(options =>
        {
            options
                .UseNpgsql(pgConnectionString)
                .UseSnakeCaseNamingConvention();
        });
        
        // Add MassTransit + RabbitMQ + EntityFrameworkCore bus outbox
        builder.Services.AddMassTransit(busConfigurator =>
        {
            busConfigurator.AddEntityFrameworkOutbox<TDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
            });
            
            busConfigurator.UsingRabbitMq((context, rabbitCfg) =>
            {
                rabbitCfg.Host(rabbitMqUrl);
                rabbitCfg.ConfigureEndpoints(context);
            });
        });

        var app = builder.Build();
        
        // Run migrations
        using (var scope = app.Services.CreateScope())
        {
            using var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
            dbContext.Database.Migrate();
        } 
        
        app.Urls.Add(listenUrl);
        
        // Publish message when application is started
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            using var scope = app.Services.CreateScope();
            var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
            var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
            publishEndpoint.Publish(new Message());
            dbContext.SaveChanges();
        });

        return app;
    }
}