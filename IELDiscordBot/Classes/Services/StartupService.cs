using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using NLog;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace IELDiscordBot.Classes.Services
{
    public class StartupService
    {
        private readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IConfigurationRoot _config;
        private readonly IServiceProvider _provider;

        public StartupService(DiscordSocketClient discord, CommandService commands, IConfigurationRoot config, IServiceProvider services)
        {
            _client = discord;
            _commands = commands;
            _config = config;
            _provider = services;
        }

        public async Task StartAsync()
        {
#if DEBUG
            await _client.LoginAsync(Discord.TokenType.Bot, _config["tokens:dev"]);
#endif
#if !DEBUG
            await _client.LoginAsync(Discord.TokenType.Bot, _config["tokens:live"]);
#endif
            _log.Info("Bot is initialising...");
            await _client.StartAsync();

            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _provider);
        }
    }
}
