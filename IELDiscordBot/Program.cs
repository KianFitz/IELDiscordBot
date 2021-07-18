using Discord;
using Discord.Commands;
using Discord.WebSocket;
using IELDiscordBot.Classes.Services;
using IELDiscordBot.Classes.Database;
using IELDiscordBot.Classes.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using System.Threading.Tasks;

namespace IELDiscordBot
{
    public class Program
    {
        private DiscordSocketClient _client;
        private IConfigurationRoot _config = ConfigService.GetConfiguration();

        public static void Main(string[] args) => new Program().StartAsync(args).GetAwaiter().GetResult();

        public async Task StartAsync(string[] args)
        {
            var services = new ServiceCollection()
                .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig { LogLevel = LogSeverity.Debug, AlwaysDownloadUsers = true }))
                .AddSingleton(new CommandService(new CommandServiceConfig
                {
                    DefaultRunMode = RunMode.Async,
                    LogLevel = LogSeverity.Debug,
                }))
                .AddSingleton<StartupService>()
                .AddSingleton<CommandHandler>()
                .AddSingleton<LoggingService>()
                .AddSingleton<DSNCalculatorService>()
                .AddSingleton<VoiceService>()
                .AddSingleton<DeleteMessageService>()
                .AddSingleton(_config)
                .AddDbContext<IELContext>(options => options.UseMySQL(BuildConnectionString()));



            var provider = services.BuildServiceProvider();

            provider.GetRequiredService<LoggingService>();
            await provider.GetRequiredService<StartupService>().StartAsync();
            provider.GetRequiredService<CommandHandler>();
            provider.GetRequiredService<DeleteMessageService>();
            provider.GetRequiredService<VoiceService>();
            provider.GetRequiredService<DSNCalculatorService>();


            await Task.Delay(-1);
        }

        private string BuildConnectionString()
        {
            return new MySqlConnectionStringBuilder()
            {
                Server = _config["database:server"],
                Password = _config["database:password"],
                Database = _config["database:db"],
                UserID = _config["database:user"],
                Port = uint.Parse(_config["database:port"])
            }
            .ConnectionString;
        }
    }
}
