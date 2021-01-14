using Discord;
using Discord.Commands;
using Discord.WebSocket;
using IELDiscordBot.Classes.Services;
using Shared;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IELDiscordBotPOC.Classes.Modules
{
    public class UserModule : ModuleBase<SocketCommandContext>
    {
        

        private readonly GoogleApiService service;
        ulong FAStatusChannel = 665242755731030045;

        ulong MasterRoleID = 671808027313045544;
        ulong ChallengerRoleID = 670230994627854347;
        ulong ProspectRoleID = 670231374896168960;
        ulong AcademyRoleID = 797537384022409256;
        ulong AppsTeamID = 472298606259339267;

        ulong GMRole = 472145107056066580;


        public UserModule(GoogleApiService service)
        {
            this.service = service;
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

            string league = await service.RequestLeagueAsync(guildUser.Username + "#" + s[1]);
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

                ByteBuffer b = new ByteBuffer(Opcodes.CMSG_PLAYER_SIGNUP_ACCEPTED, 1);
                b.WriteInt(row);

                await service.SendDataToServer(b.ToByteArray());    

                b = new ByteBuffer(Opcodes.CMSG_PLAYER_SIGNUP_ACCEPTED, 9);
                b.WriteInt(row);

                await service.SendDataToServer(b.ToByteArray());

                b = new ByteBuffer(Opcodes.CMSG_PLAYER_FA_ROLE, 1 + 1);
                b.WriteInt(row);

                await service.SendDataToServer(b.ToByteArray());
            }
            else
            {
                await Context.Channel.SendMessageAsync($"League column for user {username} is empty OR {username} cannot be found in the spreadsheet.");
            }
        }
    }
}
