using Discord;
using Discord.WebSocket;
using IELDiscordBot.Classes.Database;
using IELDiscordBot.Classes.Models;
using NLog;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IELDiscordBot.Classes.Services
{
    public class VoiceService
    {
        private readonly List<PublicVoiceChannel> _customVoiceChannels;

        private readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly IELContext _db;

        public VoiceService(IELContext db)
        {
            _db = db;
            _customVoiceChannels = new List<PublicVoiceChannel>();
        }

        public bool IsCustomChannel(ulong id)
        {
            return _customVoiceChannels.Any(x => x.voiceChannel.Id == id);
        }

        public async Task DeleteVoiceChannel(ulong channelId)
        {
            PublicVoiceChannel voiceChannel = _customVoiceChannels.FirstOrDefault(x => x.voiceChannel.Id == channelId);

            if (voiceChannel != null && voiceChannel.voiceChannel != null)
            {
                _log.Info($"Channel created by {voiceChannel.CreatorId} has no users left in it. Deleting now.");
                await voiceChannel.voiceChannel.DeleteAsync();

                _customVoiceChannels.Remove(voiceChannel);
            }
        }

        public async Task CreateNewVoiceChannel(IGuild guild, ICategoryChannel category, IGuildUser creator, IVoiceChannel initial)
        {
            PublicVoiceChannel newChannel = new PublicVoiceChannel
            {
                moderators = new List<IGuildUser>() { creator }
            };

            _log.Info($"Creating new Voice Channel for user {creator.Id}");
            var voiceChannel = await guild.CreateVoiceChannelAsync($"{creator.Username}#{creator.Discriminator}",
                func =>
                {
                    func.CategoryId = category.Id;
                });

            newChannel.voiceChannel = voiceChannel;
            newChannel.CreatorId = creator.Id;

            _log.Info($"Moved user to Voice Channel {voiceChannel.Id}");
            await creator.ModifyAsync(x =>
            {
                x.ChannelId = voiceChannel.Id;
            });

            _customVoiceChannels.Add(newChannel);
        }

        internal async Task AddUser(SocketUser user1, IGuildUser user2)
        {
            PublicVoiceChannel channel = GetVoiceChannel(user1);
            if (channel is null) return;

            await channel.voiceChannel.AddPermissionOverwriteAsync(user2,
            new OverwritePermissions().Modify(null, null, null, null, null, null, null, null, null, null, null, null, PermValue.Allow));
        }

        public PublicVoiceChannel GetVoiceChannel(SocketUser user)
        {
            if (user is IGuildUser)
            {
                return (_customVoiceChannels.First(x => x.voiceChannel.Id == (user as SocketGuildUser).VoiceChannel.Id));
            }

            return null;
        }

        internal async Task RemoveUser(SocketUser user1, IGuildUser user2)
        {
            PublicVoiceChannel channel = GetVoiceChannel(user1);
            if (channel is null) return;

            await channel.voiceChannel.RemovePermissionOverwriteAsync(user2).ConfigureAwait(false);
        }

        internal async Task LockChannel(SocketUser user, bool active)
        {
            PublicVoiceChannel channel = GetVoiceChannel(user);
            if (channel is null) return;

            IRole everyoneRole = channel.voiceChannel.Guild.Roles.First(x => x.Name == "@everyone");

            if (active)
                await channel.voiceChannel.AddPermissionOverwriteAsync(everyoneRole,
                new OverwritePermissions().Modify(null, null, null, null, null, null, null, null, null, null, null, null, PermValue.Deny));
            else
                await channel.voiceChannel.RemovePermissionOverwriteAsync(everyoneRole);
        }

        internal void RemoveModeratorFromChannel(SocketUser user1, IGuildUser user)
        {
            PublicVoiceChannel channel = GetVoiceChannel(user1);
            if (channel is null) return;

            channel.moderators.Remove(user);
        }

        internal async Task KickUser(SocketUser user1, IGuildUser user2)
        {
            PublicVoiceChannel channel = GetVoiceChannel(user1);
            if (channel is null) return;

            if (user2.VoiceChannel.Id == channel.voiceChannel.Id)
                await user2.ModifyAsync(x => x.Channel = null);
        }

        internal async Task MuteUser(SocketUser user1, IGuildUser user2, bool active)
        {
            PublicVoiceChannel channel = GetVoiceChannel(user1);
            if (channel is null) return;

            await user2.ModifyAsync(x => x.Mute = active);
        }

        internal void AddModerator(SocketUser user1, IGuildUser user2)
        {
            PublicVoiceChannel channel = GetVoiceChannel(user1);
            if (channel is null) return;

            channel.moderators.Add(user2);
        }

        internal async Task SetLimit(SocketUser user, int limit)
        {
            PublicVoiceChannel channel = GetVoiceChannel(user);
            if (channel is null) return;

            await channel.voiceChannel.ModifyAsync(x => x.UserLimit = limit);
        }

        internal async Task RenameChannel(SocketUser user, string newName)
        {
            PublicVoiceChannel channel = GetVoiceChannel(user);
            if (channel is null) return;

            await channel.voiceChannel.ModifyAsync(x => x.Name = newName);
        }

        internal bool IsModeratorInChannel(IVoiceChannel channel, IGuildUser user)
        {
            PublicVoiceChannel vc = _customVoiceChannels.First(x => x.voiceChannel.Id == channel.Id);
            return vc.moderators.Any(x => x == user);
        }
    }
}
