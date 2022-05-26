using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using static IELDiscordBot.Classes.Modules.ArchiveModule;
using static IELDiscordBot.Classes.Services.DSNCalculatorService;

namespace IELDiscordBot.Classes.Utilities
{
    internal class Embeds
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

        internal static Embed Archiving(SocketUser user, ITextChannel channel, string status)
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
                Color = Constants.SuccessColor
            };

            builder.AddField(new EmbedFieldBuilder() { Name = $"User", Value = user.Mention });
            builder.AddField(new EmbedFieldBuilder() { Name = $"Channel", Value = channel.Mention });
            builder.AddField(new EmbedFieldBuilder() { Name = $"Status", Value = status });

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

        internal static Embed ArchiveSearch(List<Info> infos)
        {
            EmbedBuilder emb = new EmbedBuilder()
            {
                Color = Constants.SuccessColor
            };
            string desc = "";

            foreach (var i in infos)
                desc += $"**Category**: {i.Category} - **Name**: {i.Name} - **Links**: [Dark]({i.GetUrl(true)}) [Light]({i.GetUrl(false)}) \n";

            if (desc == "")
                desc = "No archives with that name.";

            desc = desc.Replace(".html", "");

            emb.Description = desc;

            return emb.Build();
        }

        internal static Embed DSNCalculation(List<CalcData> data, string user, string platform, int row)
        {
            int highestSeason = 0;
            int highestPeak = 0;

            // Highest Season
            for (int i = 0; i < data.Count; i++)
            {
                if (data[i].Ratings == null || data[i].Ratings.Count == 0) continue;
                int maxPeakFromSeason = data[i].Ratings.Max();
                if (maxPeakFromSeason > highestPeak)
                {
                    highestSeason = data[i].Season;
                    highestPeak = maxPeakFromSeason;
                }
            }

            int secondHighestPeak = 0;
            int secondHighestSeason = 0;

            // Second Highest Season
            var tmp = data.Except(data.Where(x => x.Season == highestSeason));
            for (int i = 0; i < data.Count; i++)
            {
                if (data[i].Ratings == null || data[i].Ratings.Count == 0) continue;
                int maxPeakFromSeason = data[i].Ratings.Max();
                if (maxPeakFromSeason > secondHighestPeak)
                {
                    secondHighestSeason = data[i].Season;
                    secondHighestPeak = maxPeakFromSeason;
                }
            }

            int dsn = 0;

            if (highestPeak - secondHighestPeak > 100)
            {
                dsn = (int)Math.Round((highestPeak * 0.9) + (secondHighestPeak * 0.1));
            }

            List<int> peaks = new List<int>();

            for (int season = Constants.START_SEASON; season <= Constants.END_SEASON; season++)
            {
                int peak = 0;

                foreach (var section in data)
                {
                    if (section.Ratings is null || section.Ratings.Count == 0) continue;

                    if (section.Season == season)
                    {
                        var i = section.Ratings.Max();
                        if (i > peak) peak = i;
                    }
                }

                peaks.Add(peak);
            }

            int average = 0;
            if (peaks.Count() != 0)
            {
                //peaks.RemoveAll(x => x == 0);
                average = (int)Math.Round(peaks.Average());
            }

            if (dsn == 0)
            {
                dsn = (int)Math.Round((highestPeak * 0.7) + (secondHighestPeak * 0.1) + (average * 0.2));
            }

            int s2games = data.Where(x => x.Season == 16).Sum(x => x.GamesPlayed);
            int s3games = data.Where(x => x.Season == 17).Select(x => x.GamesPlayed).Distinct().Sum();
            int s4games = data.Where(x => x.Season == 18).Select(x => x.GamesPlayed).Distinct().Sum();
            int s5games = data.Where(x => x.Season == 19).Select(x => x.GamesPlayed).Distinct().Sum();
            int s6games = data.Where(x => x.Season == 20).Select(x => x.GamesPlayed).Distinct().Sum();

            string finalString = $"ID: `{user}`\nPlatform: `{platform}`\n";
            finalString += "\n**Games Played:**\n";
            finalString += $"\n**Season 6: `{s6games}`**";
            finalString += $"\n**Season 5: `{s5games}`**";
            finalString += $"\n**Season 4: `{s4games}`**";
            finalString += $"\n**Season 3: `{s3games}`**";
            finalString += $"\n**Season 2: `{s2games}`**\n";

            finalString += $"\n**MMRs:**\n";
            finalString += $"\n**Season 6: `{peaks[4]}`**";
            finalString += $"\n**Season 5: `{peaks[3]}`**";
            finalString += $"\n**Season 4: `{peaks[2]}`**";
            finalString += $"\n**Season 3: `{peaks[1]}`**";
            finalString += $"\n**Season 2: `{peaks[0]}`**";
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
                case "Notify of Acceptance":
                    status = "Awaiting Acceptance";
                    break;
                case "Pending":
                    status = "Awaiting Calculation";
                    break;
                case "Notify of Denial":
                    status = "Under Investigation";
                    break;
                case "Application Accepted":
                    status = "Accepted";
                    break;
                case "Retired Application":
                    status = "Retired";
                    break;
                default:
                    status = "Unknown";
                    break;
            }


            builder.AddField(new EmbedFieldBuilder() { Name = "Profile Name", Value = profileName });
            builder.AddField(new EmbedFieldBuilder() { Name = "Discord Id", Value = id });
            builder.AddField(new EmbedFieldBuilder() { Name = "Profile Link", Value = profileLink });
            builder.AddField(new EmbedFieldBuilder() { Name = "Application Status", Value = status });
            builder.AddField(new EmbedFieldBuilder() { Name = "Accounts Linked", Value = platformLinks });

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
                string.Join("\r\n", _assignedCounters.Select(x => x.Key + ": " + x.Value))
                + $"\r\n\r\nETA: {remaining * 1.5} seconds."
            };
            return builder.Build();
        }
    }
}
