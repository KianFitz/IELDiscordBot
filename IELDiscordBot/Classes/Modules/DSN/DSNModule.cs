using Discord.Commands;
using IELDiscordBot.Classes.Database;
using IELDiscordBot.Classes.Models;
using IELDiscordBot.Classes.Models.TRN;
using IELDiscordBot.Classes.Services;
using IELDiscordBot.Classes.Utilities;
using NLog;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static IELDiscordBot.Classes.Services.DSNCalculatorService;

namespace IELDiscordBot.Classes.Modules
{
    public class DSNModule : ModuleBase<SocketCommandContext>
    {
        private readonly Logger _log = LogManager.GetCurrentClassLogger();

        private readonly List<int> _acceptableSeasons = new List<int>() { 14, 15, 16 };
        private readonly List<int> _acceptablePlaylists = new List<int>() { 10, 11, 13 };
        public DSNModule(IELContext db, DSNCalculatorService dsn)
        {
            _db = db;
            _dsn = dsn;
        }

        private readonly DateTime[] cutOffDates = new DateTime[]
        {
            new DateTime(2021, 04, 07),
            new DateTime(2021, 08, 11),
            new DateTime(2021, 09, 01)
        };

        private readonly IELContext _db;
        private readonly DSNCalculatorService _dsn;

        [Command("manualpeak")]
        public async Task HandleManualPeakCommandAsync(string platform, string user, int season, int peak)
        {
            ManualPeakOverride manual = _db.ManualPeakOverrides.Find(platform, user, season);
            if (manual is null)
            {
                manual = new ManualPeakOverride
                {
                    Platform = platform,
                    User = user,
                    Season = season,
                    Peak = peak
                };

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
            ChromeOptions options = new ChromeOptions();
            options.AddArgument("headless");
            options.AddArgument("no-sandbox");
            options.AddArgument("disable-extensions");
            options.AddArgument("disable-gpu");
            options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/84.0.4147.125 Safari/537.36");

            ChromeDriver driver = new ChromeDriver(options);

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

                var trnResponse = await _dsn.TRNRequest(account.type, username, driver);
                if (trnResponse is null)
                {
                    _dsn.MakeRequest($"Player Data Hub!AB{row}", new List<object>() { false });
                    continue;
                }
                CalcData.AddRange(trnResponse);
                i++;
            }

            CalcData = CalcData.Distinct().ToList();
            var orderedData = CalcData.OrderByDescending(x => x.Season);

            await message.ModifyAsync(x =>
            {
                x.Content = "";
                x.Embed = Embeds.DSNCalculation(orderedData.ToList(), accountString, platformString, row);

            }).ConfigureAwait(false);

            driver.Quit();

            if (row == 0) return;
            await _dsn.CalcAndSendResponse(row, CalcData, true);
        }

        [Command("recalcsheet")]
        public async Task HandleRecalcSheetCommand()
        {
            if (Context.User.Id != 260887004005400576 && Context.User.Id != 184340563611353089 && Context.User.Id != 301876830737006593)
                return;

            int rowsToCalculate = _dsn.GetRowsToRecalculate();
            int currentRow = 2;
            var message = await Context.Channel.SendMessageAsync("", false, Embeds.MassCalcSignup()).ConfigureAwait(false);

            DateTime startTime = DateTime.Now;
            for (currentRow = 2; currentRow < rowsToCalculate + 1; currentRow++)
            {
                await _dsn.ForceRecalcRow(currentRow).ConfigureAwait(false);

                TimeSpan timeRemaining = TimeSpan.FromTicks(DateTime.Now.Subtract(startTime).Ticks * (rowsToCalculate - (currentRow + 1)) / (currentRow + 1));

                await message.ModifyAsync(x => x.Embed = Embeds.MassCalcSignup(currentRow, rowsToCalculate, timeRemaining));
            }
        }

        [Command("dsn")]
        [Name("dsn")]
        [Summary("Checks the provided accounts on the TRN API, calculates the users DSN, and inputs all information onto the Application & Data Spreadsheet")]
        public async Task HandleDSNCommandAsync(
            [Name("row")][Summary("The row in the spreadsheet to update. Will not update if the value is 0")] int row,
            [Name("args")][Summary("The list of platform/account names to check against on the TRN API.")] params string[] args)
        {
            if (args.Length % 2 != 0)
            {
                //Invalid Command
                await Context.Channel.SendMessageAsync("Please make sure parameters are sent in multiples of 2.");
                return;
            }

            var message = await Context.Channel.SendMessageAsync("Loading...");

            ChromeOptions options = new ChromeOptions();
            options.AddArgument("headless");
            options.AddArgument("no-sandbox");
            options.AddArgument("disable-extensions");
            options.AddArgument("disable-gpu");
            options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/84.0.4147.125 Safari/537.36");

            ChromeDriver driver = new ChromeDriver(options);

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

                var data = await _dsn.TRNRequest(platform, username, driver);
                if (data is null)
                {
                    _dsn.MakeRequest($"Player Data Hub!AB{row}", new List<object>() { false });
                    continue;
                }
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
