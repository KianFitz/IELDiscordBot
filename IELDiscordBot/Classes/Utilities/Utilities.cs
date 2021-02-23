using IELDiscordBot.Classes.Models;
using System;
using System.Collections.Generic;

namespace IELDiscordBot.Classes.Utilities
{
    class Utilities
    {
        public static string GetBasePath() => AppContext.BaseDirectory;

        public static List<TeamRequest> OutstandingTeamRequests = new List<TeamRequest>();
    }
}
