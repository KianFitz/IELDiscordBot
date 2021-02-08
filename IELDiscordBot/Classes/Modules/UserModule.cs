using Discord;
using Discord.Commands;
using Discord.WebSocket;
using IELDiscordBot.Classes.Services;
using IELDiscordBotPOC.Classes.Services;
using IELDiscordBotPOC.Classes.Utilities;
using Shared;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IELDiscordBotPOC.Classes.Modules
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

        [Command("acceptplayer")]
        public async Task AcceptPlayerAsync(string username, int row)
        {
            IUser user = Context.User;
            IGuildUser caller = Context.Guild.GetUser(user.Id);
            if (caller.RoleIds.Contains(AppsTeamID) == false)
            {
                await Context.Channel.SendMessageAsync($"You do not have permission to run this command.");
                return;
            }

            var s = username.Split("#");

            SocketGuildUser guildUser = Context.Guild.Users.FirstOrDefault(x => x.Username.ToLower() == s[0].ToLower() && x.DiscriminatorValue == ushort.Parse(s[1]));
            if (guildUser is null)
            {
                await Context.Channel.SendMessageAsync($"Unable to find user {username} in Discord.");
                return;
            }

            bool isGm = (guildUser.Roles.FirstOrDefault(x => x.Id == GMRole) != null);

            string league = _service.GetLeague(guildUser.Username + "#" + s[1]);
            if (league != "")
            {
                IRole roleToAssign = null;
                switch (league)
                {
                    case "Academy":
                        {
                            roleToAssign = Context.Guild.GetRole(AcademyRoleID);
                            break;
                        }
                    case "Prospect":
                        {
                            roleToAssign = Context.Guild.GetRole(ProspectRoleID);
                            break;
                        }
                    case "Challenger":
                        {
                            roleToAssign = Context.Guild.GetRole(ChallengerRoleID);
                            break;
                        }
                    case "Master":
                        {
                            roleToAssign = Context.Guild.GetRole(MasterRoleID);
                            break;
                        }
                    default:
                        return;
                }

                if (isGm == false)
                {
                    await guildUser.ModifyAsync(x =>
                    {
                        x.Nickname = $"[FA] {(x.Nickname.IsSpecified ? x.Nickname : guildUser.Username)}";
                    });

                    await guildUser.AddRoleAsync(roleToAssign).ConfigureAwait(false);

                    ITextChannel channel = Context.Guild.GetTextChannel(FAStatusChannel);
                    await channel.SendMessageAsync($"{guildUser.Mention} you have been accepted to the IEL!");
                }
                await Context.Channel.SendMessageAsync($"Player {guildUser.Username}#{guildUser.DiscriminatorValue} accepted! (GM: {isGm})");

                List<object> obj = new List<object>();
                obj.Add(true);
                string sectionToEdit = $"DSN Hub!H{row}";

                await _service.MakeRequest(sectionToEdit, obj);
                await Task.Delay(2000);

                obj = new List<object>();
                obj.Add(true);
                obj.Add("");
                obj.Add(true);
                obj.Add(true);
                obj.Add(true);
                sectionToEdit = $"DSN Hub!R{row}";

                await _service.MakeRequest(sectionToEdit, obj);
                await Task.Delay(2000);

                obj = new List<object>();
                obj.Add(true);
                obj.Add(true);
                sectionToEdit = $"DSN Hub!Z{row}";

                await _service.MakeRequest(sectionToEdit, obj);
            }
            else
            {
                await Context.Channel.SendMessageAsync($"League column for user {username} is empty OR {username} cannot be found in the spreadsheet.");
            }
        }
    }
}
