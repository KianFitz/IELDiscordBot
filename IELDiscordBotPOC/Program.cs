using Discord;
using Discord.Commands;
using Discord.WebSocket;
using IELDiscordBotPOC.Classes.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;
using System.Security.Authentication.ExtendedProtection;
using System.Threading.Tasks;

namespace IELDiscordBotPOC
{
    public class Program
    {
        private DiscordSocketClient _client;
        private IConfigurationRoot _config = ConfigService.GetConfiguration();

        public static void Main(string[] args) => new Program().StartAsync().GetAwaiter().GetResult();

        public async Task StartAsync()
        {
            var services = new ServiceCollection()
                .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig { LogLevel = LogSeverity.Debug }))
                .AddSingleton(new CommandService(new CommandServiceConfig
                {
                    DefaultRunMode = RunMode.Async,
                    LogLevel = LogSeverity.Debug
                }))
                .AddSingleton<StartupService>()
                .AddSingleton<CommandHandler>()
                .AddSingleton(_config);

            var provider = services.BuildServiceProvider();

            await provider.GetRequiredService<StartupService>().StartAsync();
            provider.GetRequiredService<CommandHandler>();

            await Task.Delay(-1);
        }
    }
}
