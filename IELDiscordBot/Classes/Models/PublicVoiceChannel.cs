using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;

namespace IELDiscordBot.Classes.Models
{
    public class PublicVoiceChannel
    {
        internal IVoiceChannel voiceChannel;
        internal List<IGuildUser> moderators;
        internal ulong CreatorId;
    }
}
