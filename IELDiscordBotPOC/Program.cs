using Discord;
using Discord.Commands;
using Discord.WebSocket;
using IELDiscordBotPOC.Classes.Database;
using IELDiscordBotPOC.Classes.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.EntityFrameworkCore.Extensions;
using MySql.Data.MySqlClient;
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
                .AddSingleton(_config)
                .AddDbContext<IELContext>(options => options.UseMySQL(BuildConnectionString()));


            var provider = services.BuildServiceProvider();

            await provider.GetRequiredService<StartupService>().StartAsync();
            provider.GetRequiredService<CommandHandler>();

            await Task.Delay(-1);
        }

        private string BuildConnectionString()
        {
            //return new MySqlConnectionStringBuilder()
            //{
            //    Server = _config["database:server"],
            //    Password = _config["database:password"],
            //    Database = _config["database:db"],
            //    UserID = _config["database:user"],
            //    Port = uint.Parse(_config["database:port"])
            //}
            //.ConnectionString;

            return new MySqlConnectionStringBuilder()
            {
                Server = "localhost",
                Password = "ielbotdev123",
                Database = "ielbot",
                UserID = "ielbot",
                Port = 3306
            }
            .ConnectionString;
        }
    }
}
