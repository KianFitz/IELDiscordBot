using Discord;
using IELDiscordBot.Classes.Services;
using System;
using System.Collections.Generic;
using System.Linq;
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

        internal static Embed UnknownRenameType(string type)
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
                Color = Constants.FailureColor,
                Description = $"{type} is an invalid rename type!." +
                $"\r\nCorrect Syntax is !rename type newName" +
                $" (Your new name can include spaces.)" +
                $"\r\n Valid types are: discord, spreadsheet, both."
            };

            return builder.Build();
        }

        internal static Embed NameTooLong(IGuildUser user, string newName)
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
                Color = Constants.FailureColor,
                Description = $"{user.Mention}, the name you have selected ({newName}) is too long for Discord. Please choose a name with 32 characters or less."
            };

            return builder.Build();
        }

        internal static Embed RequestRename(IUser user, string type, string newNickname)
        {
            EmbedBuilder builder = new EmbedBuilder()
            { 
                Color = Constants.SuccessColor,
                Description = $"{user.Mention} has requested to be renamed to {newNickname} on {type}."
            };
            builder.AddField(new EmbedFieldBuilder() { Name = "Status", Value = "Pending", IsInline = true });

            return builder.Build();
        }

        internal static Embed RequestRename(IUser user, string type, string newNickname, bool accepted, string mention)
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
                Color = accepted ? Constants.SuccessColor : Constants.FailureColor,
                Description = $"{user.Mention} has requested to be renamed to {newNickname} on {type}",
            };
            builder.AddField(new EmbedFieldBuilder() { Name = "Status", Value = $"{(accepted ? "Accepted" : "Denied")} by {mention}", IsInline = true });

            return builder.Build();
        }

        internal static Embed DSNCalculation(List<CalcData> data, string user, string platform, out List<object> obj)
        {
            int S14Peak = 0; //alcData.Where(x => x.Season == 14).Max(y => y.Ratings).First();
            int S15Peak = 0; //alcData.Where(x => x.Season == 15).Max(y => y.Ratings).First();
            int S16Peak = 0; //CalcData.Where(x => x.Season == 16).Max(y => y.Ratings).First();

            for (int season = 14; season < 17; season++)
            {
                int highestVal = 0;
                foreach (var y in data)
                {
                    if (y.Ratings is null)
                        continue;

                    if (y.Season == season)
                    {
                        highestVal = Math.Max(highestVal, y.Ratings.Count > 0 ? y.Ratings.Max() : 0);
                    }
                    else
                    {
                        continue;
                    }
                }
                switch (season)
                {
                    case 14:
                        {
                            S14Peak = highestVal;
                            break;
                        }
                    case 15:
                        {
                            S15Peak = highestVal;
                            break;
                        }
                    case 16:
                        {
                            S16Peak = highestVal;
                            break;
                        }
                }
            }

            int peakS = 14;
            int sPeakS = 0;

            int highestPeak = S14Peak;
            int secondHighestPeak = 0;
            if (S15Peak > highestPeak)
            {
                secondHighestPeak = highestPeak;
                sPeakS = 14;
                highestPeak = S15Peak;
            }
            else
            {
                secondHighestPeak = S15Peak;
                sPeakS = 15;
            }
            if (S16Peak > highestPeak)
            {
                secondHighestPeak = highestPeak;
                sPeakS = peakS;
                highestPeak = S16Peak;
                peakS = 16;
            }
            else if (S16Peak > secondHighestPeak)
            {
                secondHighestPeak = S16Peak;
                sPeakS = 16;
            }

            secondHighestPeak = Math.Max(secondHighestPeak, highestPeak - 200);

            if (sPeakS == 14)
                S14Peak = secondHighestPeak;
            else if (sPeakS == 15)
                S15Peak = secondHighestPeak;
            else if (sPeakS == 16)
                S16Peak = secondHighestPeak;

            int s14Games = data.Where(x => x.Season == 14).Sum(x => x.GamesPlayed);
            int s15Games = data.Where(x => x.Season == 15).Sum(x => x.GamesPlayed);
            int s16Games = data.Where(x => x.Season == 16).Sum(x => x.GamesPlayed);

            double dsn = (highestPeak * 0.7) + (secondHighestPeak * 0.3);

            // Dia 1 threshold

            string finalString = $"ID: `{user}`\nPlatform: `{platform}`\n";
            finalString += "\n**Games Played:**\n";
            finalString += $"\n**Season 16: `{s16Games}`**";
            finalString += $"\n**Season 15: `{s15Games}`**";
            finalString += $"\n**Season 14: `{s14Games}`**";
            finalString += $"\n**MMRs:**\n";
            finalString += $"\n**Season 16: `{S16Peak}`**";
            finalString += $"\n**Season 15: `{S15Peak}`**";
            finalString += $"\n**Season 14: `{S14Peak}`**";
            finalString += $"\n**DSN:** `{dsn}`";
#if RELEASE
            finalString += $"\n\n\n**Sheet has been updated.**";
#endif

            obj = new List<object>();
            obj.Add(s14Games);
            obj.Add(s15Games);
            obj.Add(s16Games);
            obj.Add(null);
            //obj.Add($"=IFS(ISBLANK(K{idx + 1});;AND(K{idx + 1}>=150;AND(J{idx + 1}>=150;I{idx + 1}>=150));\"Games Verified\"; AND(K{idx + 1}<=150;AND(J{idx + 1}>=150;I{idx + 1}>=150));\"Min Games S2 / 16 not reached\"; OR(J{idx + 1}<=150;I{idx + 1}<=150);\"Investigate App\")");
            obj.Add(S14Peak);
            obj.Add(S15Peak);
            obj.Add(S16Peak);

            EmbedBuilder builder = new EmbedBuilder()
            {
                Color = Constants.SuccessColor,
                Description = finalString
            };
            return builder.Build();
        }
    }
}
