using Discord;
using Discord.Commands;
using Discord.WebSocket;
using IELDiscordBotPOC.Classes.Database;
using IELDiscordBotPOC.Classes.Models;
using IELDiscordBotPOC.Classes.Modules;
using IELDiscordBotPOC.Classes.Utilities;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace IELDiscordBotPOC.Classes.Services
{
    class CommandHandler
    {
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
            _client.MessageReceived += OnMessageReceieved;
            _client.GuildMemberUpdated += OnUserUpdated;
            _client.ReactionAdded += OnReactionAdded;

        }

        private async Task OnReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            if (arg3.User.Value == _client.CurrentUser)
                return;

            TeamRequest request = Utilities.Utilities.OutstandingTeamRequests.Find(req => req.MessageId == arg3.MessageId);
            if (request != null)
            {
                var message = await arg1.DownloadAsync();

                CommandContext context = new CommandContext(_client, message);
                string roleId = "";

                switch (arg3.Emote.ToString())
                {
                    case "1️⃣":
                        roleId = _db.ConfigSettings.Find("Roles", "Prospect").Value;
                        break;    
                                  
                    case "2️⃣":
                        roleId = _db.ConfigSettings.Find("Roles", "Challenger").Value;
                        break;    
                                  
                    case "3️⃣":
                        roleId = _db.ConfigSettings.Find("Roles", "Master").Value;
                        break;

                    default:
                        return;
                }
                await HandleUserTeamSubmitted(context, request, roleId);

                Utilities.Utilities.OutstandingTeamRequests.Remove(request);
                await message.DeleteAsync();
            }
        }

        private ulong SanitiseRole(string roleId)
        {
            string retVal = "";
            foreach (char a in roleId)
            {
                if (char.IsDigit(a))
                    retVal += a;
            }

            return ulong.Parse(retVal);
        }

        private async Task HandleUserTeamSubmitted(CommandContext Context, TeamRequest request, string roleId)
        {
            ulong roleNo = SanitiseRole(roleId);

            IRole playerRole = Context.Guild.GetRole(roleNo);
            IRole teamRole = Context.Guild.GetRole(ulong.Parse(request.Team.Role));

            await request.User.AddRoleAsync(playerRole);
            await request.User.AddRoleAsync(teamRole);
        }

        private async Task OnUserJoined(SocketGuildUser user)
        {
            await user.SendMessageAsync("", false, Embeds.WelcomeToIEL()).ConfigureAwait(false);
        }

        private async Task OnUserUpdated(SocketUser oldUser, SocketUser newUser)
        {
            var oldGuildUser = (oldUser as SocketGuildUser);
            var newGuildUser = (newUser as SocketGuildUser);

            var oldRoles = (oldGuildUser.Roles);
            var newRoles = (newGuildUser.Roles);

            // if oldRoles does not contain Free Agent and newRoles does not contain Free Agent, send them a message in DMs.
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
            if (msg.HasStringPrefix(_config["prefix"], ref argPos) || msg.HasMentionPrefix(_client.CurrentUser, ref argPos))
            {
                //_log.Info($"{msg.Author.Username} (in {msg?.Channel?.Name}/{context?.Guild?.Name}) is trying to execute: " + msg.Content);
                var result = await _commands.ExecuteAsync(context, argPos, _provider);

                if (!result.IsSuccess)
                {
                    //_log.Error(result.ToString());
                    await msg.DeleteAsync().ConfigureAwait(false);
                }
            }
        }
    }
}
