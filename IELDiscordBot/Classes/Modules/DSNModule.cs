using Discord.Commands;
using IELDiscordBot.Classes.Models;
using IELDiscordBot.Classes.Models.DSN;
using IELDiscordBot.Classes.Models.DSN.Segments;
using IELDiscordBot.Classes.Models.TRN;
using IELDiscordBot.Classes.Services;
using IELDiscordBotPOC.Classes.Database;
using IELDiscordBotPOC.Classes.Utilities;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
        internal enum Playlist
        {
            TWOS = 13,
            THREES = 14,
        }

        enum Seasons
        {
            S14 = 0,
            S15 = 1,
            S16 = 2
        }

        DateTime[] cutOffDates = new DateTime[]
        {
            new DateTime(2020, 09, 23),
            new DateTime(2020, 12, 09),
            new DateTime(2021, 01, 10)
        };

        private readonly IELContext _db;
        private readonly DSNCalculatorService _dsn;

        internal struct CalcData
        {
            internal int Season;
            internal Playlist Playlist;
            internal List<int> Ratings;
            internal int GamesPlayed;
        }

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

                if (platform != "xbl" && platform != "psn" && platform != "steam")
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


                using (HttpClient client = new HttpClient())
                {
                    string apistring = string.Format(Constants.TRNAPI, platform, username);

                    HttpResponseMessage response = await client.GetAsync(apistring).ConfigureAwait(false);

                    string content = await response.Content.ReadAsStringAsync();

                    if (string.IsNullOrEmpty(content)) return;
                    if (content.ToLower().Contains("we could not find the player"))
                        return;

                    TRNObject obj = null;
                    try
                    {
                        obj = JsonConvert.DeserializeObject<TRNObject>(content);
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex);
                        return;
                    }

                    int playerId = obj.data.metadata.playerId;

                    await message.ModifyAsync(x =>
                    {
                        x.Content = "";
                        x.Embed = Embeds.DSNStatus(accIdx, accountsLength, $"Account Found. Player ID: {playerId}");
                    });

                    apistring = string.Format(Constants.TRNMMRAPI, playerId);
                    response = await client.GetAsync(apistring);
                    content = await response.Content.ReadAsStringAsync();
                    content = MakeJSONFriendly(content);

                    TRNMMRObject mmrObj = JsonConvert.DeserializeObject<TRNMMRObject>(content);

                    List<CalcData> Data = new List<CalcData>
                    {
                        await GetCalcDataForSegmentAsync(platform, username, 14, Playlist.TWOS, mmrObj),
                        await GetCalcDataForSegmentAsync(platform, username, 14, Playlist.THREES, mmrObj),
                        await GetCalcDataForSegmentAsync(platform, username, 15, Playlist.TWOS, mmrObj),
                        await GetCalcDataForSegmentAsync(platform, username, 15, Playlist.THREES, mmrObj),
                        await GetCalcDataForSegmentAsync(platform, username, 16, Playlist.TWOS, mmrObj),
                        await GetCalcDataForSegmentAsync(platform, username, 16, Playlist.THREES, mmrObj)
                    };

                    await message.ModifyAsync(x =>
                    {
                        x.Content = "";
                        x.Embed = Embeds.DSNStatus(accountsLength, accountsLength, $"Calculating...");
                    });

                    calcData.AddRange(Data);
                }
            }

            var orderedData = calcData.OrderByDescending(x => x.Season);

            string usernameString = string.Join(',', accounts.Select(x => x.User));
            string platformString = string.Join(',', accounts.Select(x => x.Platform));

            List<object> obj1 = new List<object>();

            await message.ModifyAsync(x =>
            {
                x.Content = "";
                x.Embed = Embeds.DSNCalculation(orderedData.ToList(), usernameString, platformString, out List<object> obj);

                obj1.AddRange(obj);

            }).ConfigureAwait(false);

            //obj1[3] = $"=IFS(ISBLANK(K{row});;OR(K{row}<20;J{row}<20;I{row}<20);\"Investigate App\";OR(K{row}>=150;J{row}>=200;I{row}>= 350);\"Games Verified\";AND(K{row}<150;J{row}<200;I{row}<350); \"Min Games not reached\")";
            //obj1[3] = $"=IFS(ISBLANK(K{row});;AND(K{row}>=150;AND(J{row}>=150;I{row}>=150));\"Games Verified\"; AND(K{row}<=150;AND(J{row}>=150;I{row}>=150));\"Min Games S2 / 16 not reached\"; OR(J{row}<=150;I{row}<=150);\"Investigate App\")";

#if RELEASE
            //await _service.UpdateSpreadSheet(obj1, row);
            obj1[3] = $"=IFS(ISBLANK(K{row});;OR(K{row}<20;J{row}<20;I{row}<20);\"Investigate App\";OR(K{row}>=150;J{row}>=200;I{row}>= 350);\"Games Verified\";AND(K{row}<150;J{row}<200;I{row}<350); \"Min Games not reached\")";
            string sectionToEdit = $"DSN Hub!I{row}";
            await _dsn.MakeRequest(sectionToEdit, obj1);
#endif
            //await Context.Channel.SendMessageAsync("", false, Embeds.DSNCalculation(orderedData.ToList(), usernameString, platformString)).ConfigureAwait(false);
        }

        private async Task<TRNSegment> GetSeasonSegment(int season, string platform, string user)
        {
            using (HttpClient client = new HttpClient())
            {
                string apiString = string.Format(Constants.TRNSEGMENTAPI, platform, user, season);
                var response = await client.GetAsync(apiString);

                var responseString = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<TRNSegment>(responseString);
            }
        }

        async Task<CalcData> GetCalcDataForSegmentAsync(string platform, string username, int season, Playlist playlist, TRNMMRObject obj)
        {
            CalcData retVal = new CalcData();

            retVal.Playlist = playlist;
            retVal.Season = season;

            DateTime cutOff = DateTime.Now;
            DateTime seasonStartDate = new DateTime(2020, 01, 01);

            switch (season)
            {
                case 14:
                    {
                        cutOff = cutOffDates[(int)Seasons.S14];
                        break;
                    }
                case 15:
                    {
                        cutOff = cutOffDates[(int)Seasons.S15];
                        seasonStartDate = cutOffDates[(int)Seasons.S14].AddDays(1);
                        break;
                    }
                case 16:
                    {
                        cutOff = cutOffDates[(int)Seasons.S16];
                        seasonStartDate = cutOffDates[(int)Seasons.S15].AddDays(1);
                        break;
                    }
            }

            List<Datum> Datam = new List<Datum>();
            var segment = await GetSeasonSegment(season, platform, username);
            if (segment == null)
                retVal.GamesPlayed = 0;
            else
            {
                Datam.AddRange(segment.data);
                Datam.RemoveAll(x => _acceptablePlaylists.Contains(x.attributes.playlistId) == false);
                retVal.GamesPlayed = Datam.Count > 0 ? Datam.Sum(x => x.stats.matchesPlayed.value) : 0;
            }


            if (playlist == Playlist.TWOS)
            {
                if (obj.data.Duos != null)
                {
                    List<Duo> data = new List<Duo>(obj.data.Duos);
                    data = data.Where(x => x.collectDate < cutOff && x.collectDate > seasonStartDate).ToList();
                    retVal.Ratings = data.Select(x => x.rating).ToList();
                    retVal.GamesPlayed = Datam.Count > 0 ? Datam[0].stats.matchesPlayed.value : 0;
                }
            }
            if (playlist == Playlist.THREES)
            {
                if (obj.data.Standard != null)
                {
                    List<Standard> data = new List<Standard>(obj.data.Standard);
                    data = data.Where(x => x.collectDate < cutOff && x.collectDate > seasonStartDate).ToList();
                    retVal.Ratings = data.Select(x => x.rating).ToList();
                    retVal.GamesPlayed = Datam.Count > 0 ? Datam[1].stats.matchesPlayed.value : 0;
                }
            }

            return retVal;
        }

        private string MakeJSONFriendly(string content)
        {
            content = Regex.Replace(content, "\"0\":", "\"Unranked\":");
            content = Regex.Replace(content, "\"10\":", "\"Duel\":");
            content = Regex.Replace(content, "\"11\":", "\"Duos\":");
            content = Regex.Replace(content, "\"12\":", "\"SoloStandard\":");
            content = Regex.Replace(content, "\"13\":", "\"Standard\":");
            content = Regex.Replace(content, "\"27\":", "\"Hoops\":");
            content = Regex.Replace(content, "\"28\":", "\"Rumble\":");
            content = Regex.Replace(content, "\"29\":", "\"Dropshot\":");
            content = Regex.Replace(content, "\"30\":", "\"Snowday\":");

            return content;
        }
    }
}
