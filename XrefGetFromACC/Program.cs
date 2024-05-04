using Autodesk.Forge.DesignAutomation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

public class Program
{
    public static void Main(string[] args) => CreateHostBuilder(args).Build().Run();

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration(configureDelegate: (context, config) =>
        {
            config.AddJsonFile("appsettings.user.json", optional: false, reloadOnChange: true);
            config.AddEnvironmentVariables();
        })
        .ConfigureServices((hostContext, services) =>
        {
           services.AddDesignAutomation(hostContext.Configuration);
        })
        .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());
}