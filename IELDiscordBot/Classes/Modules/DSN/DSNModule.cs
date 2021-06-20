using Discord.Commands;
using IELDiscordBot.Classes.Models;
using IELDiscordBot.Classes.Models.DSN;
using IELDiscordBot.Classes.Models.DSN.Segments;
using IELDiscordBot.Classes.Models.TRN;
using IELDiscordBot.Classes.Services;
using IELDiscordBot.Classes.Database;
using IELDiscordBot.Classes.Utilities;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static IELDiscordBot.Classes.Services.DSNCalculatorService;

namespace IELDiscordBot.Classes.Modules
{
    public class DSNModule : ModuleBase<SocketCommandContext>
    {
        Logger _log = LogManager.GetCurrentClassLogger();

        private readonly List<int> _acceptableSeasons = new List<int>() { 14, 15, 16 };
        private readonly List<int> _acceptablePlaylists = new List<int>() { 11, 13 };
        public DSNModule(IELContext db, DSNCalculatorService dsn)
        {
            _db = db;
            _dsn = dsn;
        }

        DateTime[] cutOffDates = new DateTime[]
        {
            new DateTime(2020, 09, 23),
            new DateTime(2020, 12, 09),
            new DateTime(2021, 01, 10)
        };

        private readonly IELContext _db;
        private readonly DSNCalculatorService _dsn;

        [Command("manualpeak")]
        public async Task HandleManualPeakCommandAsync(string platform, string user, int season, int peak)
        {
            ManualPeakOverride manual = _db.ManualPeakOverrides.Find(platform, user, season);
            if (manual is null)
            {
                manual = new ManualPeakOverride();
                manual.Platform = platform;
                manual.User = user;
                manual.Season = season;
                manual.Peak = peak;

                _db.Add(manual);
                await _db.SaveChangesAsync().ConfigureAwait(false);
                await Context.Channel.SendMessageAsync("Added manual peak");
            }
            else
            {
                manual.Peak = peak;
                _db.Entry(manual).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                await _db.SaveChangesAsync().ConfigureAwait(false);
                await Context.Channel.SendMessageAsync("Modified manual peak");
            }
        }
        private string ConvertPlatform(string input)
        {
            input = input.ToLower();
            switch (input)
            {
                case "steam":
                case "pc":
                    return "steam";
                case "xbox":
                case "xb":
                case "xbl":
                    return "xbl";
                case "ps":
                case "ps4":
                case "psn":
                    return "psn";
                case "epic":
                    return "epic";
            }

            return input;
        }

        [Command("dsn")]
        [Name("dsn")]
        private async Task HandleDSNCommand(int row, int profileId)
        {
            //Get Accounts from WebApp
            var r = await _dsn.GetAccountsFromWebApp(profileId);
            //Filter Accounts
            r = r.Where(x => _dsn._allowedPlatforms.Contains(x.type) && x.active).ToArray();

            List<CalcData> CalcData = new List<CalcData>();
            var message = await Context.Channel.SendMessageAsync("Loading...");

            string accountString = "";
            string platformString = "";

            int i = 0;
            //Check data for each account.
            foreach (var account in r)
            {
                i++;
                string username = "";
                await message.ModifyAsync(x =>
                {
                    x.Content = "";
                    x.Embed = Embeds.DSNStatus(i, r.Length, "Getting Account Stats from TRN API");
                });

                account.type = ConvertPlatform(account.type);
                if (account.type == "steam")
                {
                    username = account.id;
                    username = username.Substring(username.LastIndexOf('/') + 1);
                    if (username.EndsWith("/"))
                        username.Remove(username.Length - 1);
                }
                if (account.type == "xbl" || account.type == "psn" || account.type == "epic") username = account.name;

                platformString += account.type + ",";
                accountString += username + ",";
                
                var trnResponse = await _dsn.TRNRequest(account.type, username);
                if (trnResponse is null) continue;
                CalcData.AddRange(trnResponse);
            }

            CalcData = CalcData.Distinct().ToList();
            var orderedData = CalcData.OrderByDescending(x => x.Season);

            await message.ModifyAsync(x =>
            {
                x.Content = "";
                x.Embed = Embeds.DSNCalculation(orderedData.ToList(), accountString, platformString, row);

            }).ConfigureAwait(false);

            if (row == 0) return;
            await _dsn.CalcAndSendResponse(row, CalcData, true);
        }

        [Command("dsn")]
        [Name("dsn")]
        [Summary("Checks the provided accounts on the TRN API, calculates the users DSN, and inputs all information onto the Application & Data Spreadsheet")]
        public async Task HandleDSNCommandAsync(
            [Name("row")][Summary("The row in the spreadsheet to update. Will not update if the value is 0")] int row, 
            [Name("args")][Summary("The list of platform/account names to check against on the TRN API.")]params string[] args)
        {
            if (args.Length % 2 != 0)
            {
                //Invalid Command
                await Context.Channel.SendMessageAsync("Please make sure parameters are sent in multiples of 2.");
                return;
            }

            var message = await Context.Channel.SendMessageAsync("Loading...");

            List<TRNAccount> accounts = new List<TRNAccount>();
            for (int i = 0; i < args.Length; i += 2)
                accounts.Add(new TRNAccount() { Platform = args[i], User = args[i + 1] });

            List<CalcData> calcData = new List<CalcData>();
            int accountsLength = accounts.Count;

            foreach (var acc in accounts)
            {
                string username = acc.User;
                string platform = acc.Platform;

                int accIdx = accounts.IndexOf(acc);

                platform = ConvertPlatform(platform);

                if (platform != "xbl" && platform != "psn" && platform != "steam" && platform != "epic")
                {
                    await message.ModifyAsync(x => x.Content = $"Unable to load account. Platform {platform} invalid.").ConfigureAwait(false);
                    return;
                }

                await message.ModifyAsync(x =>
                {
                    x.Content = "";
                    x.Embed = Embeds.DSNStatus(accIdx, accountsLength, "Getting Account Stats from TRN API");
                });

                var data = await _dsn.TRNRequest(platform, username);
                calcData.AddRange(data);
            }

            calcData = calcData.Distinct().ToList();
            var orderedData = calcData.OrderByDescending(x => x.Season);

            string usernameString = string.Join(',', accounts.Select(x => x.User));
            string platformString = string.Join(',', accounts.Select(x => x.Platform));


            await message.ModifyAsync(x =>
            {
                x.Content = "";
                x.Embed = Embeds.DSNCalculation(orderedData.ToList(), usernameString, platformString, row);

            }).ConfigureAwait(false);


#if RELEASE
            if (row == 0) return;
            await _dsn.CalcAndSendResponse(row, calcData, true).ConfigureAwait(false);
#endif
        }
    }
}
