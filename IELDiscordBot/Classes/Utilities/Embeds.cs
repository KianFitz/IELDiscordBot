using Discord;
using IELDiscordBot.Classes.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using static IELDiscordBot.Classes.Modules.DSNModule;
using static IELDiscordBot.Classes.Services.DSNCalculatorService;

namespace IELDiscordBot.Classes.Utilities
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

        internal static Embed PollStreamGame()
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
                Color = Constants.SuccessColor,
                Description =

                $"Hello!\nOne of your upcoming games has been selected to be part of the community vote." +
                $"\n\nIf you do not wish for your game to be streamed, or if there is any change to your scheduling. Please contact the Stream Team Manager." +
                $"\nIf you do not contact us, it will be assumed that you are okay with being on stream and if your team wins the vote you are bound to playing on stream." +
                $"\nStreamed Games are subject to the following section of the IEL Manual: https://tinyurl.com/IELStreamedSeries" +
                $"\n\n Finally, please do not reply to this bot directly, it is not a managed inbox and your message won't be received."
            };

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

        internal static Embed DSNCalculation(List<CalcData> data, string user, string platform)
        {
            int S15Peak = 0;
            int S16Peak = 0;
            int S17Peak = 0;

            for (int season = 15; season < 18; season++)
            {
                int highestVal = 0;
                foreach (var y in data)
                {
                    if (y.Ratings is null)
                        continue;

                    if (y.Season == season) highestVal = Math.Max(highestVal, y.Ratings.Count > 0 ? y.Ratings.Max() : 0);
                    else continue;
                }
                switch (season)
                {
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
                    case 17:
                        {
                            S17Peak = highestVal;
                            break;
                        }
                }
            }

            int peakS = 15;
            int sPeakS = 0;

            int highestPeak = S15Peak;
            int secondHighestPeak = 0;
            if (S16Peak > highestPeak)
            {
                secondHighestPeak = highestPeak;
                sPeakS = 15;
                highestPeak = S15Peak;
            }
            else
            {
                secondHighestPeak = S16Peak;
                sPeakS = 16;
            }
            if (S17Peak > highestPeak)
            {
                secondHighestPeak = highestPeak;
                sPeakS = peakS;
                highestPeak = S17Peak;
                peakS = 17;
            }
            else if (S17Peak > secondHighestPeak)
            {
                secondHighestPeak = S17Peak;
                sPeakS = 17;
            }

            secondHighestPeak = Math.Max(secondHighestPeak, highestPeak - 200);

            if (sPeakS == 15)
                S15Peak = secondHighestPeak;
            else if (sPeakS == 16)
                S16Peak = secondHighestPeak;
            else if (sPeakS == 17)
                S17Peak = secondHighestPeak;

            int s15Games = data.Where(x => x.Season == 15).Sum(x => x.GamesPlayed);
            int s16Games = data.Where(x => x.Season == 16).Sum(x => x.GamesPlayed);
            int s17Games = data.Where(x => x.Season == 17).Sum(x => x.GamesPlayed);

            double dsn = (highestPeak * 0.7) + (secondHighestPeak * 0.3);

            // Dia 1 threshold

            string finalString = $"ID: `{user}`\nPlatform: `{platform}`\n";
            finalString += "\n**Games Played:**\n";
            finalString += $"\n**Season 2: `{s17Games}`**";
            finalString += $"\n**Season 2: `{s16Games}`**";
            finalString += $"\n**Season 1: `{s15Games}`**";
            finalString += $"\n**MMRs:**\n";
            finalString += $"\n**Season 3: `{S17Peak}`**";
            finalString += $"\n**Season 2: `{S16Peak}`**";
            finalString += $"\n**Season 1: `{S15Peak}`**";
            finalString += $"\n**DSN:** `{dsn}`";
#if RELEASE
            finalString += $"\n\n\n**Sheet has been updated.**";
#endif

            EmbedBuilder builder = new EmbedBuilder()
            {
                Color = Constants.SuccessColor,
                Description = finalString
            };
            return builder.Build();
        }


    }
}
