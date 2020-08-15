using Discord;
using Discord.Commands;
using Discord.WebSocket;
using IELDiscordBotPOC.Classes.Database;
using IELDiscordBotPOC.Classes.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace IELDiscordBotPOC.Classes.Modules
{
    public class TeamManagementModule : ModuleBase<SocketCommandContext>
    {
        private IELContext _db;
        public TeamManagementModule(IELContext db) : base()
        {
            _db = db;
        }

        #region Team Assignment
        [Command("manageteam")]
        [Alias("mteam", "mt")]
        public async Task HandleManageTeamCommandAsync(string op, string teamName, IUser user = null)
        {
            switch (op)
            {
                case "a":
                case "add":
                    await HandleAddPlayerToTeamAsync(user, teamName);
                    break;
                case "r":
                case "remove":
                    await HandleRemovePlayerFromTeamAsync(user, teamName);
                    break;
            }
        }

        private async Task HandleRemovePlayerFromTeamAsync(IUser user, string teamName)
        {
            SocketGuildUser newUser = (user as SocketGuildUser);
            Team team = _db.Teams.FirstOrDefault(team => team.Name == teamName);
            IRole role = Context.Guild.GetRole(ulong.Parse(team.Role));

            if (team != null)
            {
                if (newUser.Roles.Contains(role))
                {
                    await newUser.RemoveRoleAsync(role).ConfigureAwait(false);
                    await Context.Channel.SendMessageAsync("Successfully removed user from team.").ConfigureAwait(false);
                }
                else
                {
                    await Context.Channel.SendMessageAsync("Could not remove user from team. Reason: User is not on team.").ConfigureAwait(false);
                }
            }
            else
            {
                await Context.Channel.SendMessageAsync("Could not remove user from team. Reason: Team does not exist.").ConfigureAwait(false);
            }
        }

        private async Task HandlePlayerAddConfirm(Team t, IUser u)
        {
            SocketGuildUser user = u as SocketGuildUser;

            var message = await Context.Channel.SendMessageAsync(":one: Prospect Player\r\n:two: Challenger Player\r\n:three: Master Player");
            var messageId = message.Id;

            await message.AddReactionAsync(new Emoji("1️⃣"));
            await message.AddReactionAsync(new Emoji("2️⃣"));
            await message.AddReactionAsync(new Emoji("3️⃣"));

            Utilities.Utilities.OutstandingTeamRequests.Add(new TeamRequest() { MessageId = messageId, Team = t, User = user}) ;
        }

        private async Task HandleAddPlayerToTeamAsync(IUser user, string teamName)
        {
            Team team = _db.Teams.FirstOrDefault(team => team.Name == teamName);
            if (team != null)
            {
                ulong roleId = ulong.Parse(team.Role);
                IRole role = Context.Guild.GetRole(roleId);
                if (role != null)
                {
                    try
                    {
                        if ((user as SocketGuildUser).Roles.Contains(role))
                        {
                            await Context.Channel.SendMessageAsync("Could not add user to team. Reason: User already on team");
                        }
                        await HandlePlayerAddConfirm(team, user).ConfigureAwait(false);
                    }
                    catch(Exception ex)
                    {
                        await Context.Channel.SendMessageAsync($"Failed to add to role: {ex.Message}");
                    }
                }
                else
                {
                    await Context.Channel.SendMessageAsync("Could not find team role.").ConfigureAwait(false);
                }
            }
            else
            {
                    await Context.Channel.SendMessageAsync("Could not find team with that name.").ConfigureAwait(false);
            }
        }
        #endregion
        #region Team Management
        [Command("manageteam")]
        [Alias("mteam", "mt")]
        public async Task HandleManageTeamCommandAsync(string op, string teamName, string abbreviation = null, IRole role = null)
        {
            switch (op)
            {
                case "c":
                case "create":
                    await HandleCreateTeamCommandAsync(teamName, abbreviation, role);
                    break;
                case "d":
                case "delete":
                    await HandleDeleteTeamCommandAsync(teamName);
                    break;
            }
        }

        private async Task HandleCreateTeamCommandAsync(string teamName, string abbreviation, IRole teamRole)
        {
            try
            {
                _db.Teams.Add(new Team() { Name = teamName, Abbreviation = abbreviation, Role = teamRole.Id.ToString() });
                await _db.SaveChangesAsync().ConfigureAwait(false);
                await Context.Channel.SendMessageAsync($"Passed").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await Context.Channel.SendMessageAsync($"Failed: {ex.Message}").ConfigureAwait(false);
            }
        }
        private async Task HandleDeleteTeamCommandAsync(string teamName)
        {
            try
            {
                Team team = _db.Teams.FirstOrDefault(team => team.Name == teamName);
                if (team != null)
                {
                    _db.Remove(team);
                    await _db.SaveChangesAsync().ConfigureAwait(false);
                    await Context.Channel.SendMessageAsync($"Deleted").ConfigureAwait(false);
                }
                else
                {
                    await Context.Channel.SendMessageAsync($"No team found with that name").ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                await Context.Channel.SendMessageAsync($"Failed to delete: {ex.Message}");
            }
        }
        #endregion
    }
}
