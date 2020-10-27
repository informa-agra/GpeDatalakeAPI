using System;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace GpeDatalakeAPI
{
    class Program
    {
        public static IConfigurationRoot configuration;
        static int Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(Serilog.Events.LogEventLevel.Debug)
                .WriteTo.File(
                    formatter: new JsonFormatter(),
                    $"./dataLog-{DateTime.UtcNow:yyMMdd-HHmmss}.json",
                    LogEventLevel.Debug,
                    fileSizeLimitBytes: 10000000,
                    flushToDiskInterval: new TimeSpan(0, 4, 0, 0))
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .CreateLogger();


            try
            {
                MainAsync(args).Wait();
                return 0;
            }
            catch (Exception e)
            {
                return 1;
            }
        }

        private static async Task MainAsync(string[] args)
        {
            // Create service collection
            Log.Information("Init DataLake API");
            ServiceCollection serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            var env = "dev";
            if (args.Length > 0)
            {
                env = args[0].Trim().ToLower();
            }
            Log.Information($"Using env: {env}");

            var dl = new DatalakeExporter(configuration, env);
            await dl.RunDataLakeExporter(env);
        }

        private static void ConfigureServices(IServiceCollection serviceCollection)
        {

            // Build configuration
            configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                .AddJsonFile("gpe-app.json", false)
                .Build();

            // Add access to generic IConfigurationRoot
            serviceCollection.AddSingleton<IConfigurationRoot>(configuration);
            
        }
    }
}
