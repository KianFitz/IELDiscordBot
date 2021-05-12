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
        public async Task HandleDSNCommandAsync(int row, params string[] args)
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
                //await message.ModifyAsync(x => x.Content = $"Loading account.. {accounts.IndexOf(acc) + 1} of {accounts.Count}").ConfigureAwait(false);


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
