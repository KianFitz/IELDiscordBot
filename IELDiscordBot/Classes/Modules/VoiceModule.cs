using Discord;
using Discord.Commands;
using Discord.WebSocket;
using IELDiscordBot.Classes.Services;
using System.Threading.Tasks;

namespace IELDiscordBot.Classes.Modules
{
    [Group("voice")]
    public class VoiceModule : ModuleBase<SocketCommandContext>
    {
        private readonly VoiceService _service;
        public VoiceModule(VoiceService service)
        {
            _service = service;
        }

        [Command("limit")]
        public async Task SetLimit(int limit)
        {
            if (!IsUserAModeratorInCurrentVoiceChannel(Context.User))
            {
                await GiveNoPermissionError(Context.User);
                return;
            }

            await _service.SetLimit(Context.User, limit);
        }

        [Command("rename")]
        public async Task RenameVoiceChannel(string newName)
        {
            if (!IsUserAModeratorInCurrentVoiceChannel(Context.User))
            {
                await GiveNoPermissionError(Context.User);
                return;
            }

            await _service.RenameChannel(Context.User, newName);
        }

        [Command("lock")]
        public async Task LockVoiceChannel(bool active)
        {
            if (!IsUserAModeratorInCurrentVoiceChannel(Context.User))
            {
                await GiveNoPermissionError(Context.User);
                return;
            }

            await _service.LockChannel(Context.User, active);
        }

        [Command("adduser")]
        public async Task AddUser(IGuildUser user)
        {
            if (!IsUserAModeratorInCurrentVoiceChannel(Context.User))
            {
                await GiveNoPermissionError(Context.User);
                return;
            }

            await _service.AddUser(Context.User, user);
        }

        [Command("removeuser")]
        public async Task RemoveUser(IGuildUser user)
        {
            if (!IsUserAModeratorInCurrentVoiceChannel(Context.User))
            {
                await GiveNoPermissionError(Context.User);
                return;
            }

            await _service.RemoveUser(Context.User, user);
        }

        [Command("kickuser")]
        public async Task KickUser(IGuildUser user)
        {
            if (!IsUserAModeratorInCurrentVoiceChannel(Context.User))
            {
                await GiveNoPermissionError(Context.User);
                return;
            }

            await _service.KickUser(Context.User, user);
        }

        [Command("muteuser")]
        public async Task MuteUser(IGuildUser user, bool active)
        {
            if (!IsUserAModeratorInCurrentVoiceChannel(Context.User))
            {
                await GiveNoPermissionError(Context.User);
                return;
            }

            await _service.MuteUser(Context.User, user, active);
        }

        [Command("addmod")]
        public async Task AddModerator(IGuildUser user)
        {
            if (!IsUserAModeratorInCurrentVoiceChannel(Context.User))
            {
                await GiveNoPermissionError(Context.User);
                return;
            }

            _service.AddModerator(Context.User, user);
        }

        [Command("removemod")]
        public async Task RemoveModerator(IGuildUser user)
        {
            if (!IsUserAModeratorInCurrentVoiceChannel(Context.User))
            {
                await GiveNoPermissionError(Context.User);
                return;
            }

            _service.RemoveModeratorFromChannel(Context.User, user);
        }

        private async Task GiveNoPermissionError(SocketUser user)
        {
            await Context.Channel.SendMessageAsync($"{user.Mention}. You do not have access to modify this channel.");
        }

        private bool IsUserAModeratorInCurrentVoiceChannel(SocketUser user)
        {
            SocketGuildUser usr = Context.Guild.GetUser(user.Id);

            IVoiceChannel channel = usr.VoiceChannel;
            if (!_service.IsCustomChannel(channel.Id)) return false;
            return _service.IsModeratorInChannel(channel, usr);
        }
    }
}
