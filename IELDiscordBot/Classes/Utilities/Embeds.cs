using Discord;
using Discord.Commands;
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

        internal static Embed DSNCalculation(List<CalcData> data, string user, string platform, int row)
        {
            int S16Peak = 0;
            int S17Peak = 0;
            int S18Peak = 0;

            for (int season = 16; season < 19; season++)
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
                    case 18:
                        {
                            S18Peak = highestVal;
                            break;
                        }
                }
            }

            int peakS = 16;
            int sPeakS = 0;

            int highestPeak = S16Peak;
            int secondHighestPeak = 0;
            if (S17Peak > highestPeak)
            {
                secondHighestPeak = highestPeak;
                sPeakS = 15;
                highestPeak = S16Peak;
            }
            else
            {
                secondHighestPeak = S17Peak;
                sPeakS = 16;
            }
            if (S18Peak > highestPeak)
            {
                secondHighestPeak = highestPeak;
                sPeakS = peakS;
                highestPeak = S18Peak;
                peakS = 17;
            }
            else if (S18Peak > secondHighestPeak)
            {
                secondHighestPeak = S18Peak;
                sPeakS = 17;
            }

            secondHighestPeak = Math.Max(secondHighestPeak, highestPeak - 200);

            if (sPeakS == 16)
                S16Peak = secondHighestPeak;
            else if (sPeakS == 17)
                S17Peak = secondHighestPeak;
            else if (sPeakS == 18)
                S18Peak = secondHighestPeak;

            int s16Games = data.Where(x => x.Season == 16).Sum(x => x.GamesPlayed);
            int s17Games = data.Where(x => x.Season == 17).Select(x => x.GamesPlayed).Distinct().Sum();
            int s18Games = data.Where(x => x.Season == 18).Select(x => x.GamesPlayed).Distinct().Sum();

            double dsn = (highestPeak * 0.7) + (secondHighestPeak * 0.3);

            // Dia 1 threshold

            string finalString = $"ID: `{user}`\nPlatform: `{platform}`\n";
            finalString += "\n**Games Played:**\n";
            finalString += $"\n**Season 4: `{s18Games}`**";
            finalString += $"\n**Season 3: `{s17Games}`**";
            finalString += $"\n**Season 2: `{s16Games}`**";
            finalString += $"\n**MMRs:**\n";
            finalString += $"\n**Season 4: `{S18Peak}`**";
            finalString += $"\n**Season 3: `{S17Peak}`**";
            finalString += $"\n**Season 2: `{S16Peak}`**";
            finalString += $"\n**DSN:** `{dsn}`";
#if RELEASE
            if (row != 0)
                finalString += $"\n\n\n**Sheet has been updated.**";
#endif

            EmbedBuilder builder = new EmbedBuilder()
            {
                Color = Constants.SuccessColor,
                Description = finalString
            };
            return builder.Build();
        }

        internal static Embed NoSignup(ulong discordId)
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
                Color = Constants.SuccessColor,
                Description = $"<@{discordId}>, I could not find an application for your Discord ID. Please ensure that you were logged in with the correct Discord Account when signing up."
            };
            return builder.Build();
        }

        internal static Embed SignupDetails(string profileName, string id, string profileLink, string status, string platformLinks, ulong requestor)
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
                Color = Constants.SuccessColor,
            };

            switch (status)
            {
                case "Notify of Approval":
                    status = "Awaiting Acceptance";
                    break;
                case "Pending":
                    status = "Awaiting Calculation";
                    break;
                case "Requirements not reached":
                    status = "Denied";
                    break;
                case "Notify of Denied Signup":
                    status = "Awaiting Denial";
                    break;
                case "Approved and Notified":
                    status = "Accepted";
                    break;
                case "Investigate App":
                case "Issue":
                case "Missing Data":
                    status = "Missing Data/Information/Signup Incomplete";
                    break;
            }


            builder.AddField(new EmbedFieldBuilder() { Name = "Profile Name", Value = profileName });
            builder.AddField(new EmbedFieldBuilder() { Name = "Discord Id", Value = id });
            builder.AddField(new EmbedFieldBuilder() { Name = "Profile Link", Value = profileLink});
            builder.AddField(new EmbedFieldBuilder() { Name = "Application Status", Value = status });
            builder.AddField(new EmbedFieldBuilder() { Name = "Accounts Linked", Value = platformLinks});

            builder.Description = "Here are the details of your application. If you have any questions please ask a member of the Support or Applications teams.";
            if (status == "Missing Data/Information/Signup Incomplete" && id == requestor.ToString())
                builder.Description += "\r\nIf you were denied due to your games played, please type !rechecksignup to have your games recounted.";

            return builder.Build();
        }

        internal static Embed CommandHelp(CommandMatch foundCommand)
        {
            string syntax = $"!{foundCommand.Command.Name}";
            string parameterText = $"";


            EmbedBuilder builder = new EmbedBuilder()
            {
                Color = Constants.SuccessColor,
                Description = "I found the following command that matched your search query."
            };
            builder.Fields.Add(new EmbedFieldBuilder() { Name = "Command Name", Value = foundCommand.Command.Name });
            builder.Fields.Add(new EmbedFieldBuilder() { Name = "Summary", Value = foundCommand.Command.Summary });

            foreach (var param in foundCommand.Command.Parameters)
            {
                parameterText += $"**{param.Name}** - Type: **{param.Type.Name}** - {param.Summary}\r\n";
                syntax += "{" + param.Name + "}";
            }

            builder.Fields.Add(new EmbedFieldBuilder() { Name = "Syntax", Value = syntax });
            builder.Fields.Add(new EmbedFieldBuilder() { Name = "Parameters", Value = parameterText });

            return builder.Build();
        }

        internal static Embed CommandNotFound(string command)
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
                Color = Constants.FailureColor,
                Description = $"Sorry, I was not able to find any commands with the name {command}. Please check your spelling and try again."
            };
            return builder.Build();
        }

        internal static Embed SignupRecalculated(ulong discordId)
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
                Color = Constants.SuccessColor,
                Description = $"<@{discordId}> your games have been rechecked. Please wait up to 48 hours from running this command to hear back about your application." +
                $"\r\nIf you do not hear from us in that time, please check your application again here with the !signup command"
            };
            return builder.Build();
        }

        internal static Embed ErrorLog(string errorLog)
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
                Color = Constants.SuccessColor,
                Description = $"The following errors occured during assigning the roles: {errorLog}"
            };
            return builder.Build();
        }

        internal static Embed AssigningLeagueRoles(int remaining, Dictionary<string, int> _assignedCounters)
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
                Color = Constants.SuccessColor,
                Description = $"There are {remaining} players left to assign. \r\n" +
                $"Current Stats:" +
                string.Join("\r\n", _assignedCounters.Select(x => x.Key +": "+ x.Value))
                + $"\r\n\r\nETA: {remaining * 1.5} seconds."
            };
            return builder.Build();
        }
    }
}
