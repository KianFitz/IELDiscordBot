using Discord;
using Discord.Commands;
using Discord.WebSocket;
using IELDiscordBot.Classes.Services;
using IELDiscordBot.Classes.Services;
using IELDiscordBot.Classes.Utilities;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IELDiscordBot.Classes.Modules
{
    class RenameRequest
    {
        public IGuildUser GuildUser;
        public ulong MessageId;
        public string Type;
        public string NewName;
    }

    public class UserModule : ModuleBase<SocketCommandContext>
    {
        ulong FAStatusChannel = 665242755731030045;

        ulong MasterRoleID = 671808027313045544;
        ulong ChallengerRoleID = 670230994627854347;
        ulong ProspectRoleID = 670231374896168960;
        ulong AcademyRoleID = 797537384022409256;
        ulong AppsTeamID = 472298606259339267;

        ulong GMRole = 472145107056066580;

        private CommandHandler _commands;
        private DSNCalculatorService _service;

        public UserModule(CommandHandler commands, DSNCalculatorService service)
        {
            _commands = commands;
            _service = service;
        }

        [Command("rename")]
        public async Task RequestRenameAsync(string type, [Remainder] string newName)
        {
            if (Context.Channel.Id != 530861574689259526)
                return;

            IGuildUser user = Context.User as IGuildUser;

            type = type.ToLower();
            switch (type)
            {
                case "discord":
                case "both":
                {
                    if (newName.Length > 32)
                    {
                        await Context.Channel.SendMessageAsync("", false, Embeds.NameTooLong(user, newName)).ConfigureAwait(false);
                        return;
                    }
                    break;
                }

                case "spreadsheet":
                    break;

                default:
                    {
                        await Context.Channel.SendMessageAsync("", false, Embeds.UnknownRenameType(type)).ConfigureAwait(false);
                        return;
                    }
            }

            var message = await Context.Channel.SendMessageAsync("", false, Embeds.RequestRename(user, type, newName)).ConfigureAwait(false);
            ulong messageId = message.Id;

            await message.AddReactionsAsync(new IEmote[] { new Emoji("✅"), new Emoji("❎") }).ConfigureAwait(false);

            RenameRequest req = new RenameRequest();
            req.Type = type;
            req.NewName = newName;
            req.MessageId = messageId;
            req.GuildUser = user;
            _commands.AddRenameRequest(req);
        }

        [Command("removeteamtags")]
        public async Task RemoveAllPlayerTags()
        {
            var message = await Context.Channel.SendMessageAsync($"Downloading user list..");

            await Context.Guild.DownloadUsersAsync().ConfigureAwait(false);
            var users = Context.Guild.Users;
            int amountUpdated = 0;
            int errorsCounted = 0;
            int usersChecked = 0;
            
            await message.ModifyAsync(x => x.Content = $"Renaming players..").ConfigureAwait(false);

            foreach (var user in users)
            {
                usersChecked++;
                if (user.Nickname != null)
                {
                    try
                    {
                        string oldName = user.Nickname;
                        string newName = Regex.Replace(user.Nickname, @"^\[\w+\] ", "");
                        if (oldName != newName)
                        {
                            await user.ModifyAsync(x =>
                            {
                                x.Nickname = newName;
                            }).ConfigureAwait(false);
                            amountUpdated++;
                        }
                    }
                    catch (Exception ex)
                    {
                        errorsCounted++;
                    }
                }
                if (usersChecked % 50 == 0)
                {
                    await message.ModifyAsync(x => x.Content = $"Renaming Users. Checked: {usersChecked}. Removed {amountUpdated} tags. Caught {errorsCounted} errors.").ConfigureAwait(false);
                }
            }

            await message.ModifyAsync(x => x.Content = $"Operation completed! Removed {amountUpdated} tags. Caught {errorsCounted} errors.").ConfigureAwait(false);
        }

        [Command("accept")]
        public async Task AcceptPlayer(int row)
        {
            IUser user = Context.User;
            IGuildUser caller = Context.Guild.GetUser(user.Id);
            if (caller.RoleIds.Contains(AppsTeamID) == false)
            {
                await Context.Channel.SendMessageAsync($"You do not have permission to run this command.");
                return;
            }

            await _service.QueueAccept(row, Context.Guild, Context.Channel);
        }

        [Command("deny")]
        public async Task DenyPlayer(int row)
        {
            IUser user = Context.User;
            IGuildUser caller = Context.Guild.GetUser(user.Id);
            if (caller.RoleIds.Contains(AppsTeamID) == false)
            {
                await Context.Channel.SendMessageAsync($"You do not have permission to run this command.");
                return;
            }

            await _service.QueueDeny(row, Context.Guild, Context.Channel);
        }

        [Command("assignleagueroles")]
        public async Task AssignLeagueFARoles()
        {
            await _service.AssignLeagueFARoles(Context.Channel, Context.Guild);
        }

        [Command("signup")]
        public async Task CheckCurrentSignup(ulong discordId = 0)
        {
            if (discordId == 0)
                discordId = Context.User.Id;

            await _service.GetSignupDetails(discordId, Context.Channel, Context.User.Id);
        }

        [Command("rechecksignup")]
        public async Task RecheckSignup()
        {
            await _service.RecheckSignup(Context.User.Id, Context.Channel).ConfigureAwait(false);
        }

        [Command("rechecksignup")]
        public async Task RecheckSignup(ulong userId)
        {
            if (Context.User.Id != 301876830737006593) return;

            await _service.RecheckSignup(userId, Context.Channel).ConfigureAwait(false);
        }
    }
}
