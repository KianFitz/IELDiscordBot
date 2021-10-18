using Discord;
using System.Collections.Generic;

namespace IELDiscordBot.Classes.Models
{
    public class PublicVoiceChannel
    {
        internal IVoiceChannel voiceChannel;
        internal List<IGuildUser> moderators;
        internal ulong CreatorId;
    }
}
