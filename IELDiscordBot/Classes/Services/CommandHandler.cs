using Discord;
using Discord.Commands;
using Discord.WebSocket;
using IELDiscordBot.Classes.Services;
using IELDiscordBot.Classes.Models;
using IELDiscordBotPOC.Classes.Database;
using IELDiscordBotPOC.Classes.Models;
using IELDiscordBotPOC.Classes.Modules;
using IELDiscordBotPOC.Classes.Utilities;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Configuration;
using NLog;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IELDiscordBotPOC.Classes.Services
{
    public class CommandHandler
    {
        private readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly IELContext _db;
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IConfigurationRoot _config;
        private readonly IServiceProvider _provider;
        private readonly List<RenameRequest> _renameRequests;
        private readonly GoogleApiService _googleService;
        private readonly DeleteMessageService _deleteService;
        private readonly List<ulong> _staffRoleIDs;

        private readonly IEmote _acceptEmote;
        private readonly IEmote _denyEmote;
        private readonly IEmote _upvoteEmote;

        private readonly ulong _emoteVoteChannel;
        public CommandHandler(IELContext db, DiscordSocketClient discord, CommandService commands, IConfigurationRoot config, IServiceProvider services, GoogleApiService google, DeleteMessageService delete)
        {
            _db = db;
            _client = discord;
            _commands = commands;
            _config = config;
            _provider = services;
            _renameRequests = new List<RenameRequest>();
            _googleService = google;
            _deleteService = delete;

            _acceptEmote = new Emoji("✅");
            _denyEmote = new Emoji("❎");
            _upvoteEmote = new Emoji("⬆️");

            _emoteVoteChannel = 805461819187003423;

            _client.UserJoined += OnUserJoined;
            _client.UserLeft += OnUserLeft;
            _client.MessageReceived += OnMessageReceieved;
            _client.GuildMemberUpdated += OnUserUpdated;
            _client.ReactionAdded += OnReactionAdded;
            _client.ReactionRemoved += OnReactionRemoved;
            _staffRoleIDs = new List<ulong>()
            {
                468918928845045780, // IEL Managers
                639039764393230347, // IEL Team Leaders
                469683155805274122, // Moderation Team
                547754108346433536  // Support Team
            };
        }

        private bool IsStaffMember(IGuildUser user)
        {
            return user.RoleIds.Select(x => x).Intersect(_staffRoleIDs).Any();
        }

        private async Task OnReactionRemoved(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            if (arg3.UserId == _client.CurrentUser.Id) return;

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

        internal void AddRenameRequest(RenameRequest req)
        {
            _renameRequests.Add(req);
        }

        internal async Task HandleRenameRequestAsync(RenameRequest req, IEmote emote, IUser approver, IUserMessage message)
        {
            if (emote.Name != _acceptEmote.Name && emote.Name != _denyEmote.Name) return;
            if (IsStaffMember(approver as IGuildUser) == false)
            {
                var m = await message.Channel.SendMessageAsync($"{approver.Mention} you cannot approve/deny this name change.");
                _deleteService.ScheduleDeletion(m, 5);
                return;
            }
            bool accepted = emote.Name == _acceptEmote.Name;

            if (req != null)
            {
                if (!accepted)
                {
                    await message.ModifyAsync(x => x.Embed = Embeds.RequestRename(req.GuildUser, req.Type, req.NewName, accepted, approver.Mention));
                    _renameRequests.Remove(req);
                    return;
                }

                if (req.Type == "discord" || req.Type == "both")
                {
                    string newDiscordName = "";
                    if (req.GuildUser.Nickname != null)
                    {
                        int indexOfBracket = req.GuildUser.Nickname.IndexOf("]");
                        if (indexOfBracket == -1)
                            newDiscordName = req.NewName;
                        else
                            newDiscordName = req.GuildUser.Nickname.Substring(0, indexOfBracket + 1) + " " + req.NewName;
                    }
                    else
                        newDiscordName = req.NewName;

                    await req.GuildUser.ModifyAsync(x =>
                    {
                        x.Nickname = newDiscordName;
                    }).ConfigureAwait(false);
                }

                if (req.Type == "spreadsheet" || req.Type == "both")
                {
                    ByteBuffer packet = new ByteBuffer(Opcodes.CMSG_NEW_NICKNAME, req.GuildUser.Username.Length + 5 + req.NewName.Length);
                    packet.WriteString(req.GuildUser.Username + "#" + req.GuildUser.Discriminator);
                    packet.WriteString(req.NewName);
                    await _googleService.SendDataToServer(packet.ToByteArray()).ConfigureAwait(false);
                }

                await message.ModifyAsync(x => x.Embed = Embeds.RequestRename(req.GuildUser, req.Type, req.NewName, accepted, approver.Mention));
                _renameRequests.Remove(req);
            }
        }

        private async Task OnReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            if (arg3.UserId == _client.CurrentUser.Id) return;

            var message = await arg1.DownloadAsync();

            IUser user = await message.Channel.GetUserAsync(arg3.UserId).ConfigureAwait(false);

            await HandleExtraReactionMethodsAsync(arg1.Id, arg3, user, message).ConfigureAwait(false);

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

        private async Task HandleExtraReactionMethodsAsync(ulong messageId, SocketReaction reaction, IUser approver, IUserMessage message)
        {
            RenameRequest req = _renameRequests.Find(x => x.MessageId == messageId);
            if (req != null)
            {
                await HandleRenameRequestAsync(req, reaction.Emote, approver, message).ConfigureAwait(false);
                return;
            }
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


            if (message.Channel.Id == _emoteVoteChannel)
            {
                await HandleEmoteVote(message).ConfigureAwait(false);
                return;
            }

            int argPos = 0;
            if (msg.HasStringPrefix(_config["prefix"], ref argPos))
            {
                _log.Info($"{msg.Author.Username} (in {msg?.Channel?.Name}/{context?.Guild?.Name}) is trying to execute: " + msg.Content);
                var result = await _commands.ExecuteAsync(context, argPos, _provider);

                if (!result.IsSuccess)
                {
                    if (CheckForCustomCommand(msg, argPos, out CustomCommand cmd))
                    {
                        //var result = await _customCommands.ExecuteAsync(context, argPos, _provider);
                        await ExecuteCustomCommandAsync(context, cmd).ConfigureAwait(false);
                        return;
                    }

                    //_log.Error(result.ToString());
                    await msg.DeleteAsync().ConfigureAwait(false);
                }
            }
        }

        private async Task ExecuteCustomCommandAsync(SocketCommandContext context, CustomCommand cmd)
        {
            await context.Channel.SendMessageAsync(cmd.ReturnValue).ConfigureAwait(false);
        }

        private bool CheckForCustomCommand(IMessage msg, int argPos, out CustomCommand cmd)
        {
            int indexOfFirstSpace = msg.Content.IndexOf(' ');
            if (indexOfFirstSpace == -1)
                indexOfFirstSpace = msg.Content.Length - 1;

            string command = msg.Content.Substring(argPos, indexOfFirstSpace);

            cmd = _db.CustomCommands.FirstOrDefault(comm => comm.Command == command);

            return cmd != null;

        }

        private async Task HandleEmoteVote(SocketMessage message)
        {
            await message.AddReactionAsync(_upvoteEmote);
        }
    }
}
