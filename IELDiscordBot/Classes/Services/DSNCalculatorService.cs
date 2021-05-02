using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using NLog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Sheets.v4;
using Google.Apis.Services;
using System.Net.Http;
using IELDiscordBot.Classes.Utilities;
using IELDiscordBot.Classes.Models;
using Newtonsoft.Json;
using IELDiscordBot.Classes.Models.DSN.Segments;
using System.Text.RegularExpressions;
using IELDiscordBot.Classes.Models.DSN;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using IELDiscordBot.Classes.Models.WebAppAPI;

namespace IELDiscordBot.Classes.Services
{
    public class DSNCalculatorService
    {
        private readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly DiscordSocketClient _client;
        private readonly HttpClient _webClient;
        private readonly IConfigurationRoot _config;
        private readonly Timer _timer;
        private readonly List<int> _acceptablePlaylists = new List<int>() { 11, 13 };
        private static string ErrorLog = "";
        private static string AccountsChecked = "Accounts Checked: ";
        ServiceAccountCredential _sheetsCredential;
        string[] Scopes = { SheetsService.Scope.Spreadsheets };
        const string ApplicationName = "IEL Discord Bot .NET Application";
        const string SpreadsheetID = "1Yf38nVz_WD3VBf74LTjnt75NNLvAp54SySrvt2PRf3I";
        const string ServiceAccountEmail = "ieldiscordbot@inspired-rock-284217.iam.gserviceaccount.com";

        public enum Playlist
        {
            TWOS = 13,
            THREES = 14,
        }

        public enum Seasons
        {
            S15 = 0,
            S16 = 1,
            S17 = 2
        }

        DateTime[] cutOffDates = new DateTime[]
        {
            new DateTime(2020, 12, 09),
            new DateTime(2021, 04, 07),
            new DateTime(2021, 05, 02)
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
            _webClient = new HttpClient();
            Setup();
             _timer = new Timer(async _ =>
            {
                await ProcessNewSignupsAsync().ConfigureAwait(false);
                await GetLatestValues().ConfigureAwait(false);
            },
            null,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMinutes(5));
        }

        enum ColumnIDs
        {
            Name = 0,
            Discord = 1,
            PlayerID = 2,
            ProfileLink = 3,
            DSN = 19,
            League = 20
        }


        private void Setup()
        {
            var certificate = new X509Certificate2($@"key.p12", "notasecret", X509KeyStorageFlags.Exportable);
            _sheetsCredential = new ServiceAccountCredential(
                new ServiceAccountCredential.Initializer(ServiceAccountEmail)
                {
                    Scopes = Scopes
                }.FromCertificate(certificate));

            service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = _sheetsCredential,
                ApplicationName = ApplicationName
            });
        }

        private SheetsService service;
        private IList<IList<object>> _latestValues = null;
        private IList<IList<object>> _oldValues = null;
        private Queue<SpreadsheetUpdate> _updates = new Queue<SpreadsheetUpdate>();


        private async Task GetLatestValues()
        {
            SpreadsheetsResource.ValuesResource.GetRequest request =
    service.Spreadsheets.Values.Get(SpreadsheetID, "DSN Hub!A:AA");

            ValueRange response = await request.ExecuteAsync().ConfigureAwait(false);

            if (_latestValues != null) _oldValues = _latestValues;

            _latestValues = response.Values;

            CheckForUpdates();
        }

        private void CheckForUpdates()
        {
            List<string> _updates = new List<string>();

            for (int idx = 0; idx < _latestValues.Count; idx++)
            {
                var currentRow = _latestValues[idx];
                var oldRow = _latestValues[idx];

            }
        }
        public async Task<Platform[]> GetAccountsFromWebApp(int playerId)
        {
            string url = $"https://webapp.imperialesportsleague.co.uk/api/platforms/{playerId}";

            var request = await _webClient.GetAsync(url).ConfigureAwait(false);
            if (request.StatusCode != System.Net.HttpStatusCode.OK)
            {
                _log.Error($"Error getting values for player id: {playerId} from webapp!");
                return null;
            }
            string content = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonConvert.DeserializeObject<Platform[]>(content);
        }

        private async Task ProcessNewSignupsAsync()
        {
            SpreadsheetsResource.ValuesResource.GetRequest request =
                service.Spreadsheets.Values.Get(SpreadsheetID, "DSN Hub!A:AA");

            ValueRange response = await request.ExecuteAsync().ConfigureAwait(false);

            IList<IList<object>> values = response.Values;

            for (int row = 0; row < values.Count; row++)
            {
                IList<object> r = values[row];
                if (string.IsNullOrEmpty(r[(int)ColumnIDs.Name].ToString())) continue;
                if (string.IsNullOrEmpty(r[(int)ColumnIDs.DSN].ToString()))
                {
                    await CalculateDSN(r, service, row);
                    _log.Info($"Completed DSN Calculation for User: {r[(int)ColumnIDs.Name]}");
                    await Task.Delay(10000);
                }
            }
        }

        private readonly string[] _allowedPlatforms = { "steam", "xbl", "psn", "xbox", "ps" };

        private async Task CalculateDSN(IList<object> row, SheetsService service, int idx)
        {
            //Get Accounts from WebApp
            var r = await GetAccountsFromWebApp(int.Parse(row[(int)ColumnIDs.PlayerID].ToString()));
            //Filter Accounts
            r = r.Where(x => _allowedPlatforms.Contains(x.type)).ToArray();

            List<CalcData> CalcData = new List<CalcData>();

            string dsnCommand = $"!dsn {idx + 1} ";

            //Check data for each account.
            foreach (var account in r)
            {
                account.type = ConvertPlatform(account.type);
                if (account.type == "steam")
                {
                    account.id = account.id.Substring(account.id.LastIndexOf('/') + 1);
                    if (account.id.EndsWith("/"))
                        account.id.Remove(account.id.Length - 1);
                }
                var trnResponse = await TRNRequest(account.type, account.id);
                if (trnResponse is null) continue;
                CalcData.AddRange(trnResponse);
            }

            await CalcAndSendResponse(idx, CalcData);
        }

        public async Task CalcAndSendResponse(int idx, List<CalcData> CalcData)
        {
            int S15Peak = 0;
            int S16Peak = 0;
            int S17Peak = 0;

            for (int season = 15; season < 18; season++)
            {
                int highestVal = 0;
                foreach (var y in CalcData)
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

            int s15Games = CalcData.Where(x => x.Season == 15).Sum(x => x.GamesPlayed);
            int s16Games = CalcData.Where(x => x.Season == 16).Sum(x => x.GamesPlayed);
            int s17Games = CalcData.Where(x => x.Season == 17).Sum(x => x.GamesPlayed);

            IList<object> obj = new List<object>();
            UpdateValuesResponse res = null;

            obj.Add(s15Games);
            obj.Add(s16Games);
            obj.Add(s17Games);
            obj.Add(null);
            obj.Add(S15Peak);
            obj.Add(S16Peak);
            obj.Add(S17Peak);

            obj[3] = $"=IFS(ISBLANK(A{idx + 1});;AND(NOT(ISBLANK(A{idx + 1}));ISBLANK(F{idx + 1});ISBLANK(G{idx + 1});ISBLANK(H{idx + 1});ISBLANK(J{idx + 1});ISBLANK(K{idx + 1});ISBLANK(L{idx + 1})); \"Pending\";AND(NOT(ISBLANK(A{idx + 1}));OR(ISBLANK(F{idx + 1});ISBLANK(G{idx + 1});ISBLANK(H{idx + 1});ISBLANK(J{idx + 1});ISBLANK(K{idx + 1});ISBLANK(L{idx + 1}))); \"Missing Data\";OR(H{idx + 1} < DV_MinGAbsolut; G{idx + 1} < DV_MinGAbsolut; F{idx + 1} < DV_MinGAbsolut; L{idx + 1} = 0; K{idx + 1} = 0; J{idx + 1} = 0); \"Investigate App\";AND(H{idx + 1} < DV_MinGCurrent; G{idx + 1} < DV_MinGPrev1; F{idx + 1} < DV_MinGPrev2); \"Min Games not reached\";AND(L{idx + 1} < DV_DSNMin; K{idx + 1} < DV_DSNMin; F{idx + 1} < DV_DSNMin); \"Too Low\";OR(H{idx + 1} >= DV_MinGCurrent; G{idx + 1} >= DV_MinGPrev1; F{idx + 1} >= DV_MinGPrev2); \"Verified\")";

            ValueRange v = new ValueRange();
            v.MajorDimension = "ROWS";
            v.Values = new List<IList<object>> { obj };
            SpreadsheetsResource.ValuesResource.UpdateRequest u = service.Spreadsheets.Values.Update(v, SpreadsheetID, $"DSN Hub!F{idx + 1}");//:O{idx+1}");
            u.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            res = await u.ExecuteAsync();
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

        public async Task<List<CalcData>> TRNRequest(string platform, string username)
        {
            platform = ConvertPlatform(platform);
            if (platform == "steam")
            {
                username = username.Substring(username.LastIndexOf('/') + 1);
                if (username.EndsWith("/"))
                    username.Remove(username.Length - 1);
            }

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
                    await GetCalcDataForSegmentAsync(platform, username, 15, Playlist.TWOS, mmrObj),
                    await GetCalcDataForSegmentAsync(platform, username, 15, Playlist.THREES, mmrObj),
                    await GetCalcDataForSegmentAsync(platform, username, 16, Playlist.TWOS, mmrObj),
                    await GetCalcDataForSegmentAsync(platform, username, 16, Playlist.THREES, mmrObj),
                    await GetCalcDataForSegmentAsync(platform, username, 17, Playlist.TWOS, mmrObj),
                    await GetCalcDataForSegmentAsync(platform, username, 17, Playlist.THREES, mmrObj)
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
                case 15:
                    {
                        cutOff = cutOffDates[(int)Seasons.S15];
                        break;
                    }
                case 16:
                    {
                        cutOff = cutOffDates[(int)Seasons.S16];
                        seasonStartDate = cutOffDates[(int)Seasons.S15].AddDays(1);
                        break;
                    }
                case 17:
                    {
                        cutOff = cutOffDates[(int)Seasons.S17];
                        seasonStartDate = cutOffDates[(int)Seasons.S16].AddDays(1);
                        break;
                    }
            }

            try
            {
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
            }
            catch(Exception ex)
            {
                _log.Error(ex);
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
        public struct CalcData
        {
            internal int Season;
            internal Playlist Playlist;
            internal List<int> Ratings;
            internal int GamesPlayed;
        }

        public async Task MakeRequest(string sectionToEdit, List<object> obj)
        {
            ValueRange v = new ValueRange();
            v.MajorDimension = "ROWS";
            v.Values = new List<IList<object>> { obj };
            SpreadsheetsResource.ValuesResource.UpdateRequest u = service.Spreadsheets.Values.Update(v, SpreadsheetID, sectionToEdit);//:O{idx+1}");
            u.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            UpdateValuesResponse res = u.Execute();
        }

        internal string GetLeague(string discordUsername)
        {
            for (int row = 0; row < _latestValues.Count; row++)
            {
                IList<object> r = _latestValues[row];

                if (r[(int)ColumnIDs.Discord].ToString().ToLower() == discordUsername.ToLower())
                {
                    return r[(int)ColumnIDs.League].ToString();
                }
            }

            return "";
        }

        internal int GetRowNumber(string discordUsername)
        {
            for (int row = 0; row < _latestValues.Count; row++)
            {
                IList<object> r = _latestValues[row];

                if (r[2].ToString().ToLower() == discordUsername.ToLower())
                    return row + 1;
            }

            return -1;
        }
    }
}

