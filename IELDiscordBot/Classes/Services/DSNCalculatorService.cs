using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using IELDiscordBot.Classes.Models;
using IELDiscordBot.Classes.Models.DSN;
using IELDiscordBot.Classes.Models.DSN.Segments;
using IELDiscordBot.Classes.Models.WebAppAPI;
using IELDiscordBot.Classes.Utilities;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using NLog;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace IELDiscordBot.Classes.Services
{
    public class DSNCalculatorService
    {
        private readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly DiscordSocketClient _client;
        private readonly HttpClient _webClient;
        private readonly IConfigurationRoot _config;
        private readonly CommandService _commands;
        private readonly Timer _timer;
        private readonly Timer _queueTimer;
        private readonly List<int> _acceptablePlaylists = new List<int>() { 10, 11, 13 };
        private readonly List<StatusClass> _signupStatus;
        private static readonly string ErrorLog = "";
        private static readonly string AccountsChecked = "Accounts Checked: ";
        private ServiceAccountCredential _sheetsCredential;
        private readonly string[] Scopes = { SheetsService.Scope.Spreadsheets };
        private const string ApplicationName = "IEL Discord Bot .NET Application";
        private const string PlayerDataSpreadsheetID = "15suvNkf_knCo66J33L3L-SS6mwl6DTfv31TPJxeey7w";
        private const string GameAdminSpreadsheetID = "1TC0Z_n3uaAq1DIsrTunH3xrJwqBE9wZxMVrND18RnOM";
        private const string ServiceAccountEmail = "ieldiscordbot@inspired-rock-284217.iam.gserviceaccount.com";

        public enum Playlist
        {
            ONES = 10,
            TWOS = 11,
            THREES = 13,
        }

        public enum Seasons
        {
            S4 = 0,
            S5 = 1,
            S6 = 2,
            S7 = 3, 
            S8 = 4
        }

        private readonly DateTime[] cutOffDates = new DateTime[]
        {
            new DateTime(2021, 11, 07),
            new DateTime(2022, 03, 09),
            new DateTime(2022, 06, 15),
            new DateTime(2022, 09, 07),
            new DateTime(2024, 01, 01)
        };

        internal class DSNCalculationData
        {
            public string User;
            public string Platform;
            public int Season;
            public int GamesPlayed;
            public int MaxMMR;
        }
        public DSNCalculatorService(DiscordSocketClient client, IConfigurationRoot config, CommandService commands)
        {
            _client = client;
            _config = config;
            _commands = commands;
            _webClient = new HttpClient();
            _signupStatus = new List<StatusClass>();
            Setup();
            LoadDistributions();
            _timer = new Timer(async _ =>
           {
                // await ProcessNewSignupsAsync().ConfigureAwait(false);
           },
           null,
           TimeSpan.FromSeconds(5),
           TimeSpan.FromMinutes(30));
            _queueTimer = new Timer(async _ =>
            {
                await GetLatestValues().ConfigureAwait(false);
                await ProcessSignupQueueAsync().ConfigureAwait(false);
            },
           null,
           TimeSpan.FromSeconds(5),
           TimeSpan.FromSeconds(30));
        }

        private async Task ProcessSignupQueueAsync()
        {
            List<StatusClass> toProcess = new List<StatusClass>(_signupStatus);
            _signupStatus.Clear();
            ulong channelID = 977977301146955816;
            ulong GuildID = 564159012501717281;
            if (toProcess.Count == 0)
                return;


            IGuild guild = _client.GetGuild(GuildID);
            ITextChannel textChannel = await guild.GetTextChannelAsync(channelID);
            
            foreach (var signup in toProcess)
            {
                List<object> obj = new List<object>
                {
                    true,
                };

                if (signup.DiscordUser is null)
                {
                    MakeRequest($"RSC Player Data Hub!AG{signup.RowNumber}", obj);
                    obj[0] = false;
                    MakeRequest($"RSC Player Data Hub!AC{signup.RowNumber}", obj);
                    await signup.StaffChannel.SendMessageAsync($"Unable to process signup for row: {signup.RowNumber}, Player not in Discord");
                    continue;
                }

                if (signup.Accept)
                {
                    if (GetVerifiedByRow(signup.RowNumber) == false)
                    {
                        await signup.StaffChannel.SendMessageAsync($"Unable to process signup for row: {signup.RowNumber}, \"All Trackers Reviewed\" box not checked!");
                        continue;
                    }

                    await textChannel.SendMessageAsync($"{signup.DiscordUser.Mention} you have been accepted into the RSC!");
                }
                else
                {
                    if (signup.DenyReason == "")
                    {
                        await signup.StaffChannel.SendMessageAsync($"Unable to process signup for row: {signup.RowNumber}, \"Deny reason\" not selected.");
                        continue;
                    }

                    await textChannel.SendMessageAsync($"{signup.DiscordUser.Mention} {_denySentences[signup.DenyReason]}");
                }
                MakeRequest($"RSC Player Data Hub!AG{signup.RowNumber}", obj);
                if (signup.Accept == false) continue;

                if (signup.DiscordUser.Nickname != null && signup.DiscordUser.Nickname.Contains("|") == false)
                {
                    await signup.DiscordUser.AddRolesAsync(signup.RolesToAdd);
                    await signup.DiscordUser.ModifyAsync(x =>
                        {
                            x.Nickname = $"FA | {(x.Nickname.IsSpecified ? x.Nickname : signup.DiscordUser.Username)}";
                        });
                }
                //await SendStatusToWebApp(int.Parse(_playerDataLatestValues[signup.RowNumber - 1][2].ToString()), signup.Accept).ConfigureAwait(false);
                await Task.Delay(2000);
            }
        }

        internal int GetRowsToRecalculate()
        {
            int retVal = 0;
            for (int row = 1; row < _playerDataLatestValues.Count; row++)
            {
                IList<object> r = _playerDataLatestValues[row];
                if (string.IsNullOrEmpty(r[(int)ColumnIDs.Name].ToString())) continue;
                if (!string.IsNullOrEmpty(r[(int)ColumnIDs.DSN].ToString()))
                {
                    retVal++;
                }
            }
            return retVal;
        }

        internal async Task ForceRecalcRow(int currentRow)
        {
            IList<object> signup = _playerDataLatestValues[currentRow - 1];
            await CalculateDSN(signup, service, currentRow - 1);
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
            var status = signup[34].ToString();
            var r = await GetAccountsFromWebApp(int.Parse(signup[(int)ColumnIDs.PlayerID].ToString()));
            r = r.Where(x => _allowedPlatforms.Contains(x.type) && x.active).ToArray();
            var platformLinks = string.Join("\r\n", r.Select(x => x.type + ": " + (x.type == "steam" ? x.id : x.name)));

            var message = await channel.SendMessageAsync("", false, Embeds.SignupDetails(profileName, id, profileLink, status, platformLinks, requestor)).ConfigureAwait(false);
        }

        internal async Task RecheckSignup(ulong discordId, ISocketMessageChannel channel)
        {
            IList<object> signup = null;
            int i = 0;
            for (i = 0; i < _playerDataLatestValues.Count; i++)
            {
                if (_playerDataLatestValues[i][(int)ColumnIDs.Discord].ToString() == discordId.ToString())
                {
                    signup = _playerDataLatestValues[i];
                    break;
                }
            }

            if (signup is null)
            {
                await channel.SendMessageAsync("Unable to find your signup.");
                return;
            }

            if (!_denySentences.ContainsKey(signup[30].ToString())) {
                return;
            }

            MakeRequest($"Player Data Hub!AF{i+1}", new List<object>() { false });

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
            var retVal = _playerDataLatestValues.FirstOrDefault(x => x[(int)ColumnIDs.Discord].ToString() == discordId.ToString());
            return retVal;
        }

        private bool GetVerifiedByRow(int row)
        {
            return bool.Parse(_playerDataLatestValues[row - 1][36].ToString());
        }

        private enum ColumnIDs : int
        {
            Name = 0,
            Discord = 1,
            PlayerID = 2,
            GamesPlayed = 6,

            ProfileLink = 7,
            DSN = 14,
            ApplicationStatus = 34,
            League = 38,
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
        private IList<IList<object>> _playerDataLatestValues = null;
        private IList<IList<object>> _gameAdminLatestValues = null;

        private async Task GetLatestValues()
        {
            SpreadsheetsResource.ValuesResource.GetRequest request =
    service.Spreadsheets.Values.Get(PlayerDataSpreadsheetID, "RSC Player Data Hub!A:BC");

            ValueRange response = await request.ExecuteAsync().ConfigureAwait(false);

            _playerDataLatestValues = response.Values;
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

        public async Task SendStatusToWebApp(int playerId, bool accepted)
        {
            var values = new Dictionary<string, string>()
            {
                { "userId", playerId.ToString() },
                { "status", accepted ? "accepted" : "declined" }
            };

            var content = new FormUrlEncodedContent(values);

            await _webClient.PutAsync("https://webapp.imperialesportsleague.co.uk/api/signup/status", content);
        }

        internal async Task QueueAccept(int row, SocketGuild guild, ISocketMessageChannel channel)
        {
            //var roles = GetRoles(guild, row);

            var roles = new IRole[] { guild.GetRole(793599335446478931) };


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
            IGuildUser guildUser = guild.GetUser(GetDiscordID(row-1));

            if (guildUser is null)
            {
                return null;
            }

            Dictionary<string, IRole> faRoles = new Dictionary<string, IRole>()
            {
                { "Academy", guild.GetRole(797537384022409256) },
                { "Prospect", guild.GetRole(670231374896168960) },
                { "Challenger", guild.GetRole(670230994627854347) },
                { "Master", guild.GetRole(671808027313045544) }
            };


            //Dictionary<string, IRole> playerRoles = new Dictionary<string, IRole>()
            //{
            //    { "Academy", guild.GetRole(797537428696989767) },
            //    { "Prospect", guild.GetRole(712599931382136842) },
            //    { "Challenger", guild.GetRole(712599930866237531) },
            //    { "Master", guild.GetRole(712599928890720284) }
            //};


            string league = _playerDataLatestValues[row-1][(int)ColumnIDs.League].ToString();

            IRole faRole = faRoles[league];
            //IRole playerRoleToAssigned = playerRoles[league];
            IRole genericFARole = guild.GetRole(978785891487191061);

            return new IRole[] { genericFARole, faRole };
        }

        internal ulong GetDiscordID(int row)
        {
            return ulong.Parse(_playerDataLatestValues[row - 1][(int)ColumnIDs.Discord].ToString());
        }

        public async Task AssignLeagueFARoles(ISocketMessageChannel channel, IGuild guild)
        {
            int remaining = _playerDataLatestValues.Where(x => x[0].ToString() != "").Count();
            Dictionary<string, int> _assignedCounters = new Dictionary<string, int>();

            var allUsers = await guild.GetUsersAsync().ConfigureAwait(false);
            IRole genericFARole = guild.GetRole(978785891487191061);

            Dictionary<string, IRole> faRoles = new Dictionary<string, IRole>()
            {
                { "Academy", guild.GetRole(797537384022409256) },
                { "Prospect", guild.GetRole(670231374896168960) },
                { "Challenger", guild.GetRole(670230994627854347) },
                { "Master", guild.GetRole(671808027313045544) }
            };
            await GetLatestValues().ConfigureAwait(false);

            foreach (var user in allUsers)
            {
                try
                {
                    if (user.RoleIds.Contains(genericFARole.Id))
                    {
                        var userId = user.Id;
                        var signup = GetSignupByDiscordId(userId);
                        string league = signup[(int)ColumnIDs.League].ToString();
                        var role = faRoles[league];

                        foreach (var faRole in faRoles)
                        {
                            if (user.RoleIds.Contains(faRole.Value.Id))
                            {
                                if (faRole.Value.Id != role.Id)
                                {
                                    await channel.SendMessageAsync($"Changed {user.Id} from role {faRole.Value.Name} to {role.Name}");
                                    await user.RemoveRoleAsync(faRole.Value).ConfigureAwait(false);
                                }
                            }
                        }
                        await user.AddRoleAsync(role).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    await channel.SendMessageAsync($"User: {user.Id} - Exception: {ex.Message}");
                }
            }
        }

        private async Task ProcessNewSignupsAsync()
        {
            ulong GuildId = 564159012501717281;
            ulong StaffLogId = 564165986584887299;

            IGuild guild = _client.GetGuild(GuildId);
            ITextChannel staffLogChannel = await guild.GetTextChannelAsync(StaffLogId);

            SpreadsheetsResource.ValuesResource.GetRequest request =
                service.Spreadsheets.Values.Get(PlayerDataSpreadsheetID, "RSC Player Data Hub!A:BC");

            ValueRange response = await request.ExecuteAsync().ConfigureAwait(false);

            IList<IList<object>> playerDataHubValues = response.Values;

            request = service.Spreadsheets.Values.Get(PlayerDataSpreadsheetID, "PlayerData!A:AQ");
            response = await request.ExecuteAsync().ConfigureAwait(false);

            IList<IList<object>> playerDataValues = response.Values;


            for (int row = 0; row < playerDataHubValues.Count; row++)
            {
                IList<object> r = playerDataHubValues[row];
                if (lockedRows.Contains(row)) continue;
                if (string.IsNullOrEmpty(r[(int)ColumnIDs.Name].ToString())) continue;
                if (string.IsNullOrEmpty(r[(int)ColumnIDs.GamesPlayed].ToString()))
                {
                    _log.Info($"Started DSN Calculation for User: {r[(int)ColumnIDs.Name]}");
                    var message = await staffLogChannel.SendMessageAsync("Started CMV Calculation for user: " + r[(int)ColumnIDs.Name]);

                    lockedRows.Add(row);

                    var msg = await staffLogChannel.SendMessageAsync(playerDataValues[row][8].ToString());
                    await Task.Delay(2000);
                    await msg.DeleteAsync();
                    //await CalculateDSN(r, service, row).ConfigureAwait(false);
                    await message.ModifyAsync(x => x.Content = "~~Finished CMV Calculation for user: " + r[(int)ColumnIDs.Name] + "~~" + " Done!").ConfigureAwait(false);

                    _log.Info($"Completed DSN Calculation for User: {r[(int)ColumnIDs.Name]}");
                    await Task.Delay(new Random().Next(10000, 15000));
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

            var newStatus = new StatusClass()
            {
                Accept = false,
                DenyReason = GetDenyReason(row),
                DiscordUser = discordUser as SocketGuildUser,
                StaffChannel = channel,
                RowNumber = row
            };

            if (string.IsNullOrEmpty(newStatus.DenyReason))
            {
                await channel.SendMessageAsync($"I do not know a deny message for the status of application on row {row}");
                return;
            }

            _signupStatus.Add(newStatus);

            await channel.SendMessageAsync($"Row {row} added to accept/deny queue. Queue will be ran in ~30 seconds");
        }

        //private readonly Dictionary<string, string> _denySentences = new Dictionary<string, string>()
        //{
        //    { "Tracker Broken"                      , "You have been denied because the account(s) you have given do not link to a valid tracker. Check your accounts using the !signup command." },
        //    { "Not All Accounts Accessible"         , "You have been denied because the account(s) you have given do not link to a valid tracker. Check your accounts using the !signup command." },
        //    { "Tracker Inconsistencies"             , "You have been denied because your account is showing too many irregularities. Please open a modmail for a further explanation."},
        //    { "Games Required"                      , "You have been denied because you do no meet the minimum games requirement for the current season, you need 150 games played in Season 6. Once you have reached this requirement please head to #bot-commands and run the !rechecksignup command."},
        //    { "Games Required NOT reachable"        , "You have been denied because there is not enough data available on your tracker to reach the minimum game requirement. Check that all your accounts are on your signup using the !signup command. After updating your accounts run the !rechecksignup command. Or open a modmail for more information."},
        //    { "Not enough Games total"              , "You have been denied because you have not reached the required amount of total games over the past 5 seasons, we require 500 games played in total in the past 5 seasons."},
        //    { "Not enough Games in 2s and 3s"       , "You have been denied because not enough of your total games have been played in 2s and 3s, we require 450/500 over the past 5 seasons."},
        //};

        private readonly Dictionary<string, string> _denySentences = new Dictionary<string, string>()
        {
            { "Bot Issue"                                   , "" },
            { "Denied - Alt Discord Account"                , "Denied as the discord account is an alt." },
            { "Denied - Banned / Smurf"                     , "Player banned from the discord or smurfing." },
            { "Denied - Data Insufficient"                  , "You have been denied because there is not enough data available on your tracker to reach the minimum game requirement. Check that all your accounts and open a modmail for more information." },
            { "Games Required"                              , "You have been denied because you do no meet the minimum games requirement for the current season, you need to play 250 games (200 as returning player) in the last two seasons in 2s and 3s combined." },
            { "Pending Data"                                , "Bot is fetching data." },
            { "Pending Review"                              , "The tracker team is reviewing your accounts." },
            { "Player is not in the RSC Discord"            , "Player is not in the RSC Discord." },
            { "Tracker Broken"                              , "You have been denied because the account(s) you have given do not link to a valid tracker." },
            { "Tracker Inconsistencies"                     , "There have been inconsistencies in your tracker graph. Please open a modmail for more information." },
            { "Trackers Inaccessible"                       , "You have been denied because the account(s) you have given do not link to a valid tracker. Open a modmail for more information." },
        };

        private string GetDenyReason(int row)
        {
            if (_denySentences.ContainsKey(_playerDataLatestValues[row - 1][37].ToString())) {
                return _playerDataLatestValues[row - 1][30].ToString();
            }
            return String.Empty;
        }

        internal readonly string[] _allowedPlatforms = { "steam", "xbl", "psn", "xbox", "ps", "epic" };
        private readonly List<int> lockedRows = new List<int>();

        private async Task CalculateDSN(IList<object> row, SheetsService service, int idx)
        {
            ChromeOptions options = new ChromeOptions();
            options.AddArgument("headless");
            options.AddArgument("no-sandbox");
            options.AddArgument("disable-extensions");
            options.AddArgument("disable-gpu");
            options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/84.0.4147.125 Safari/537.36");

            ChromeDriver driver = new ChromeDriver(options);
            
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

                var trnResponse = await TRNRequest(account.type, username, driver);
                dsnCommand += $"{account.type} {username} ";
                if (trnResponse is null)
                {
                    MakeRequest($"Player Data Hub!AB{idx + 1}", new List<object>() { false });
                    continue;
                }
                CalcData.AddRange(trnResponse);
            }

            await CalcAndSendResponse(idx, CalcData);
            await InsertDSNCommand(dsnCommand, idx + 1);

            driver.Quit();
        }

        private async Task InsertDSNCommand(string dsnCommand, int row)
        {
            ValueRange v = new ValueRange
            {
                MajorDimension = "ROWS",
                Values = new List<IList<object>> { new List<object>() { dsnCommand } }
            };
            SpreadsheetsResource.ValuesResource.UpdateRequest u = service.Spreadsheets.Values.Update(v, PlayerDataSpreadsheetID, $"Player Data Hub!AK{row}");//:O{idx+1}");
            u.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await u.ExecuteAsync();
        }

        public async Task CalcAndSendResponse(int idx, List<CalcData> CalcData, bool fromCommand = false)
        {
            if (fromCommand) idx -= 1;

            int highestSeason = 0;
            int highestPeak = 0;

            // Highest Season
            for (int i = 0; i < CalcData.Count; i++)
            {
                if (CalcData[i].Ratings == null || CalcData[i].Ratings.Count == 0) continue;
                int maxPeakFromSeason = CalcData[i].Ratings.Max();
                if (maxPeakFromSeason > highestPeak)
                {
                    highestSeason = CalcData[i].Season;
                    highestPeak = maxPeakFromSeason;
                }
            }

            List<int> peaks = new List<int>();

            for (int season = Constants.START_SEASON; season <= Constants.END_SEASON; season++)
            {
                int peak = 0;

                foreach (var section in CalcData)
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

            int s2games = CalcData.Where(x => x.Season == 18).Select(x => x.GamesPlayed).Distinct().Sum();
            int s3games = CalcData.Where(x => x.Season == 19).Select(x => x.GamesPlayed).Distinct().Sum();
            int s4games = CalcData.Where(x => x.Season == 20).Select(x => x.GamesPlayed).Distinct().Sum();
            int s5games = CalcData.Where(x => x.Season == 21).Select(x => x.GamesPlayed).Distinct().Sum();
            int s6games = CalcData.Where(x => x.Season == 22).Select(x => x.GamesPlayed).Distinct().Sum();

            int totalGames = s2games + s3games + s4games + s5games + s6games;
            int totalOnes = CalcData.Where(x => x.Playlist == Playlist.ONES).Select(x => x.GamesPlayed).Sum();
            int totalGamesNoOnes = totalGames - totalOnes;

            List<object> obj = new List<object>();
            obj.Add(s2games);
            obj.Add(s3games);
            obj.Add(s4games);
            obj.Add(s5games);
            obj.Add(s6games);
            obj.Add(0);
            obj.Add(0);
            obj.Add(CalcData.Where(x => (x.Season == 22 || x.Season == 21) && x.Playlist != Playlist.ONES).Select(x => x.GamesPlayed).Distinct().Sum());
            obj.Add(CalcData.Where(x => x.Season == 22 || x.Season == 21).Select(x => x.GamesPlayed).Distinct().Sum());
            obj.Add(totalGamesNoOnes);
            obj.Add(totalGames);

            ValueRange v = new ValueRange
            {
                MajorDimension = "ROWS",
                Values = new List<IList<object>> { obj }
            };

            SpreadsheetsResource.ValuesResource.UpdateRequest u = service.Spreadsheets.Values.Update(v, PlayerDataSpreadsheetID, $"RSC Player Data Hub!G{idx + 1}");//:O{idx+1}");
            u.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await u.ExecuteAsync();

            obj = new List<object>();
            obj.Add(peaks[0]);
            obj.Add(peaks[1]);
            obj.Add(peaks[2]);
            obj.Add(peaks[3]);
            obj.Add(peaks[4]);
            obj.Add(0);
            obj.Add(0);

            v = new ValueRange
            {
                MajorDimension = "ROWS",
                Values = new List<IList<object>> { obj }
            };

            u = service.Spreadsheets.Values.Update(v, PlayerDataSpreadsheetID, $"RSC Player Data Hub!S{idx + 1}");//:O{idx+1}");
            u.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await u.ExecuteAsync();
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

        public async Task<List<CalcData>> TRNRequest(string platform, string username, ChromeDriver driver)
        {
            platform = ConvertPlatform(platform);
            if (platform == "steam")
            {
                username = username.Substring(username.LastIndexOf('/') + 1);
                if (username.EndsWith("/"))
                    username.Remove(username.Length - 1);
            }

            string apistring = string.Format(Constants.TRNAPI, platform, username);

            driver.Navigate().GoToUrl(apistring);
            string content = driver.PageSource;

            if (string.IsNullOrEmpty(content)) return null;
            if (content.ToLower().Contains("we could not find the player"))
                return null;

            content = content.Replace("<html><head><meta name=\"color-scheme\" content=\"light dark\"></head><body><pre style=\"word-wrap: break-word; white-space: pre-wrap;\">", "");
            content = content.Replace("</pre></body></html>", "");

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
            driver.Navigate().GoToUrl(apistring);
            content = driver.PageSource;
            content = content.Replace("<html><head><meta name=\"color-scheme\" content=\"light dark\"></head><body><pre style=\"word-wrap: break-word; white-space: pre-wrap;\">", "");
            content = content.Replace("</pre></body></html>", "");

            content = MakeJSONFriendly(content);

            TRNMMRObject mmrObj = JsonConvert.DeserializeObject<TRNMMRObject>(content);

            List<CalcData> Data = new List<CalcData>();
            for (int i = Constants.START_SEASON; i <= Constants.END_SEASON; i++)
            {
                Data.Add(await GetCalcDataForSegmentAsync(platform, username, i, Playlist.ONES, mmrObj, driver));
                Data.Add(await GetCalcDataForSegmentAsync(platform, username, i, Playlist.TWOS, mmrObj, driver));
                Data.Add(await GetCalcDataForSegmentAsync(platform, username, i, Playlist.THREES, mmrObj, driver));
            }

            return Data;
        }

        private async Task<CalcData> GetCalcDataForSegmentAsync(string platform, string username, int season, Playlist playlist, TRNMMRObject obj, ChromeDriver driver)
        {
            CalcData retVal = new CalcData
            {
                Playlist = playlist,
                Season = season
            };

            DateTime cutOff = DateTime.Now;
            DateTime seasonStartDate = new DateTime(2020, 12, 09);

            switch (season)
            {
                case 18:
                    {
                        cutOff = cutOffDates[(int)Seasons.S4];
                        break;
                    }
                case 19:
                    {
                        cutOff = cutOffDates[(int)Seasons.S5];
                        seasonStartDate = cutOffDates[(int)Seasons.S4].AddDays(1);
                        break;
                    }
                case 20:
                    {
                        cutOff = cutOffDates[(int)Seasons.S6];
                        seasonStartDate = cutOffDates[(int)Seasons.S5].AddDays(1);
                        break;
                    }
                case 21:
                    {
                        cutOff = cutOffDates[(int)Seasons.S7];
                        seasonStartDate = cutOffDates[(int)Seasons.S6].AddDays(1);
                        break;
                    }
                case 22:
                    {
                        cutOff = cutOffDates[(int)Seasons.S8];
                        seasonStartDate = cutOffDates[(int)Seasons.S7].AddDays(1);
                        break;
                    }
            }

            try
            {
                List<Datum> Datam = new List<Datum>();
                var segment = await GetSeasonSegment(season, platform, username, driver);
                if (segment == null)
                    retVal.GamesPlayed = 0;
                else
                {
                    Datam.AddRange(segment.data);
                    Datam.RemoveAll(x => _acceptablePlaylists.Contains(x.attributes.playlistId) == false);
                    retVal.GamesPlayed = Datam.Count > 0 ? Datam.Sum(x => x.stats.matchesPlayed.value) : 0;
                }

                if (playlist == Playlist.ONES)
                {
                    if (obj.data.Duel != null)
                    {
                        List<Duel> data = new List<Duel>(obj.data.Duel);
                        data = data.Where(x => x.collectDate < cutOff & x.collectDate > seasonStartDate).ToList();
                        retVal.Ratings = data.Select(x => x.rating).ToList();

                        HandleOnesRatings(ref retVal);
                    }
                    if (season == 18)
                    {
                        var ones = Datam.Find(x => x.attributes.playlistId == (int)Playlist.ONES);
                        if (ones != null)
                        {
                            retVal.Ratings.Add(ones.stats.rating.value);
                        }
                    }

                }
                else if (playlist == Playlist.TWOS)
                {
                    if (obj.data.Duos != null)
                    {
                        List<Duo> data = new List<Duo>(obj.data.Duos);
                        data = data.Where(x => x.collectDate < cutOff && x.collectDate > seasonStartDate).ToList();
                        retVal.Ratings = data.Select(x => x.rating).ToList();
                    }

                    if (season == 18)
                    {
                        var twos = Datam.Find(x => x.attributes.playlistId == (int)Playlist.TWOS);
                        if (twos != null)
                        {
                            retVal.Ratings.Add(twos.stats.rating.value);
                        }
                    }
                }
                else if (playlist == Playlist.THREES)
                {
                    if (obj.data.Standard != null)
                    {
                        List<Standard> data = new List<Standard>(obj.data.Standard);
                        data = data.Where(x => x.collectDate < cutOff && x.collectDate > seasonStartDate).ToList();
                        retVal.Ratings = data.Select(x => x.rating).ToList();
                    }

                    if (season == 18)
                    {
                        var threes = Datam.Find(x => x.attributes.playlistId == (int)Playlist.THREES);
                        if (threes != null)
                        {
                            retVal.Ratings.Add(threes.stats.rating.value);
                        }
                    }
                }
                if (Datam is null || Datam.Count == 0)
                {
                    retVal.GamesPlayed = 0;
                    return retVal;
                }
                switch (playlist)
                {
                    case Playlist.ONES:
                        retVal.GamesPlayed = Datam[0].stats.matchesPlayed.value;
                        break;
                    case Playlist.TWOS:
                        retVal.GamesPlayed = Datam[1].stats.matchesPlayed.value;
                        break;
                    case Playlist.THREES:
                        retVal.GamesPlayed = Datam[2].stats.matchesPlayed.value;
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex);
            }

            return retVal;
        }

        private void HandleOnesRatings(ref CalcData retVal)
        {
            var copy = new List<int>(retVal.Ratings);
            var newValues = new List<int>();

            foreach (var val in copy)
                newValues.Add(ConvertValue(val));

            retVal.Ratings = newValues;
        }

        class Distribution
        {
            public int MinValue;
            public int MaxValue;

            public Distribution(int min, int max)
            {
                MinValue = min;
                MaxValue = max;
            }
        }

        private int ConvertValue(int val)
        {
            for (int i = 0; i < _onesDistributions.GetLength(0); i++)
            {
                for (int j = 0; j < _onesDistributions.GetLength(1); j++)
                {
                    Distribution ones = _onesDistributions[i, j];
                    if (ones is null) continue;

                    if (val > ones.MinValue && val <= ones.MaxValue)
                    {
                        Distribution threes = _threesDistribution[i, j];

                        return Convert.ToInt32((val - ones.MinValue) * (threes.MaxValue - threes.MinValue) / (ones.MaxValue - ones.MinValue) + threes.MinValue);
                    }
                }
            }

            return val;
            //throw new ArgumentException($"Value {val} does not fall into any of the brackets");
        }

        private static Distribution[,] _onesDistributions;
        private static Distribution[,] _threesDistribution;

        async Task LoadDistributions()
        {
            _onesDistributions = new Distribution[,]
            {
                {new Distribution(995, 1052), new Distribution(1053, 1106), new Distribution(1107, 1171)},
                {new Distribution(1172, 1225), new Distribution(1226, 1290), new Distribution(1291, 1347)},
                {new Distribution(1348, 1535), null, null }
            };

            _threesDistribution = new Distribution[,]
            {
                {new Distribution(1075, 1187), new Distribution(1188, 1307), new Distribution(1308, 1427)},
                {new Distribution(1428, 1566), new Distribution(1567, 1707), new Distribution(1708, 1862)},
                {new Distribution(1863, 2001), null, null },
            };
        }

        private async Task<TRNSegment> GetSeasonSegment(int season, string platform, string user, ChromeDriver driver)
        {
            string apiString = string.Format(Constants.TRNSEGMENTAPI, platform, user, season);
            driver.Navigate().GoToUrl(apiString);
            string responseString = driver.PageSource;
            responseString = responseString.Replace("<html><head><meta name=\"color-scheme\" content=\"light dark\"></head><body><pre style=\"word-wrap: break-word; white-space: pre-wrap;\">", "");
            responseString = responseString.Replace("</pre></body></html>", "");
            return JsonConvert.DeserializeObject<TRNSegment>(responseString);
        }
        public struct CalcData
        {
            internal int Season;
            internal Playlist Playlist;
            internal List<int> Ratings;
            internal int GamesPlayed;
        }

        /// <summary>
        /// Execute a request on the Google Spreadsheet API
        /// </summary>
        /// <param name="sectionToEdit">The cell(s) to edit. Example: Sheet1!A1:A1</param>
        /// <param name="listOfValues">List of values in order to put into the request.</param>
        /// <returns></returns>
        public void MakeRequest(string sectionToEdit, List<object> listOfValues)
        {
            ValueRange v = new ValueRange
            {
                MajorDimension = "ROWS",
                Values = new List<IList<object>> { listOfValues }
            };
            SpreadsheetsResource.ValuesResource.UpdateRequest u = service.Spreadsheets.Values.Update(v, PlayerDataSpreadsheetID, sectionToEdit);//:O{idx+1}");
            u.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            UpdateValuesResponse res = u.Execute();
        }

        /// <summary>
        /// Searches the latest values that were grabbed from the Google Spreadsheet and finds the league of user with the given username.
        /// </summary>
        /// <param name="discordUsername">Discord Username in format [Username#Discriminator]</param>
        /// <returns></returns>
        //internal string GetLeague(string discordUsername)
        //{
        //    for (int row = 0; row < _latestValues.Count; row++)
        //    {
        //        IList<object> r = _latestValues[row];

        //        if (r[(int)ColumnIDs.Discord].ToString().ToLower() == discordUsername.ToLower())
        //        {
        //            return r[(int)ColumnIDs.League].ToString();
        //        }
        //    }

        //    return "";
        //}

        /// <summary>
        /// Searches the latest values that were grabbed from the Google Spreadsheet and finds the user with the given ID
        /// </summary>
        /// <param name="discordId">Discord ID of the user</param>
        /// <returns></returns>
        internal int GetRowNumber(ulong discordId)
        {
            for (int row = 0; row < _playerDataLatestValues.Count; row++)
            {
                IList<object> r = _playerDataLatestValues[row];

                if (ulong.Parse(r[3].ToString()) == discordId)
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

