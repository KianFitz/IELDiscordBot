using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using NLog;
using System;
using System.Collections.Generic;
using System.Threading;

namespace IELDiscordBot.Classes.Services
{
    public class DeleteMessageService
    {
        private readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly DiscordSocketClient _client;
        private readonly IConfigurationRoot _config;
        private readonly Timer _timer;

        private readonly Dictionary<IMessage, int> _scheduledMessages;

        public DeleteMessageService(DiscordSocketClient client, IConfigurationRoot config)
        {
            _client = client;
            _config = config;
            _scheduledMessages = new Dictionary<IMessage, int>();

            _timer = new Timer(async _ =>
            {
                Dictionary<IMessage, int> tmp = new Dictionary<IMessage, int>(_scheduledMessages);

                foreach (var pair in tmp)
                {
                    _scheduledMessages[pair.Key] -= 3;
                    if (pair.Value <= 0)
                    {
                        await pair.Key.DeleteAsync().ConfigureAwait(false);
                        _scheduledMessages.Remove(pair.Key);
                    }
                }
            },
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(3));
        }

        public void ScheduleDeletion(IMessage message, int seconds)
        {
            _scheduledMessages.Add(message, seconds);
        }
    }
}
