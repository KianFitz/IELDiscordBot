using Discord.WebSocket;

namespace IELDiscordBot.Classes.Models
{
    public class Team
    {
        public int ID;
        public string Name;
        public string Abbreviation;
        public string Role;
        public string CaptainID;
    }

    public class TeamRequest
    {
        public ulong MessageId;
        public Team Team;
        public SocketGuildUser User;
    }
}
