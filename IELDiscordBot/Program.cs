using Discord;
using Discord.Commands;
using Discord.WebSocket;
using IELDiscordBot.Classes.Services;
using IELDiscordBotPOC.Classes.Database;
using IELDiscordBotPOC.Classes.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using System.Threading.Tasks;

namespace IELDiscordBotPOC
{
    public class Program
    {
        private DiscordSocketClient _client;
        private IConfigurationRoot _config = ConfigService.GetConfiguration();

        public static void Main(string[] args) => new Program().StartAsync(args).GetAwaiter().GetResult();

        public async Task StartAsync(string[] args)
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
                .AddSingleton<LoggingService>()
                //.AddSingleton<DSNCalculatorService>()
#if RELEASE
                .AddSingleton<GoogleApiService>()
#endif
                .AddSingleton(_config)
                .AddDbContext<IELContext>(options => options.UseMySQL(BuildConnectionString()));



            var provider = services.BuildServiceProvider();

            provider.GetRequiredService<LoggingService>();
            await provider.GetRequiredService<StartupService>().StartAsync();
            provider.GetRequiredService<CommandHandler>();
            //provider.GetRequiredService<DSNCalculatorService>();
#if RELEASE
            await provider.GetRequiredService<GoogleApiService>().ConnectToServer().ConfigureAwait(false);
#endif


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
