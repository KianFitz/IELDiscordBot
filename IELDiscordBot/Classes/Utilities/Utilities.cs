using IELDiscordBot.Classes.Models;
using IELDiscordBotPOC.Classes.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace IELDiscordBotPOC.Classes.Utilities
{
    class Utilities
    {
        public static string GetBasePath() => AppContext.BaseDirectory;

        public static List<TeamRequest> OutstandingTeamRequests = new List<TeamRequest>();
        public static List<Captcha> OutstandingCaptchas = new List<Captcha>();
    }
}
