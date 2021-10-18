using Discord;
using Discord.Commands;
using IELDiscordBot.Classes.Services;
using IELDiscordBot.Classes.Utilities;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IELDiscordBot.Classes.Modules
{
    internal class RenameRequest
    {
        public IGuildUser GuildUser;
        public ulong MessageId;
        public string Type;
        public string NewName;
    }

    public class UserModule : ModuleBase<SocketCommandContext>
    {
        private readonly ulong AppsTeamID = 472298606259339267;

        private readonly CommandHandler _commands;
        private readonly DSNCalculatorService _service;

        public UserModule(CommandHandler commands, DSNCalculatorService service)
        {
            _commands = commands;
            _service = service;
        }

        [Command("rename")]
        [Name("rename")]
        [Summary("Update the user's nickname on a given platform.")]
        public async Task RequestRenameAsync
        (
            [Name("type")]
            [Summary("Where the rename should take place.\r\nValid Types are: spreadsheet|discord|both")]
            string type,

            [Name("newName")]
            [Summary("What the nickname should be updated to.")]
            [Remainder] string newName
        )
        {
            // Hardcoded to #request-name-change channel
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

            // Create rename request
            RenameRequest req = new RenameRequest
            {
                Type = type,
                NewName = newName,
                MessageId = messageId,
                GuildUser = user
            };
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
                    catch (Exception)
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
        [Name("signup")]
        [Summary("Check the signup status for the provided Discord Id. If no ID is provided, checks the signup for the calling user.")]
        public async Task CheckCurrentSignup([Name("discordId")][Summary("The Discord ID of the user to lookup")] ulong discordId = 0)
        {
            if (discordId == 0)
                discordId = Context.User.Id;

            await _service.GetSignupDetails(discordId, Context.Channel, Context.User.Id);
        }

        [Command("rechecksignup")]
        [Name("rechecksignup")]
        [Summary("Runs the DSN calculation process used by the Application Team. Can only be run once per 48 hours.")]
        public async Task RecheckSignup()
        {
            //await Context.Channel.SendMessageAsync("DSN Calculation has not begun, you cannot recheck/recalculate your games/DSN at this time.");
            //return;
            await _service.RecheckSignup(Context.User.Id, Context.Channel).ConfigureAwait(false);
        }

        [Command("help")]
        [Name("help")]
        [Summary("Nice, I suppose someone had to try it. " +
            "It searches the internal command database and uses a summary that I have written for the commands/parameters to let you know how to use the bot. " +
            "You're welcome :)")]
        public async Task GetHelpForCommand([Name("command")][Summary("The name of the command to get the documentation for")] string command)
        {
            await _commands.SendHelpForCommand(command, Context);
        }
    }
}
