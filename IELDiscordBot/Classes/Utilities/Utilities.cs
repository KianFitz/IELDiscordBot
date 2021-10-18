using IELDiscordBot.Classes.Models;
using System;
using System.Collections.Generic;

namespace IELDiscordBot.Classes.Utilities
{
    internal class Utilities
    {
        public static string GetBasePath()
        {
            return AppContext.BaseDirectory;
        }

        public static List<TeamRequest> OutstandingTeamRequests = new List<TeamRequest>();
    }
}
