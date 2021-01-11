using Discord;
using Discord.Commands;
using Discord.WebSocket;
using IELDiscordBotPOC.Classes.Database;
using IELDiscordBotPOC.Classes.Models;
using Microsoft.Extensions.Configuration;
using NLog;
using System;
using System.Threading.Tasks;

namespace IELDiscordBotPOC.Classes.Services
{
    class CommandHandler
    {
        private readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly IELContext _db;
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IConfigurationRoot _config;
        private readonly IServiceProvider _provider;

        public CommandHandler(IELContext db, DiscordSocketClient discord, CommandService commands, IConfigurationRoot config, IServiceProvider services)
        {
            _db = db;
            _client = discord;
            _commands = commands;
            _config = config;
            _provider = services;

            _client.UserJoined += OnUserJoined;
            _client.UserLeft += OnUserLeft;
            _client.MessageReceived += OnMessageReceieved;
            _client.GuildMemberUpdated += OnUserUpdated;
            _client.ReactionAdded += OnReactionAdded;
            _client.ReactionRemoved += OnReactionRemoved;
        }



        private async Task OnReactionRemoved(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            DBConfigSettings config = _db.ConfigSettings.Find("Channels", "Log");
            if (config != null)
            {
                ulong channelId = MakeNumeric(config.Value);
                var message = await arg1.DownloadAsync();

                CommandContext context = new CommandContext(_client, message);
                var channel = await context.Guild.GetTextChannelAsync(channelId);

                await channel.SendMessageAsync($"Reaction: {arg3.Emote} removed from message {message.Id} by <@!{arg3.UserId}>!\r\nLink: https://discordapp.com/channels/{context.Guild.Id}/{message.Channel.Id}/{message.Id}");
            }
        }

        private async Task OnReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            //if (arg3.User.Value == _client.CurrentUser)
            //    return;

            //TeamRequest request = Utilities.Utilities.OutstandingTeamRequests.Find(req => req.MessageId == arg3.MessageId);
            //if (request != null)
            //{
            //    var message = await arg1.DownloadAsync();

            //    CommandContext context = new CommandContext(_client, message);
            //    string roleId = "";

            //    switch (arg3.Emote.ToString())
            //    {
            //        case "1️⃣":
            //            roleId = _db.ConfigSettings.Find("Roles", "Prospect").Value;
            //            break;

            //        case "2️⃣":
            //            roleId = _db.ConfigSettings.Find("Roles", "Challenger").Value;
            //            break;

            //        case "3️⃣":
            //            roleId = _db.ConfigSettings.Find("Roles", "Master").Value;
            //            break;

            //        default:
            //            return;
            //    }
            //    await HandleUserTeamSubmitted(context, request, roleId);

            //    Utilities.Utilities.OutstandingTeamRequests.Remove(request);
            //    await message.DeleteAsync();
            //}
            //else
            //{
            //    DBConfigSettings config = _db.ConfigSettings.Find("Channels", "Log");
            //    if (config != null)
            //    {
            //        ulong channelId = MakeNumeric(config.Value);
            //        var message = await arg1.DownloadAsync();

            //        CommandContext context = new CommandContext(_client, message);
            //        var channel = await context.Guild.GetTextChannelAsync(channelId);

            //        await channel.SendMessageAsync($"Reaction: {arg3.Emote} added to message {message.Id} by <@!{arg3.UserId}>!\r\nLink: https://discordapp.com/channels/{context.Guild.Id}/{message.Channel.Id}/{message.Id}");

            //    }
            //}
        }

        private ulong MakeNumeric(string value)
        {
            string retVal = "";
            foreach (char a in value)
            {
                if (char.IsDigit(a))
                    retVal += a;
            }

            return ulong.Parse(retVal);
        }

        private async Task HandleUserTeamSubmitted(CommandContext Context, TeamRequest request, string roleId)
        {
            ulong roleNo = MakeNumeric(roleId);

            IRole playerRole = Context.Guild.GetRole(roleNo);
            IRole teamRole = Context.Guild.GetRole(ulong.Parse(request.Team.Role));

            await request.User.AddRoleAsync(playerRole);
            await request.User.AddRoleAsync(teamRole);
        }

        private async Task OnUserJoined(SocketGuildUser user)
        {
            //await user.SendMessageAsync("", false, Embeds.WelcomeToIEL()).ConfigureAwait(false);

            DBConfigSettings config = _db.ConfigSettings.Find("Channels", "Log");
            if (config != null)
            {
                IGuild guild = user.Guild;
                ITextChannel logChannel = user.Guild.GetTextChannel(MakeNumeric(config.Value));

            }
        }

        private async Task OnUserLeft(SocketGuildUser arg)
        {
        }

        private async Task OnUserUpdated(SocketUser oldUser, SocketUser newUser)
        {
            var oldGuildUser = (oldUser as SocketGuildUser);
            var newGuildUser = (newUser as SocketGuildUser);

            var oldRoles = (oldGuildUser.Roles);
            var newRoles = (newGuildUser.Roles);

            // TODO: if oldRoles does not contain Free Agent and newRoles does not contain Free Agent, send them a message in DMs.
            // Ask Tutan for the cooldown

            //await newUser.SendMessageAsync("", false, Embeds.NewFreeAgent()).ConfigureAwait(false);
        }

        private async Task OnMessageReceieved(SocketMessage message)
        {
            var msg = message as SocketUserMessage;
            var context = new SocketCommandContext(_client, msg);

            if (msg == null)
                return;

            if (msg.Author == _client.CurrentUser)
                return;

            int argPos = 0;
            if (msg.HasStringPrefix(_config["prefix"], ref argPos))
            {
                _log.Info($"{msg.Author.Username} (in {msg?.Channel?.Name}/{context?.Guild?.Name}) is trying to execute: " + msg.Content);
                var result = await _commands.ExecuteAsync(context, argPos, _provider);

                if (!result.IsSuccess)
                {
                    _log.Error(result.ToString());
                    await context.Channel.SendMessageAsync(result.ErrorReason).ConfigureAwait(false);
                    await msg.DeleteAsync().ConfigureAwait(false);
                }
            }
        }
    }
}
