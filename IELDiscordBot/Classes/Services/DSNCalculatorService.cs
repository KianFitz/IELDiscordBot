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
using Discord;
using System.IO;

namespace IELDiscordBot.Classes.Services
{
    public class DSNCalculatorService
    {
        private readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly DiscordSocketClient _client;
        private readonly HttpClient _webClient;
        private readonly IConfigurationRoot _config;
        private readonly Timer _timer;
        private readonly Timer _queueTimer;
        private readonly List<int> _acceptablePlaylists = new List<int>() { 11, 13 };
        private readonly List<StatusClass> _signupStatus;
        private static string ErrorLog = "";
        private static string AccountsChecked = "Accounts Checked: ";
        ServiceAccountCredential _sheetsCredential;
        string[] Scopes = { SheetsService.Scope.Spreadsheets };
        const string ApplicationName = "IEL Discord Bot .NET Application";
        const string SpreadsheetID = "1QyixxLA2jl1p_K1TlL5mp9Fsa8xsVa402iwSXo4rvMQ";
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
            new DateTime(2020, 12, 10),
            new DateTime(2021, 04, 07),
            new DateTime(2021, 05, 03)
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
            _signupStatus = new List<StatusClass>();
            Setup();
             _timer = new Timer(async _ =>
            {
                //await ProcessNewSignupsAsync().ConfigureAwait(false);
            },
            null,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMinutes(5));
            _queueTimer = new Timer(async _ =>
            {
                await GetLatestValues().ConfigureAwait(false);
                //await ProcessSignupQueueAsync().ConfigureAwait(false);
            },
           null,
           TimeSpan.FromSeconds(5),
           TimeSpan.FromSeconds(30));
        }

        private async Task ProcessSignupQueueAsync()
        {
            List<StatusClass> toProcess = new List<StatusClass>(_signupStatus);
            _signupStatus.Clear();
            ulong channelID = 665242755731030045;
            //ulong faRoleID = 743591049670164620;
            ulong GuildID = 468918204362653696;
            ulong GMRole = 472145107056066580;
            if (toProcess.Count == 0)
                return;


            IGuild guild = _client.GetGuild(GuildID);
            ITextChannel textChannel = await guild.GetTextChannelAsync(channelID);

            foreach (var signup in toProcess)
            {
                List<object> obj = new List<object>();
                obj.Add(true);
                obj.Add(true);
                obj.Add("");
                obj.Add(true);

                if (signup.DiscordUser is null)
                {
                    obj[0] = false;
                    obj[1] = false;
                    obj[2] = "Player not in Discord";
                    await MakeRequest($"DSN Hub!P{signup.RowNumber}", obj);
                    await signup.StaffChannel.SendMessageAsync($"Unable to process signup for row: {signup.RowNumber}, Player not in Discord");
                    continue;
                }

                if (signup.Accept)
                {
                    if (GetVerifiedByRow(signup.RowNumber) == false)
                    {
                        await signup.StaffChannel.SendMessageAsync($"Unable to process signup for row: {signup.RowNumber}, \"Verified App\" box not checked!");
                        continue;
                    }

                    //await signup.DiscordUser.AddRoleAsync(guild.GetRole(faRoleID));
                    await textChannel.SendMessageAsync($"{signup.DiscordUser.Mention} you have been accepted to the IEL!");
                }
                else
                {
                    if (signup.DenyReason == "")
                    {
                        await signup.StaffChannel.SendMessageAsync($"Unable to process signup for row: {signup.RowNumber}, \"Deny reason\" not selected.");
                        continue;
                    }

                    obj[1] = false;
                    obj[2] = _latestValues[signup.RowNumber - 1][17].ToString();
                    await textChannel.SendMessageAsync($"{signup.DiscordUser.Mention} {_denySentences[signup.DenyReason]}");
                }
                await MakeRequest($"DSN Hub!P{signup.RowNumber}", obj);
                if (signup.Accept == false) continue;

                if (signup.DiscordUser.Roles.Any(x => x.Id == GMRole)) continue;
                await signup.DiscordUser.AddRolesAsync(signup.RolesToAdd);
                await signup.DiscordUser.ModifyAsync(x =>
                    {
                        x.Nickname = $"[FA] {(x.Nickname.IsSpecified ? x.Nickname : signup.DiscordUser.Username)}";
                    });
                await Task.Delay(1500);
            }
        }

        internal async Task GetSignupDetails(ulong discordId, ISocketMessageChannel channel, ulong requestor)
        {
            var signup = GetSignupByDiscordId(discordId);
            if (signup is null)
            {
                await channel.SendMessageAsync("", false, Embeds.NoSignup(discordId)).ConfigureAwait(false);

                return;
            }

            var profileName = signup[0].ToString();
            var id = signup[1].ToString();
            var profileLink = signup[4].ToString();
            var status = signup[24].ToString();
            var r = await GetAccountsFromWebApp(int.Parse(signup[(int)ColumnIDs.PlayerID].ToString()));
            r = r.Where(x => _allowedPlatforms.Contains(x.type)).ToArray();
            var platformLinks = string.Join("\r\n", r.Select(x => x.type + ": " + (x.type == "steam" ? x.id : x.name)));

            var message = await channel.SendMessageAsync("", false, Embeds.SignupDetails(profileName, id, profileLink, status, platformLinks, requestor)).ConfigureAwait(false);
        }

        internal async Task RecheckSignup(ulong discordId, ISocketMessageChannel channel)
        {
            IList<object> signup = null;
            int i = 0;
            for (i = 0; i < _latestValues.Count; i++)
            {
                if (_latestValues[i][(int)ColumnIDs.Discord].ToString() == discordId.ToString())
                {
                    signup = _latestValues[i];
                    break;
                }
            }

            if (signup is null)
                return;

            if (signup[24].ToString() != "Investigate App" && signup[24].ToString() != "Requirements not reached")
                return;

            var message = await channel.SendMessageAsync("Loading..");
            await CalculateDSN(signup, service, i);
            await message.ModifyAsync(x =>
            {
                x.Embed = Embeds.SignupRecalculated(discordId);
                x.Content = "";
            });
        }


        public IList<object> GetSignupByDiscordId(ulong discordId)
        {
            var retVal = _latestValues.FirstOrDefault(x => x[(int)ColumnIDs.Discord].ToString() == discordId.ToString());
            return retVal;
        }

        private bool GetVerifiedByRow(int row)
        {
            return bool.Parse(_latestValues[row - 1][16].ToString());
        }

        enum ColumnIDs : int
        {
            Name = 0,
            Discord = 1,
            PlayerID = 2,
            ProfileLink = 3,
            DSN = 19,
            League = 20,
            ApplicationStatus = 24
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

            _latestValues = response.Values;
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

        internal async Task QueueAccept(int row, SocketGuild guild, ISocketMessageChannel channel)
        {
            var roles = GetRoles(guild, row);

            if (roles is null)
            {
                await channel.SendMessageAsync($"Row {row} could not be added to accept/deny queue, user left Discord server.");
                return;
            }

            _signupStatus.Add(new StatusClass()
            { 
                Accept = true,
                DenyReason = "",
                DiscordUser = guild.GetUser(GetDiscordID(row)),
                RolesToAdd = roles,
                StaffChannel = channel,
                RowNumber = row
            });

            await channel.SendMessageAsync($"Row {row} added to accept/deny queue. Queue will be ran in ~30 seconds");
        }

        public IRole[] GetRoles(SocketGuild guild, int row)
        {
            IRole gmRole = guild.GetRole(472145107056066580);

            IGuildUser guildUser = guild.GetUser(GetDiscordID(row));

            if (guildUser is null)
            {
                return null;
            }

            if ((guildUser as SocketGuildUser).Roles.Any(x => x.Id == 472145107056066580)) return new IRole[] { };

            Dictionary<string, IRole> faRoles = new Dictionary<string, IRole>()
            {
                { "Academy", guild.GetRole(797537384022409256) },
                { "Prospect", guild.GetRole(670231374896168960) },
                { "Challenger", guild.GetRole(670230994627854347) },
                { "Master", guild.GetRole(671808027313045544) }
            };

            Dictionary<string, IRole> playerRoles = new Dictionary<string, IRole>()
            {
                { "Academy", guild.GetRole(797537428696989767) },
                { "Prospect", guild.GetRole(712599931382136842) },
                { "Challenger", guild.GetRole(712599930866237531) },
                { "Master", guild.GetRole(712599928890720284) }
            };

            string league = _latestValues[row - 1][(int)ColumnIDs.League].ToString();

            IRole faRole = faRoles[league];
            IRole playerRoleToAssigned = playerRoles[league];

            return new IRole[] { faRole, playerRoleToAssigned };
        }

        internal ulong GetDiscordID(int row)
        {
            return ulong.Parse(_latestValues[row - 1][(int)ColumnIDs.Discord].ToString());
        }

        public async Task AssignLeagueFARoles(ISocketMessageChannel channel, IGuild guild)
        {
            int remaining = _latestValues.Where(x => x[0].ToString() != "").Count();
            Dictionary<string, int> _assignedCounters = new Dictionary<string, int>();
            var message = await channel.SendMessageAsync("", false, Embeds.AssigningLeagueRoles(remaining, _assignedCounters));

            await GetLatestValues().ConfigureAwait(false);

            string errorLog = "";

            for (int row = 1; row < _latestValues.Count; row++)
            {
                var signup = _latestValues[row];
                if (signup[(int)ColumnIDs.Discord].ToString() == "") continue;

                ulong discordId = ulong.Parse(signup[(int)ColumnIDs.Discord].ToString());
                if (signup[(int)ColumnIDs.ApplicationStatus].ToString() != "Approved and Notified") continue;
                string league = signup[(int)ColumnIDs.League].ToString();



                var user = await guild.GetUserAsync(discordId);
                if (user is null)
                {
                    errorLog += $"{signup[(int)ColumnIDs.Name]}: User left Discord\r\n";
                    continue;
                }
                if (user.RoleIds.Any(x => x == 472145107056066580))
                {
                    league = "GM (Skipped)";
                }

                if (_assignedCounters.ContainsKey(league)) _assignedCounters[league]++;
                else _assignedCounters.Add(league, 1);

                if (league == "GM (Skipped)")
                    continue;

                try
                {
                }
                catch(Exception ex)
                {
                    if (ex.Message.ToLower().Contains("forbidden"))
                        errorLog += $"{signup[(int)ColumnIDs.Name]}: Permission Error\r\n";
                    else
                        errorLog += $"{signup[(int)ColumnIDs.Name]}: {ex.Message}\r\n";
                }

                remaining--;

                if (row % 10 == 0)
                {
                    await message.ModifyAsync(x =>
                        x.Embed = Embeds.AssigningLeagueRoles(remaining, _assignedCounters)
                    );
                }

                await Task.Delay(1500);
            }

            await message.ModifyAsync(
                x => x.Embed = Embeds.AssigningLeagueRoles(remaining, _assignedCounters)
                );


            if (errorLog.Length > 2000)
            {
                File.WriteAllText(@"C:\Temp\DiscordErrorLog.log", errorLog);
                await channel.SendFileAsync(@"C:\Temp\DiscordErrorLog.log", "");
            }
            else
                await channel.SendMessageAsync("", false, Embeds.ErrorLog(errorLog)).ConfigureAwait(false);
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
                if (lockedRows.Contains(row)) continue;
                if (string.IsNullOrEmpty(r[(int)ColumnIDs.Name].ToString())) continue;
                if (string.IsNullOrEmpty(r[(int)ColumnIDs.DSN].ToString()))
                {
                    _log.Info($"Started DSN Calculation for User: {r[(int)ColumnIDs.Name]}");
                    lockedRows.Add(row);
                    await CalculateDSN(r, service, row);
                    _log.Info($"Completed DSN Calculation for User: {r[(int)ColumnIDs.Name]}");
                    await Task.Delay(10000);
                    lockedRows.Remove(row);
                }
            }
        }

        internal async Task QueueDeny(int row, SocketGuild guild, ISocketMessageChannel channel)
        {
            IGuildUser discordUser = guild.GetUser(GetDiscordID(row));
            if (discordUser is null)
            {
                await channel.SendMessageAsync($"Row {row} could not be added to accept/deny queue. User has left Discord server.");
                return;
            }

            _signupStatus.Add(new StatusClass()
            {
                Accept = false,
                DenyReason = GetDenyReason(row),
                DiscordUser = discordUser as SocketGuildUser,
                StaffChannel = channel,
                RowNumber = row
            });

            await channel.SendMessageAsync($"Row {row} added to accept/deny queue. Queue will be ran in ~30 seconds");
        }

        Dictionary<string, string> _denySentences = new Dictionary<string, string>()
        {
            { "Not enough data on Tracker"          , "You've been declined due to not having enough data (games played) in one of the seasons." },
            { "Tracker broken"                      , "You've been declined due to the given main account not linking to a valid profile." },
            { "Smurf / Alt broken"                    , "You've been declined due to the given alt account not linking to a valid profile."},
            { "Not enough games"                    , "You've been declined due to not having enough games in the current season. Please notify an application member once you have enough games played. "},
            { "Too low rank"                        , "You've been declined due to your ranks being too low to join the league. "},
            { "Suspicious Signup / Investigation"   , "You've been declined due to your account showing too many irregularities. Please contact an application team member if you want a further explanation."}
        };

        private string GetDenyReason(int row)
        {
            return _latestValues[row - 1][17].ToString();
        }

        internal readonly string[] _allowedPlatforms = { "steam", "xbl", "psn", "xbox", "ps", "epic" };
        private List<int> lockedRows = new List<int>();

        private async Task CalculateDSN(IList<object> row, SheetsService service, int idx)
        {
            //Get Accounts from WebApp
            var r = await GetAccountsFromWebApp(int.Parse(row[(int)ColumnIDs.PlayerID].ToString()));
            //Filter Accounts
            r = r.Where(x => _allowedPlatforms.Contains(x.type) && x.active).ToArray();

            List<CalcData> CalcData = new List<CalcData>();

            string dsnCommand = $"!dsn {idx + 1} ";

            //Check data for each account.
            foreach (var account in r)
            {
                string username = "";

                account.type = ConvertPlatform(account.type);
                if (account.type == "steam")
                {
                    username = account.id;
                    username = username.Substring(username.LastIndexOf('/') + 1);
                    if (username.EndsWith("/"))
                        username.Remove(username.Length - 1);
                }
                if (account.type == "xbl" || account.type == "psn" || account.type == "epic") username = account.name;

                var trnResponse = await TRNRequest(account.type, username);
                dsnCommand += $"{account.type} {username} ";
                if (trnResponse is null) continue;
                CalcData.AddRange(trnResponse);
            }

            await CalcAndSendResponse(idx, CalcData);
            await InsertDSNCommand(dsnCommand, idx + 1);
        }

        private async Task InsertDSNCommand(string dsnCommand, int row)
        {
            ValueRange v = new ValueRange();
            v.MajorDimension = "ROWS";
            v.Values = new List<IList<object>> { new List<object>() { dsnCommand } };
            SpreadsheetsResource.ValuesResource.UpdateRequest u = service.Spreadsheets.Values.Update(v, SpreadsheetID, $"DSN Hub!AB{row}");//:O{idx+1}");
            u.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await u.ExecuteAsync();
        }

        public async Task CalcAndSendResponse(int idx, List<CalcData> CalcData, bool fromCommand = false)
        {
            if (fromCommand) idx -= 1;

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
            int s17Games = CalcData.Where(x => x.Season == 17).Select(x => x.GamesPlayed).Distinct().Sum();

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
                _log.Info($@"Cookies\r\n{ string.Join("\r\n", response.RequestMessage.Content)}");

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

                List<CalcData> Data = new List<CalcData>();

                if (platform != "epic")
                {
                    Data.Add(await GetCalcDataForSegmentAsync(platform, username, 15, Playlist.TWOS, mmrObj));
                    Data.Add(await GetCalcDataForSegmentAsync(platform, username, 15, Playlist.THREES, mmrObj));
                    Data.Add(await GetCalcDataForSegmentAsync(platform, username, 16, Playlist.TWOS, mmrObj));
                    Data.Add(await GetCalcDataForSegmentAsync(platform, username, 16, Playlist.THREES, mmrObj));
                    Data.Add(await GetCalcDataForSegmentAsync(platform, username, 17, Playlist.TWOS, mmrObj));
                    Data.Add(await GetCalcDataForSegmentAsync(platform, username, 17, Playlist.THREES, mmrObj));
                }
                else
                {
                    Data.Add(await GetCalcDataForSegmentAsync(platform, username, 17, Playlist.TWOS, mmrObj));
                    Data.Add(await GetCalcDataForSegmentAsync(platform, username, 17, Playlist.THREES, mmrObj));
                }

                return Data;
            }
        }

        async Task<CalcData> GetCalcDataForSegmentAsync(string platform, string username, int season, Playlist playlist, TRNMMRObject obj)
        {
            CalcData retVal = new CalcData();

            retVal.Playlist = playlist;
            retVal.Season = season;

            DateTime cutOff = DateTime.Now;
            DateTime seasonStartDate = new DateTime(2020, 09, 23);

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

    internal class StatusClass
    {
        public int RowNumber { get; set; }
        public bool Accept { get; set; }
        public SocketGuildUser DiscordUser { get; set; }
        public string DenyReason { get; set; }
        public ISocketMessageChannel StaffChannel { get; set; }
        public IRole[] RolesToAdd { get; internal set; }
    }
}

