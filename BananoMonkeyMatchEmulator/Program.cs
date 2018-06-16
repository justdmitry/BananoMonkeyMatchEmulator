namespace BananoMonkeyMatchEmulator
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Serilog;

    public static class Program
    {
        public static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .Enrich.FromLogContext()
                .WriteTo.Console(theme: Serilog.Sinks.SystemConsole.Themes.SystemConsoleTheme.Grayscale)
                .CreateLogger();

            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            var services = new ServiceCollection()
                .AddSingleton<IConfiguration>(config)
                .AddLogging(lb => lb.AddSerilog(dispose: true))
                .Configure<EmulatorOptions>(config.GetSection("Emulator"))
                .AddSingleton<IEmulator, Emulator>()
                .BuildServiceProvider();

            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");

            var emulator = services.GetRequiredService<IEmulator>();
            logger.LogInformation("IEmulator created: " + emulator.GetType().Name);

            while (true)
            {
                try
                {
                    await emulator.RunAsync();
                    break;
                }
                catch (HttpRequestException ex)
                {
                    emulator.ExceptionCount++;
                    if (emulator.ExceptionCount > 10)
                    {
                        throw;
                    }
                    else if (emulator.ExceptionCount < 5)
                    {
                        logger.LogWarning("Exception: " + ex.Message);
                        await Task.Delay(TimeSpan.FromSeconds(emulator.ExceptionCount));
                    }
                    else
                    {
                        logger.LogWarning("Exception: " + ex.Message);
                        await Task.Delay(TimeSpan.FromSeconds(30));
                    }
                }
            }

            logger.LogWarning("Emulator stopped.");
        }
    }
}
