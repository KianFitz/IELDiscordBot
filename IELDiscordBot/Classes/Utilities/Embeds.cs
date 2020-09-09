using Discord;
using System.Collections.Generic;
using static IELDiscordBot.Classes.Modules.DSNModule;

namespace IELDiscordBotPOC.Classes.Utilities
{
    class Embeds
    {
        internal static Embed NewFreeAgent()
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
            };

            return builder.Build();
        }

        internal static Embed WelcomeToIEL()
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
            };

            return builder.Build();
        }

        internal static Embed DSNStatus(int accountIndex, int maxAccounts, string currentStatus)
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
                Color = Constants.SuccessColor,
                Description = $"Loading Account {accountIndex + 1} of {maxAccounts}.\r\n" +
                $"Current Status: {currentStatus}"
            };
            return builder.Build();
        }

        internal static Embed DSNError(string platform, string account, string currentStatus)
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
                Color = Constants.FailureColor,
                Description = $"Error Loading Account {account} on Platform {platform}.\r\n" +
                $"Error: {currentStatus}"
            };
            return builder.Build();
        }

        internal static Embed DSNCalculation(List<DSNCalculationData> data, string user, string platform)
        {
            int mmr1 = 0;
            int mmr2 = 0;
            double dsn = 0;
            string gp = "";
            string mmr = "";
            foreach (var d in data)
            {
                gp += $"Season {d.Season}: `{d.GamesPlayed}`\n";
                mmr += $"Season {d.Season}: `{d.MaxMMR}`\n";

                if (d.MaxMMR > mmr1)
                {
                    if (mmr1 > mmr2)
                        mmr2 = mmr1;

                    mmr1 = d.MaxMMR;
                }
                else if (d.MaxMMR > mmr2)
                    mmr2 = d.MaxMMR;
            }

            if (mmr2 > 0)
                dsn = ((mmr1 * 0.7) + (mmr2 * 0.3));
            else
                dsn = (mmr1 * 0.7);

            // Dia 1 threshold
            if (mmr1 >= 935)
            {
                if (dsn < 1100)
                    dsn = 1100;
            }
            else
                dsn = 0;

            string finalString = $"ID: `{user}`\nPlatform: `{platform}`\n";
            finalString += "\n**Games Played:**\n";
            finalString += gp;
            finalString += "\n**MMRs:**\n";
            finalString += mmr;
            finalString += $"\n**DSN:** `{(dsn > 0 ? dsn.ToString() : "Illegal. Player has not reached Diamond 1!")}`";
            if (mmr2 == 0)
                finalString += $"\n**Only 1 season peak found!!**";


            EmbedBuilder builder = new EmbedBuilder()
            {
                Color = Constants.SuccessColor,
                Description = finalString
            };
            return builder.Build();
        }
    }
}
