﻿using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;


namespace GoogleSheetsAPIInterface.Network
{
    class GoogleAPI
    {
        private SheetsService service;
        private ServiceAccountCredential _sheetsCredential;
        const string ServiceAccountEmail = "ieldiscordbot@inspired-rock-284217.iam.gserviceaccount.com";

        string[] Scopes = { SheetsService.Scope.Spreadsheets };
        const string ApplicationName = "IEL Discord Bot .NET Application";
        const string SpreadsheetID = "1ozwketqZ4ZU9Dk2wyB20Yq8KDQXw1zA2EOUdXuuG7NY";

        Timer _timer;

        static GoogleAPI _instance;
        public static GoogleAPI Instance()
        {
            if (_instance is null)
                _instance = new GoogleAPI();
            return _instance;
        }

        public async Task Setup()
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

            _timer = new Timer(async _ =>
            {
                await GetLatestValues().ConfigureAwait(false);
            },
            null,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMinutes(5));
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

        private IList<IList<object>> _latestValues = null;

        private async Task GetLatestValues()
        {
            SpreadsheetsResource.ValuesResource.GetRequest request =
    service.Spreadsheets.Values.Get(SpreadsheetID, "DSN Hub!A:AH");

            ValueRange response = await request.ExecuteAsync().ConfigureAwait(false);

            _latestValues = response.Values;
        }


        internal string GetLeague(string discordUsername)
        {
            for (int row = 0; row < _latestValues.Count; row++)
            {
                IList<object> r = _latestValues[row];

                if (r[2].ToString().ToLower() == discordUsername.ToLower())
                {
                    return r[24].ToString();
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
