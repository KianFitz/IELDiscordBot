using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using NLog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Sheets.v4;
using System.IO;
using Google.Apis.Util.Store;
using Google.Apis.Services;
using System.Net.Http;
using IELDiscordBotPOC.Classes.Utilities;
using IELDiscordBot.Classes.Models;
using Newtonsoft.Json;
using IELDiscordBot.Classes.Models.DSN.Segments;
using System.Text.RegularExpressions;
using IELDiscordBot.Classes.Models.DSN;
using System.Linq;

namespace IELDiscordBot.Classes.Services
{
    public class DSNCalculatorService
    {
        private readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly DiscordSocketClient _client;
        private readonly IConfigurationRoot _config;
        private readonly Timer _timer;

        private readonly List<int> _acceptablePlaylists = new List<int>() { 11, 13 };

        private static string ErrorLog = "";
        private static string AccountsChecked = "Accounts Checked: ";

        enum Playlist
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

        internal class DSNCalculationData
        {
            public string User;
            public string Platform;
            public int Season;
            public int GamesPlayed;
            public int MaxMMR;
        }
        public DSNCalculatorService(DiscordSocketClient client, IConfigurationRoot config)
        {
            _client = client;
            _config = config;
            Setup();
            _timer = new Timer(async _ =>
            {
                await ProcessNewSignupsAsync().ConfigureAwait(false);
                await GetLatestValues().ConfigureAwait(false);
            },
            null,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMinutes(10));
        }

        UserCredential _sheetsCredential;
        string[] Scopes = { SheetsService.Scope.Spreadsheets };
        const string ApplicationName = "IEL Discord Bot .NET Appliation";
        const string SpreadsheetID = "1ozwketqZ4ZU9Dk2wyB20Yq8KDQXw1zA2EOUdXuuG7NY";

        enum ColumnIDs
        {
            Name = 0,
            Country = 1,
            Discord = 2,
            Platform = 3,
            Tracker = 4,
            RawAltID = 5,
            AltTracker = 6,
            InDiscordCheck = 7,
            S14Games = 8,
            S15Games = 9,
            S16Games = 10,
            MinGamesReached = 11,
            S14Peak = 12,
            S15Peak = 13,
            S16Peak = 14,
            TotalPeak = 15,
            DSN = 22,
            League = 23,
            Notes = 32
        }


        private void Setup()
        {
            using (var fileStream = new FileStream(_config["dsn:sheets:credentials"], FileMode.Open, FileAccess.Read))
            {
                string credPath = _config["dsn:sheets:token"];
                _sheetsCredential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(fileStream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
                _log.Info("Credential File Saved to: " + credPath);
            }
        }

        private SheetsService service;

        private IList<IList<object>> _latestValues = null;

        private async Task GetLatestValues()
        {
            SpreadsheetsResource.ValuesResource.GetRequest request =
    service.Spreadsheets.Values.Get(SpreadsheetID, "DSN Hub!A:AH");

            ValueRange response = await request.ExecuteAsync().ConfigureAwait(false);

            _latestValues = response.Values;
        }

        private async Task ProcessNewSignupsAsync()
        {
            service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = _sheetsCredential,
                ApplicationName = ApplicationName
            });

            SpreadsheetsResource.ValuesResource.GetRequest request =
                service.Spreadsheets.Values.Get(SpreadsheetID, "DSN Hub!A:AH");

            ValueRange response = await request.ExecuteAsync().ConfigureAwait(false);

            IList<IList<object>> values = response.Values;

            for (int row = 0; row < values.Count; row++)
            {
                IList<object> r = values[row];

                if (string.IsNullOrEmpty(r[(int)ColumnIDs.S14Games].ToString()))
                {
                    if (values[row].Count >= 34)
                    {
                        if (string.IsNullOrEmpty(r[(int)ColumnIDs.Notes].ToString()) == false)
                            continue;
                    }
                    await CalculateDSN(r, service, row);

                }
            }
        }

        private async Task CalculateDSN(IList<object> row, SheetsService service, int idx)
        {
            string mainPlatform = row[(int)ColumnIDs.Platform].ToString();
            mainPlatform = ConvertPlatform(mainPlatform);
            //string mainID = ReplacePlatform(row[(int)ColumnIDs.Tracker].ToString().Replace(Constants.TRNREDUNDANT, Constants.TRNREPLACE));
            string mainID = row[(int)ColumnIDs.Tracker].ToString();
            mainID = mainID.Substring(mainID.LastIndexOf('/') + 1);
            if (mainPlatform.ToLower() == "switch") return;

            List<string> array = row[(int)ColumnIDs.AltTracker].ToString().Split(" ").ToList();
            array.RemoveAll(x => x.ToLower() == "on");
            array.RemoveAll(x => x == "");

            if (array.Count != 0)
                return;

            if (mainID.Contains("#"))
            {
                mainID = mainID.Substring(mainID.IndexOf("#") + 1);
            }

            List<CalcData> CalcData = new List<CalcData>();
            var x = await TRNRequest(mainPlatform, mainID);

            if (x is null) return;
            CalcData.AddRange(x);

            List<KeyValuePair<string, string>> altAccounts = new List<KeyValuePair<string, string>>();


            if (array.Count == 1)
                altAccounts.Add(new KeyValuePair<string, string>(mainPlatform, array[0]));

            for (int i = 0; i < array.Count; i++)
            {
                if (array.Count >= 20) break;

                string s1 = array[i];
                s1 = s1.ToLower();

                s1.Replace(":", "");
                s1.Replace("(", "");
                s1.Replace(")", "");
                s1.Trim();

                if (s1.Contains("epic") || s1.Contains("switch"))
                    continue;

                if (s1.Contains("steamcommunity.com"))
                {
                    if (s1.EndsWith('/'))
                        s1 = s1.Substring(0, s1.Length - 1);

                    string profilelink = s1.Substring(s1.LastIndexOf('/') + 1);

                    altAccounts.Add(new KeyValuePair<string, string>("steam", profilelink));
                    continue;

                }

                if (array.Count > i + 1)
                {
                    string s2 = array[i + 1];
                    s2 = s2.ToLower();

                    s2.Replace(":", "");
                    s2.Replace("(", "");
                    s2.Replace(")", "");
                    s2.Trim();


                    if (s2.Contains("epic") || s2.Contains("switch")) continue;

                    if (s2.Contains("steamcommunity.com"))
                    {
                        if (s2.EndsWith('/'))
                            s2 = s2.Substring(0, s2.Length - 1);

                        string profilelink = s2.Substring(s2.LastIndexOf('/') + 1);

                        altAccounts.Add(new KeyValuePair<string, string>("steam", profilelink));
                        continue;
                    }

                    if (ConvertPlatform(s1) != s1)
                    {
                        altAccounts.Add(new KeyValuePair<string, string>(s1, s2));
                    }
                    if (ConvertPlatform(s2) != s2)
                    {
                        altAccounts.Add(new KeyValuePair<string, string>(s2, s1));
                    }

                    altAccounts.Add(new KeyValuePair<string, string>(mainPlatform, s1));

                }
            }

            foreach (var account in altAccounts)
            {
                List<CalcData> acc = await TRNRequest(account.Key, account.Value);
                if (acc is null)
                {
                    ErrorLog += $"Error Finding Account: Platform: {account.Key} Account: {account.Value} // ";
                    _log.Error(ErrorLog);
                }
                else
                {
                    AccountsChecked += $"\r\nPlatform: {account.Key} Account: {account.Value} -- ";
                    CalcData.AddRange(acc);
                }
            }

            int S14Peak = 0; //alcData.Where(x => x.Season == 14).Max(y => y.Ratings).First();
            int S15Peak = 0; //alcData.Where(x => x.Season == 15).Max(y => y.Ratings).First();
            int S16Peak = 0; //CalcData.Where(x => x.Season == 16).Max(y => y.Ratings).First();

            for (int season = 14; season < 17; season++)
            {
                int highestVal = 0;
                foreach (var y in CalcData)
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

            int s14Games = CalcData.Where(x => x.Season == 14).Sum(x => x.GamesPlayed);
            int s15Games = CalcData.Where(x => x.Season == 15).Sum(x => x.GamesPlayed);
            int s16Games = CalcData.Where(x => x.Season == 16).Sum(x => x.GamesPlayed);
            //double DSN = (highestPeak * 0.7) + (secondHighestPeak * 0.3);

            IList<object> obj = new List<object>();
            UpdateValuesResponse res = null;

            obj.Add(s14Games);
            obj.Add(s15Games);
            obj.Add(s16Games);
            obj.Add($"=IFS(ISBLANK(K{idx + 1});;AND(K{idx + 1}>=150;AND(J{idx + 1}>=150;I{idx + 1}>=150));\"Games Verified\"; AND(K{idx + 1}<=150;AND(J{idx + 1}>=150;I{idx + 1}>=150));\"Min Games S2 / 16 not reached\"; OR(J{idx + 1}<=150;I{idx + 1}<=150);\"Investigate App\")");
            obj.Add(S14Peak);
            obj.Add(S15Peak);
            obj.Add(S16Peak);

            ValueRange v = new ValueRange();
            v.MajorDimension = "ROWS";
            v.Values = new List<IList<object>> { obj };
            SpreadsheetsResource.ValuesResource.UpdateRequest u = service.Spreadsheets.Values.Update(v, SpreadsheetID, $"DSN Hub!I{idx + 1}");//:O{idx+1}");
            u.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            res = u.Execute();


            obj = new List<object>();
            obj.Add(AccountsChecked);
            v = new ValueRange();
            v.MajorDimension = "ROWS";
            v.Values = new List<IList<object>> { obj };
            u = service.Spreadsheets.Values.Update(v, SpreadsheetID, $"DSN Hub!AH{idx + 1}");//:O{idx+1}");
            u.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            res = u.Execute();

            _log.Info($"Updating DSN Calculator Returned: {res.UpdatedCells}");
            ErrorLog = "";
            AccountsChecked = "Accounts Checked: ";
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

        string ReplacePlatform(string input)
        {
            input.ToLower();

            input = input.Replace("pc", "steam");
            input = input.Replace("xbox", "xbl");
            input = input.Replace("ps4", "psn");

            return input;
        }

        private async Task<List<CalcData>> TRNRequest(string platform, string username)
        {
            platform = ConvertPlatform(platform);
            using (HttpClient client = new HttpClient())
            {
                string apistring = string.Format(Constants.TRNAPI, platform, username);

                HttpResponseMessage response = await client.GetAsync(apistring).ConfigureAwait(false);

                string content = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrEmpty(content)) return null;
                if (content.ToLower().Contains("we could not find the player"))
                    return null;

                TRNObject obj = null;
                try
                {
                    obj = JsonConvert.DeserializeObject<TRNObject>(content);
                }
                catch (Exception ex)
                {
                    _log.Error(ex);
                    return null;
                }

                int playerId = obj.data.metadata.playerId;

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

                return Data;
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


        struct CalcData
        {
            internal int Season;
            internal Playlist Playlist;
            internal List<int> Ratings;
            internal int GamesPlayed;
        }

        internal async Task UpdateSpreadSheet(List<object> obj, int row)
        {
            ValueRange v = new ValueRange();
            v.MajorDimension = "ROWS";
            v.Values = new List<IList<object>> { obj };
            SpreadsheetsResource.ValuesResource.UpdateRequest u = service.Spreadsheets.Values.Update(v, SpreadsheetID, $"DSN Hub!I{row}");//:O{idx+1}");
            u.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            UpdateValuesResponse res = u.Execute();
        }

        internal string GetLeagueAsync(string discordUsername)
        {
            for (int row = 0; row < _latestValues.Count; row++)
            {
                IList<object> r = _latestValues[row];

                if (r[(int)ColumnIDs.Discord].ToString() == discordUsername)
                {
                    return r[(int)ColumnIDs.League].ToString();
                }
            }

            return "";
        }

    }
}

