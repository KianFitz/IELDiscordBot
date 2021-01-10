//using Discord;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using IELDiscordBot.Classes.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
//using Discord.WebSocket;
//using System.Threading.Tasks;

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
        ulong FARole = 497676708481335298;

        public UserModule(DSNCalculatorService service)
        {
            this.service = service;
        }

        public async Task AcceptPlayerAsync(IGuildUser mentionedUser)
        {
            SocketGuildUser guildUser = (mentionedUser as SocketGuildUser);

            string league = service.GetLeagueAsync(guildUser.Username + "#" + guildUser.DiscriminatorValue);

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

            IRole role = Context.Guild.GetRole(FARole);

            List<IRole> rolesToAssign = new List<IRole>();
            rolesToAssign.Add(roleToAssign);
            rolesToAssign.Add(role);

            await guildUser.AddRolesAsync(rolesToAssign).ConfigureAwait(false);

            ITextChannel channel = Context.Guild.GetTextChannel(FAStatusChannel);
            await channel.SendMessageAsync($"{guildUser.Mention} you have been accepted to the IEL!");
        }
    }
}
