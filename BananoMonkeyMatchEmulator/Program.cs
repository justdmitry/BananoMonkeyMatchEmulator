namespace BananoMonkeyMatchEmulator
{
    using System;
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
                .AddTransient<Humanizator>()
                .AddTransient<Emulator>()
                .BuildServiceProvider();

            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");

            var wallet = config["Wallet"];
            var discord = config["Discord"];

            logger.LogInformation($"Wallet '{wallet}', Discord '{discord}'");

            var emulator = services.GetRequiredService<Emulator>();

            await emulator.RunAsync(wallet, discord, true);
        }
    }
}
