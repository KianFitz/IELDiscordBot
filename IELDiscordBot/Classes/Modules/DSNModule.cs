using Discord.Commands;
using IELDiscordBot.Classes.Models;
using IELDiscordBot.Classes.Models.DSN;
using IELDiscordBot.Classes.Models.DSN.Segments;
using IELDiscordBot.Classes.Models.TRN;
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
        private readonly DateTime _mmrCutoffDate = new DateTime(2021, 09, 06, 23, 59, 59);

        private readonly IELContext _db;

        public DSNModule(IELContext db)
        {
            _db = db;
        }

        internal class DSNCalculationData
        {
            public string User;
            public string Platform;
            public int Season;
            public int GamesPlayed;
            public int MaxMMR;
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
        public async Task HandleDSNCommandAsync(params string[] args)
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

            List<DSNCalculationData> calcData = new List<DSNCalculationData>();
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

                    TRNObject obj = null;
                    List<Segment> segments = new List<Segment>();
                    try
                    {
                        obj = JsonConvert.DeserializeObject<TRNObject>(content);

                        segments.AddRange(obj.data.segments);
                        segments.RemoveAll(x => _acceptableSeasons.Contains(x.attributes.season) == false);
                        segments.RemoveAll(x => _acceptablePlaylists.Contains(x.attributes.playlistId) == false);

                    }
                    catch (Exception ex)
                    {
                        if (content.Contains("We could not find the player"))
                        {
                            await message.ModifyAsync(x =>
                            {
                                x.Content = "";
                                x.Embed = Embeds.DSNError(platform, username, $"Could not find account. Please check the spelling or try again.");
                            });
                        }
                        else
                        {
                            await message.ModifyAsync(x =>
                            {
                                x.Content = "";
                                x.Embed = Embeds.DSNError(platform, username, $"{ex.Message} at {ex.StackTrace} <@!301876830737006593>");
                            });
                            _log.Error(content);
                        }
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

                    for (int i = 0; i < _acceptableSeasons.Count; i++)
                    {
                        await message.ModifyAsync(x =>
                        {
                            x.Content = "";
                            x.Embed = Embeds.DSNStatus(accIdx, accountsLength, $"Getting Season Info for Season {_acceptableSeasons[i]} from TRN MMR API");
                        });

                        List<Datum> Data = new List<Datum>();
                        var segment = await GetSeasonSegment(_acceptableSeasons[i], platform, username);
                        if (segment == null)
                        {
                            calcData.Add(new DSNCalculationData()
                            {
                                User = username,
                                Platform = platform,
                                Season = _acceptableSeasons[0],
                                GamesPlayed = 0,
                                MaxMMR = 0
                            });
                            continue;
                        }

                        Data.AddRange(segment.data);
                        Data.RemoveAll(x => _acceptablePlaylists.Contains(x.attributes.playlistId) == false);

                        calcData.Add(new DSNCalculationData()
                        {
                            User = username,
                            Platform = platform,
                            Season = _acceptableSeasons[i],
                            GamesPlayed = Data.Count > 0 ? Data.Sum(x => x.stats.matchesPlayed.value) : 0,
                            MaxMMR = Data.Count > 0 ? Data.Max(x => x.stats.rating.value) : 0
                        });
                    }

                    TRNMMRObject mmrobj = JsonConvert.DeserializeObject<TRNMMRObject>(content);

                    List<Duo> duos = new List<Duo>();
                    List<Standard> standard = new List<Standard>();
                    List<Duel> duel = new List<Duel>();
                    List<Solostandard> solo = new List<Solostandard>();

                    if (mmrobj.data.Duos != null)
                        duos.AddRange(mmrobj.data.Duos);
                    else
                        duos.Add(new Duo() { rating = 0 });

                    if (mmrobj.data.Standard != null)
                        standard.AddRange(mmrobj.data.Standard);
                    else
                        standard.Add(new Standard() { rating = 0 });

                    if (mmrobj.data.SoloStandard != null)
                        solo.AddRange(mmrobj.data.SoloStandard);
                    else
                        solo.Add(new Solostandard() { rating = 0 });

                    if (mmrobj.data.Duel != null)
                        duel.AddRange(mmrobj.data.Duel);
                    else
                        duel.Add(new Duel() { rating = 0 });

                    List<int> HighestMMRs = new List<int>();

                    duos = duos.Where(x => x.collectDate < _mmrCutoffDate).ToList();
                    standard = standard.Where(x => x.collectDate < _mmrCutoffDate).ToList();
                    duel = duel.Where(x => x.collectDate < _mmrCutoffDate).ToList();
                    solo = solo.Where(x => x.collectDate < _mmrCutoffDate).ToList();

                    HighestMMRs.Add(duos.Count == 0 ? 0 : duos.Max(x => x.rating));
                    HighestMMRs.Add(standard.Count == 0 ? 0 : standard.Max(x => x.rating));
                    HighestMMRs.Add(duel.Count == 0 ? 0 : duel.Max(x => x.rating));
                    HighestMMRs.Add(solo.Count == 0 ? 0 : solo.Max(x => x.rating));

                    int highestMMR = HighestMMRs.Max();
                    //calcData.Add(new DSNCalculationData()
                    //{
                    //    Platform = platform,
                    //    User = username,
                    //    Season = _acceptableSeasons.Last(),
                    //    MaxMMR = highestMMR,
                    //    GamesPlayed = segments.Sum(x => x.stats.matchesPlayed.value)
                    //});
                    calcData.Last().MaxMMR = highestMMR;
                }

                var peaks = _db.ManualPeakOverrides.ToList().Where(x => x.User == username && x.Platform == platform);
                if (peaks.Count() > 0)
                    foreach (var p in peaks)
                        calcData.First(x => x.Season == p.Season && x.User == username && x.Platform == platform).MaxMMR = p.Peak;
            }
            for (int i = 14; i > 11; i--)
            {
                var results = calcData.Where(x => x.Season == i);

                if (results.Count() == 0)
                    continue;

                int maxMMR = results.Max(x => x.MaxMMR);
                int totalGames = results.Sum(x => x.GamesPlayed);

                calcData.RemoveAll(x => x.MaxMMR != maxMMR && x.Season == i);
                calcData.First(x => x.Season == i).GamesPlayed = totalGames;
            }

            var orderedData = calcData.OrderByDescending(x => x.Season);

            string usernameString = string.Join(',', accounts.Select(x => x.User));
            string platformString = string.Join(',', accounts.Select(x => x.Platform));

            await message.ModifyAsync(x =>
            {
                x.Content = "";
                x.Embed = Embeds.DSNStatus(accountsLength, accountsLength, $"Calculating...");
            });

            await message.ModifyAsync(x =>
            {
                x.Content = "";
                x.Embed = Embeds.DSNCalculation(orderedData.ToList(), usernameString, platformString);
            }).ConfigureAwait(false);

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
