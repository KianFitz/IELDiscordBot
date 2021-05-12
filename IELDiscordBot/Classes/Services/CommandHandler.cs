using Discord;
using Discord.Commands;
using Discord.WebSocket;
using IELDiscordBot.Classes.Models;
using IELDiscordBot.Classes.Database;
using IELDiscordBot.Classes.Modules;
using IELDiscordBot.Classes.Utilities;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Configuration;
using NLog;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

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


        private readonly IEmote _acceptEmote;
        private readonly IEmote _denyEmote;
        private readonly IEmote _upvoteEmote;

        private readonly ulong _emoteVoteChannel;
        private readonly ulong _ielPollChannel;
        private readonly ulong _franchiseContacts;
        public CommandHandler(IELContext db, DiscordSocketClient discord, CommandService commands, IConfigurationRoot config, IServiceProvider services, DeleteMessageService delete, DSNCalculatorService dsn)
        {
            _db = db;
            _client = discord;
            _commands = commands;
            _config = config;
            _provider = services;
            _renameRequests = new List<RenameRequest>();
            _deleteService = delete;
            _dsn = dsn;
            _franchises = new List<Franchise>();

            _acceptEmote = new Emoji("✅");
            _denyEmote = new Emoji("❎");
            _upvoteEmote = new Emoji("⬆️");

            _emoteVoteChannel = 805461819187003423;
            _ielPollChannel = 781593835477270583;
            _franchiseContacts = 668540809410248714;

            _client.UserJoined += OnUserJoined;
            _client.UserLeft += OnUserLeft;
            _client.MessageReceived += OnMessageReceieved;
            _client.GuildMemberUpdated += OnUserUpdated;
            _client.ReactionAdded += OnReactionAdded;
            _client.ReactionRemoved += OnReactionRemoved;
            _client.GuildAvailable += OnGuildAvailable;

            _staffRoleIDs = new List<ulong>()
            {
                468918928845045780, // IEL Managers
                639039764393230347, // IEL Team Leaders
                469683155805274122, // Moderation Team
                547754108346433536  // Support Team
            };
        }

        struct Franchise
        {
            internal string Name;
            internal IGuildUser GM;
            internal IGuildUser AGM;
            internal IGuildUser MasterCaptain;
            internal IGuildUser ChallengerCaptain;
            internal IGuildUser ProspectCaptain;
        }


        private async Task OnGuildAvailable(SocketGuild arg)
        {
            string GetValue(string input)
            {
                return input.Substring(input.IndexOf("-") + 1);
            }

            string GetName(string input)
            {
                int startIndex = input.IndexOf("**") + 2;
                int endIndex = input.LastIndexOf("**");


                return input.Substring(startIndex, endIndex - startIndex);
            }

            if (arg.Id != 468918204362653696) return;

            var franchiseContacts = arg.GetTextChannel(_franchiseContacts);
            var messages = await franchiseContacts.GetMessagesAsync().FlattenAsync();

            foreach (var msg in messages)
            {
                var lines = msg.Content.Split("---------------------------------------------");

                foreach (var franchise in lines)
                {
                    var m = franchise.Split("\n");
                    m = m.Where(x => String.IsNullOrEmpty(x) == false && x != "__**Season 8 Franchise Contacts**__").ToArray();
                    if (m.Length <= 1) continue;

                    Franchise f = new Franchise();

                    f.Name = GetName(m[0]);
                    //TODO: HARDCODED FIX
                    if (f.Name == "Zero Fox") f.Name = "Zero Fox Gaming";
                    f.GM = arg.GetUser(MentionUtils.ParseUser(GetValue(m[1]).Trim()));
                    f.AGM = arg.GetUser(MentionUtils.ParseUser(GetValue(m[2]).Trim()));
                    f.MasterCaptain = arg.GetUser(MentionUtils.ParseUser(GetValue(m[3]).Trim()));
                    f.ChallengerCaptain = arg.GetUser(MentionUtils.ParseUser(GetValue(m[4]).Trim()));
                    f.ProspectCaptain = arg.GetUser(MentionUtils.ParseUser(GetValue(m[5]).Trim()));

                    _franchises.Add(f);

                }
            }

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
                    string fullName = $"{req.GuildUser.Username}#{req.GuildUser.Discriminator}";
                    int row = _dsn.GetRowNumber(fullName);

                    if (row != -1)
                    {
                        string sectionToEdit = $"Player Data!O{row}";
                        await _dsn.MakeRequest(sectionToEdit, new List<object>() { req.NewName }).ConfigureAwait(false);
                        return;
                    }
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

            if (message.Channel.Id == _ielPollChannel)
            {
                await HandlePollPostedAsync(message).ConfigureAwait(false);
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

        struct PollEntry
        {
            internal string League;
            internal string Team1;
            internal string Team2;
        }


        // TODO: Really hacky. Tried to do it with hashtables and sscanf but REGEX SAID NO.
        private async Task HandlePollPostedAsync(SocketMessage message)
        {
            string[] lines = message.Content.Split("\n");
            lines = lines.Where(x => Regex.IsMatch(x, "(Prospect|Challenger|Master)([A-Za-z ])+ vs ([A-Za-z ])+")).ToArray();

            List<PollEntry> entries = new List<PollEntry>();

            foreach (string line in lines)
            {
                string tmp = line.Remove(0, line.IndexOf(" ") + 1);
                string league = tmp.Substring(0, tmp.IndexOf(" ") + 1);
                tmp = tmp.Replace(league, "");
                league = league.Trim();

                string team1 = tmp.Substring(0, tmp.IndexOf("vs")).Trim();
                string team2 = tmp.Substring(tmp.IndexOf("vs") + 2).Trim();


                PollEntry entry = new PollEntry()
                {
                    League = league,
                    Team1 = team1,
                    Team2 = team2
                };

                entries.Add(entry);
            }
            List<IGuildUser> usersToSendTo = new List<IGuildUser>();

            foreach (PollEntry e in entries)
            {
                Franchise f = _franchises.First(x => x.Name == e.Team1);
                Franchise f2 = _franchises.First(x => x.Name == e.Team2);

                switch (e.League)
                {
                    case "Prospect":
                        {
                            usersToSendTo.Add(f.ProspectCaptain);
                            usersToSendTo.Add(f2.ProspectCaptain);
                            break;
                        }
                    case "Challenger":
                        {
                            usersToSendTo.Add(f.ChallengerCaptain);
                            usersToSendTo.Add(f2.ChallengerCaptain);
                            break;
                        }
                    case "Master":
                        {
                            usersToSendTo.Add(f.MasterCaptain);
                            usersToSendTo.Add(f2.MasterCaptain);
                            break;
                        }
                }
            }
            usersToSendTo.ForEach(x => x.SendMessageAsync("", false, Embeds.PollStreamGame()).ConfigureAwait(false));
            _log.Info($"Message sent to the following users: {string.Join(",", usersToSendTo.Select(x => x.Nickname))}");
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
