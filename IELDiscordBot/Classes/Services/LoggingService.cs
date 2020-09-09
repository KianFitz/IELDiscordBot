using Discord;
using Discord.Commands;
using Discord.WebSocket;
using IELDiscordBotPOC.Classes.Utilities;
using NLog;
using NLog.Conditions;
using NLog.Config;
using NLog.Targets;
using System;
using System.IO;
using System.Threading.Tasks;

namespace IELDiscordBot.Classes.Services
{
    public class LoggingService
    {
        private readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;

        private string _logDirectory => Path.Combine(Utilities.GetBasePath(), "Logs");
        private string _logFile => Path.Combine(_logDirectory, $"{DateTime.UtcNow.ToString("yyyy-MM-dd")}.txt");

        public LoggingService(DiscordSocketClient discord, CommandService commands)
        {
            SetupLogger();

            _discord = discord;
            _commands = commands;

            _discord.Log += OnLogAsync;
            _commands.Log += OnLogAsync;
        }

        internal Task OnLogAsync(LogMessage message)
        {
            if (message.Severity == LogSeverity.Info)
            {
                _log.Info($"{message.ToString()}");
            }
            else if (message.Severity == LogSeverity.Warning)
            {
                _log.Warn($"{message.ToString()}");
            }
            else if (message.Severity == LogSeverity.Error)
            {
                _log.Error($"{message.ToString()}");
            }
            else if (message.Severity == LogSeverity.Critical)
            {
                _log.Fatal($"{message.ToString()}");
            }
            else if (message.Severity == LogSeverity.Debug)
            {
                _log.Debug($"{message.ToString()}");
            }
            else if (message.Severity == LogSeverity.Verbose)
            {
                _log.Trace($"{message.ToString()}");
            }
            return Task.CompletedTask;
        }

        internal void SetupLogger()
        {
            try
            {
                var logConfig = new LoggingConfiguration();
                var consoleTarget = new ColoredConsoleTarget();
                var fileTarget = new FileTarget();

                var highlightRuleInfo = new ConsoleRowHighlightingRule
                {
                    Condition = ConditionParser.ParseExpression("level == LogLevel.Info"),
                    ForegroundColor = ConsoleOutputColor.DarkGreen
                };

                consoleTarget.RowHighlightingRules.Add(highlightRuleInfo);

                consoleTarget.Layout = @"${date:format=HH\:mm\:ss} ${logger:shortName=true} | ${level:uppercase=true:padding=-5} | ${message}";
                fileTarget.FileName = Path.Combine(_logDirectory, $"{DateTime.UtcNow.ToString("yyyy-MM-dd")}.log");
                fileTarget.Layout = @"${date:format=HH\:mm\:ss} ${logger:shortName=true} | ${level:uppercase=true:padding=-5} | ${message}";

                logConfig.AddTarget("console", consoleTarget);
                logConfig.AddTarget("file", fileTarget);

                var rule1 = new LoggingRule("*", NLog.LogLevel.Info, consoleTarget);
                logConfig.LoggingRules.Add(rule1);

                var rule2 = new LoggingRule("*", NLog.LogLevel.Info, fileTarget);
                logConfig.LoggingRules.Add(rule2);

                LogManager.Configuration = logConfig;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}
