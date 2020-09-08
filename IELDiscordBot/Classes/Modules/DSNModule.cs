using Discord.Commands;
using IELDiscordBot.Classes.Models;
using IELDiscordBotPOC.Classes.Utilities;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Google.Protobuf.WellKnownTypes;
using IELDiscordBot.Classes.Models.DSN;
using System.Text.RegularExpressions;
using IELDiscordBot.Classes.Models.DSN.Segments;
using Microsoft.VisualBasic.CompilerServices;
using IELDiscordBotPOC.Classes.Database;
using System.Linq;
using IELDiscordBot.Classes.Models.TRN;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;

namespace IELDiscordBot.Classes.Modules
{
    public class DSNModule : ModuleBase<SocketCommandContext>
    {
        private readonly List<int> _acceptableSeasons = new List<int>() { 12, 13, 14 };
        private readonly List<int> _acceptablePlaylists = new List<int>() {  10, 11, 12, 13 };
        private readonly DateTime _mmrCutoffDate = new DateTime(2020, 09, 06, 23, 59, 59);

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

        //[Command("dsn")]
        //public async Task HandleDSNCommandAsync(string platform, string username)
        //{
        //    using (HttpClient client = new HttpClient())
        //    {
        //        List<DSNCalculationData> calcData = new List<DSNCalculationData>();

        //        string apistring = string.Format(Constants.TRNAPI, platform, username);

        //        HttpResponseMessage response = await client.GetAsync(apistring).ConfigureAwait(false);
                
        //        string content = await response.Content.ReadAsStringAsync();

        //        TRNObject obj = null;
        //        try
        //        {
        //            obj = JsonConvert.DeserializeObject<TRNObject>(content);
        //        }
        //        catch(Exception ex)
        //        {

        //        }

        //        List<Segment> segments = new List<Segment>(obj.data.segments);
        //        segments.RemoveAll(x => _acceptableSeasons.Contains(x.attributes.season) == false);
        //        segments.RemoveAll(x => _acceptablePlaylists.Contains(x.attributes.playlistId) == false);

        //        var gamesPlayed = segments.Select(x => new { x.attributes.season, x.stats.matchesPlayed.value }).ToList();

        //        int playerId = obj.data.metadata.playerId;

        //        apistring = string.Format(Constants.TRNMMRAPI, playerId);
        //        response = await client.GetAsync(apistring);
        //        content = await response.Content.ReadAsStringAsync();
        //        content = MakeJSONFriendly(content);


        //        for (int i = 0; i < _acceptableSeasons.Count - 1; i++)
        //        {
        //            List<Datum> Data = new List<Datum>();
        //            var segment = await GetSeasonSegment(_acceptableSeasons[i], platform, username);
        //            if (segment == null)
        //            {
        //                calcData.Add(new DSNCalculationData() 
        //                { 
        //                    Platform = platform, 
        //                    User = username, 
        //                    Season = _acceptableSeasons[0], 
        //                    GamesPlayed = 0, 
        //                    MaxMMR = 0 
        //                });
        //                continue;
        //            }

        //            Data.AddRange(segment.data);
        //            Data.RemoveAll(x => _acceptablePlaylists.Contains(x.attributes.playlistId) == false);

        //            calcData.Add(new DSNCalculationData()
        //            {
        //                Platform = platform,
        //                User = username,
        //                Season = _acceptableSeasons[i],
        //                GamesPlayed = Data.Count > 0 ? Data.Sum(x => x.stats.matchesPlayed.value) : 0,
        //                MaxMMR = Data.Count > 0 ? Data.Max(x => x.stats.rating.value) : 0
        //            });
        //        }

        //        TRNMMRObject mmrobj = JsonConvert.DeserializeObject<TRNMMRObject>(content);

        //        List<Duel> duels = new List<Duel>(mmrobj.data.Duel);
        //        List<Duo> duos = new List<Duo>(mmrobj.data.Duos);
        //        List<Standard> standard = new List<Standard>(mmrobj.data.Standard);
        //        List<Solostandard> solostandard = new List<Solostandard>(mmrobj.data.SoloStandard);

        //        List<int> HighestMMRs = new List<int>
        //        {
        //            duos.Max(x => x.rating),
        //            standard.Max(x => x.rating),
        //        };

        //        int highestMMR = HighestMMRs.Max();
        //        calcData.Add(new DSNCalculationData()
        //        {
        //            Season = _acceptableSeasons.Last(),
        //            MaxMMR = highestMMR,
        //            GamesPlayed = segments.Sum(x => x.stats.matchesPlayed.value)
        //        });

        //        List<ManualPeakOverride> peaks = _db.ManualPeakOverrides.ToList().Where(x => x.User == username && x.Platform == platform).ToList();
        //        if (peaks.Count > 0)
        //            foreach (var p in peaks)
        //                calcData.First(x => x.Season == p.Season).MaxMMR = p.Peak;

        //        var orderedData = calcData.OrderByDescending(x => x.Season);

        //        await Context.Channel.SendMessageAsync("", false, Embeds.DSNCalculation(orderedData.ToList(), username, platform)).ConfigureAwait(false);
        //    }
        //}

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
            for (int i = 0; i < args.Length; i+=2)
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

                    for (int i = 0; i < _acceptableSeasons.Count - 1; i++)
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

                    List<int> HighestMMRs = new List<int>
                    {
                        duos.Where(x => x.collectDate < _mmrCutoffDate).Max(x => x.rating),
                        standard.Where(x => x.collectDate < _mmrCutoffDate).Max(x => x.rating),
                        duel.Where(x => x.collectDate < _mmrCutoffDate).Max(x => x.rating),
                        solo.Where(x => x.collectDate < _mmrCutoffDate).Max(x => x.rating)
                    };

                    int highestMMR = HighestMMRs.Max();
                    calcData.Add(new DSNCalculationData()
                    {
                        Platform = platform,
                        User = username,
                        Season = _acceptableSeasons.Last(),
                        MaxMMR = highestMMR,
                        GamesPlayed = segments.Sum(x => x.stats.matchesPlayed.value)
                    });
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
            content = Regex.Replace(content, "\"0\":",  "\"Unranked\":");
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
