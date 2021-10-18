using Discord;

namespace IELDiscordBot.Classes.Utilities
{
    internal class Constants
    {
        public const string ConfigFileName = "config.json";
        public const string TRNREDUNDANT = "https://rocketleague.tracker.network/profile/";
        public const string TRNREPLACE = "https://api.tracker.gg/api/v2/rocket-league/standard/profile/";
        public const string TRNAPI = "https://api.tracker.gg/api/v2/rocket-league/standard/profile/{0}/{1}/";
        public const string TRNMMRAPI = "https://api.tracker.gg/api/v2/rocket-league/player-history/mmr/{0}";
        public const string TRNSEGMENTAPI = "https://api.tracker.gg/api/v2/rocket-league/standard/profile/{0}/{1}/segments/playlist?season={2}";


        public static Color SuccessColor = new Color(127, 255, 0);
        public static Color FailureColor = new Color(220, 20, 60);
    }
}
