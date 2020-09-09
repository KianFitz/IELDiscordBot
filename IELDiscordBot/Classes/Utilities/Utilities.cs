using IELDiscordBotPOC.Classes.Models;
using System;
using System.Collections.Generic;

namespace IELDiscordBotPOC.Classes.Utilities
{
    class Utilities
    {
        public static string GetBasePath() => AppContext.BaseDirectory;

        public static List<TeamRequest> OutstandingTeamRequests = new List<TeamRequest>();
    }
}
