using Discord;
using Discord.Commands;
using Discord.WebSocket;
using IELDiscordBot.Classes.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IELDiscordBotPOC.Classes.Modules
{
    public class UserModule : ModuleBase<SocketCommandContext>
    {
        private readonly DSNCalculatorService service;
        ulong FAStatusChannel = 665242755731030045;

        ulong MasterRoleID = 671808027313045544;
        ulong ChallengerRoleID = 670230994627854347;
        ulong ProspectRoleID = 670231374896168960;
        ulong AcademyRoleID = 797537384022409256;
        ulong AppsTeamID = 472298606259339267;


        public UserModule(DSNCalculatorService service)
        {
            this.service = service;
        }

        public async Task AcceptPlayerAsync(IGuildUser mentionedUser, int row)
        {
            IUser user = Context.User;
            IGuildUser caller = Context.Guild.GetUser(user.Id);
            if (caller.RoleIds.Contains(AppsTeamID) == false)
            {
                await Context.Channel.SendMessageAsync($"You do not have permission to run this command.");
                return;
            }

            SocketGuildUser guildUser = (mentionedUser as SocketGuildUser);
            string league = service.GetLeagueAsync(guildUser.Username + "#" + guildUser.DiscriminatorValue);
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

                await guildUser.ModifyAsync(x =>
                {
                    x.Nickname = $"[FA] {(x.Nickname.IsSpecified ? guildUser.Username : x.Nickname)}";
                });

                await guildUser.AddRoleAsync(roleToAssign).ConfigureAwait(false);

                ITextChannel channel = Context.Guild.GetTextChannel(FAStatusChannel);
                await channel.SendMessageAsync($"{guildUser.Mention} you have been accepted to the IEL!");
                await Context.Channel.SendMessageAsync($"Player {guildUser.Username}#{guildUser.DiscriminatorValue} accepted!");

                List<object> obj = new List<object>();

                obj.Add(true);
                service.PlayerInDiscord(obj, row);

                obj.Clear();

                service.SignupAccepted(obj, row); 

                obj.Add(true);
                obj.Add("");
                obj.Add(true);
                obj.Add(true);
                obj.Add(true);
            }
            else
            {
                await Context.Channel.SendMessageAsync($"Unable to find user {guildUser.Username}#{guildUser.DiscriminatorValue}");
            }
        }
    }
}
