using Autodesk.Forge.Core;
using Autodesk.Forge.DesignAutomation;
using DARunner.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DARunner
{
    internal class Program
    {
        class ConsoleHost : IHostedService
        {
            public Task StartAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task StopAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
        static async Task Main(string[] args)
        {
            // Use HostBuilder to bootstrap the application
            var host = new HostBuilder()
                .ConfigureHostConfiguration(builder =>
                {
                    // some logging settings
                    builder.AddJsonFile("appsettings.json");
                })
                .ConfigureAppConfiguration(builder =>
                {
                    // TODO1: you must supply your appsettings.user.json with the following content:
                    //{
                    //    "Forge": {
                    //        "ClientId": "<your client Id>",
                    //        "ClientSecret": "<your secret>"
                    //    }
                    //}
                    builder.AddJsonFile("appsettings.user.json");                    
                })
                .ConfigureLogging((logger) =>
                {
                    logger.Services.AddLogging();
                    logger.SetMinimumLevel(LogLevel.Error); 
                    logger.AddConsole();
                })
                .ConfigureServices((hostContext, services) =>
                {
                    var config = hostContext.Configuration;
                    var clientID = config["APS_CLIENT_ID"];
                    var clientSecret = config["APS_CLIENT_SECRET"];                   
                    if (string.IsNullOrEmpty(clientID) || string.IsNullOrEmpty(clientSecret))
                    {
                        throw new ApplicationException("Missing required environment variables APS_CLIENT_ID or APS_CLIENT_SECRET.");
                    }
                    // add our no-op host (required by the HostBuilder)
                    services.AddHostedService<ConsoleHost>();
                    // our own app where all the real stuff happens
                    services.AddSingleton<APS>();

                    // add and configure DESIGN AUTOMATION
                    services.AddDesignAutomation(hostContext.Configuration);
                })
                .UseConsoleLifetime()
                .Build();

                using (host)
                {
                    await host.StartAsync();
                    // Get a reference to our App and run it
                    var app = host.Services.GetRequiredService<APS>();
                    // Delete all the buckets and run the app
                    await app.CleanUp();
                    await app.RunAsync();               
                    await host.StopAsync();
                }
        }
    }
}
