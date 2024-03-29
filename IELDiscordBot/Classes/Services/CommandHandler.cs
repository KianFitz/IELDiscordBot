﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using IELDiscordBot.Classes.Database;
using IELDiscordBot.Classes.Models;
using IELDiscordBot.Classes.Modules;
using IELDiscordBot.Classes.Utilities;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Configuration;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IELDiscordBot.Classes.Services
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
        private readonly DeleteMessageService _deleteService;
        private readonly DSNCalculatorService _dsn;
        private readonly List<ulong> _staffRoleIDs;
        private readonly List<Franchise> _franchises;
        private readonly VoiceService _voiceService;


        private readonly IEmote _acceptEmote;
        private readonly IEmote _denyEmote;
        private readonly IEmote _upvoteEmote;

        private readonly ulong _emoteVoteChannel;
        private readonly ulong _ielPollChannel;
        private readonly ulong _franchiseContacts;
        public CommandHandler(IELContext db, DiscordSocketClient discord, CommandService commands, IConfigurationRoot config,
            IServiceProvider services, DeleteMessageService delete, DSNCalculatorService dsn, VoiceService voice)
        {
            _db = db;
            _client = discord;
            _commands = commands;
            _config = config;
            _provider = services;
            _renameRequests = new List<RenameRequest>();
            _deleteService = delete;
            _dsn = dsn;
            _voiceService = voice;
            _franchises = new List<Franchise>();

            _acceptEmote = new Emoji(_config["emojis:accept"]);
            _denyEmote = new Emoji(_config["emojis:deny"]);
            _upvoteEmote = new Emoji(_config["emojis:upvote"]);

            //_emoteVoteChannel = ulong.Parse(_config["ids:textChannelIds:emoteVoteChannel"]);
            _emoteVoteChannel = 0;
            //_ielPollChannel = ulong.Parse(_config["ids:textChannelIds:pollChannel"]);
            _ielPollChannel = 0;
            //_franchiseContacts = ulong.Parse(_config["ids:textChannelIds:franchiseContactsChannel"]);
            _franchiseContacts = 0;

            _client.MessageReceived += OnMessageReceieved;
            _client.ReactionAdded += OnReactionAdded;
            _client.ReactionRemoved += OnReactionRemoved;
            _client.UserVoiceStateUpdated += OnUserVoiceStatusChanged;

            _staffRoleIDs = new List<ulong>()
            {
                ulong.Parse(_config["ids:staffRoles:IELManagers"]),
                ulong.Parse(_config["ids:staffRoles:IELTeamLeaders"]),
                ulong.Parse(_config["ids:staffRoles:IELModerationTeam"]),
                ulong.Parse(_config["ids:staffRoles:IELSupportTeam"])
            };
        }

        private async Task OnUserVoiceStatusChanged(SocketUser arg1, SocketVoiceState arg2, SocketVoiceState arg3)
        {
            if (arg3.VoiceChannel != null)
            {
                if (arg3.VoiceChannel.Id == ulong.Parse(_config["ids:voiceChannelIds:createVoice"]))
                {
                    await _voiceService.CreateNewVoiceChannel(
                        arg3.VoiceChannel.Guild,
                        arg3.VoiceChannel.Category,
                        (arg1 as IGuildUser),
                        arg3.VoiceChannel).ConfigureAwait(false);
                }
            }
            if (arg2.VoiceChannel != null)
            {
                if (arg2.VoiceChannel.Users.Count == 0 && _voiceService.IsCustomChannel(arg2.VoiceChannel.Id))
                {
                    await _voiceService.DeleteVoiceChannel(arg2.VoiceChannel.Id);
                }
            }
        }

        internal bool ContainsCommand(string key)
        {
            return (_commands.Commands.Any(x => x.Name == key));
        }

        private struct Franchise
        {
            internal string Name;
            internal IGuildUser GM;
            internal IGuildUser AGM;
            internal IGuildUser MasterCaptain;
            internal IGuildUser ChallengerCaptain;
            internal IGuildUser ProspectCaptain;
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
                    int row = _dsn.GetRowNumber(req.GuildUser.Id);

                    if (row != -1)
                    {
                        string sectionToEdit = $"Player Data!C{row}";
                        _dsn.MakeRequest(sectionToEdit, new List<object>() { req.NewName });
                        return;
                    }
                }

                await message.ModifyAsync(x => x.Embed = Embeds.RequestRename(req.GuildUser, req.Type, req.NewName, accepted, approver.Mention));
                _renameRequests.Remove(req);
            }
        }

        internal async Task SendHelpForCommand(string command, ICommandContext Context)
        {
            var result = _commands.Search(Context, command);
            if (result.IsSuccess)
            {
                foreach (var foundCommand in result.Commands)
                {
                    await Context.Channel.SendMessageAsync("", false, Embeds.CommandHelp(foundCommand)).ConfigureAwait(false);
                }
            }
            else
            {
                await Context.Channel.SendMessageAsync("", false, Embeds.CommandNotFound(command)).ConfigureAwait(false);
            }
        }

        private async Task OnReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            if (arg3.UserId == _client.CurrentUser.Id) return;

            var message = await arg1.DownloadAsync();
            IUser user = await message.Channel.GetUserAsync(arg3.UserId).ConfigureAwait(false);


            await HandleExtraReactionMethodsAsync(arg1.Id, arg3, user, message).ConfigureAwait(false);
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

            if (message.Channel.Id == _ielPollChannel)
            {
                //await HandlePollPostedAsync(message).ConfigureAwait(false);
                return;
            }

            int argPos = 0;
            if (msg.HasStringPrefix(_config["prefix"], ref argPos))
            {
                _log.Info($"{msg.Author.Username} (in {msg?.Channel?.Name}/{context?.Guild?.Name}) is trying to execute: " + msg.Content);
                var result = await _commands.ExecuteAsync(context, argPos, _provider);

                if (!result.IsSuccess)
                {
                    //if (CheckForCustomCommand(msg, argPos, out CustomCommand cmd))
                    //{
                    //    //var result = await _customCommands.ExecuteAsync(context, argPos, _provider);
                    //    await ExecuteCustomCommandAsync(context, cmd).ConfigureAwait(false);
                    //    return;
                    //}

                    //_log.Error(result.ToString());
                    await msg.DeleteAsync().ConfigureAwait(false);
                }
            }
        }

        private struct PollEntry
        {
            internal string League;
            internal string Team1;
            internal string Team2;
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
