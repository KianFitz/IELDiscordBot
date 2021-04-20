﻿using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    public class SpreadsheetService
    {
        ServiceAccountCredential _sheetsCredential;
        string[] Scopes = { SheetsService.Scope.Spreadsheets };
        const string ApplicationName = "IEL Discord Bot .NET Application";
        const string SpreadsheetID = "1ry60q_1gWJAZcgwcZBP9b9LHd5TmzZxfDLd-xeYyoHs";
        const string ServiceAccountEmail = "ieldiscordbot@inspired-rock-284217.iam.gserviceaccount.com";
        private SheetsService service;
        private IList<IList<object>> _latestValues = null;
        private int _rowToEdit = 2;


        public async Task MakeRequest(string sectionToEdit, List<object> obj, bool incrementRow = true)
        {
            ValueRange v = new ValueRange();
            v.MajorDimension = "ROWS";
            v.Values = new List<IList<object>> { obj };
            SpreadsheetsResource.ValuesResource.UpdateRequest u = service.Spreadsheets.Values.Update(v, SpreadsheetID, sectionToEdit);//:O{idx+1}");
            u.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            UpdateValuesResponse res = u.Execute();

            if (incrementRow) _rowToEdit++;
        }
        private void GetLatestValues()
        {
            SpreadsheetsResource.ValuesResource.GetRequest request =
    service.Spreadsheets.Values.Get(SpreadsheetID, "Player Data!A:AH");

            ValueRange response = request.Execute();

            _latestValues = response.Values;
        }


        public int GetNextAvailableRow()
        {
            return _rowToEdit;
        }

        private SpreadsheetService()
        {
            Setup();
            GetLatestValues();
        }

        private static SpreadsheetService _instance;
        public static SpreadsheetService Instance()
        {
            if (_instance is null)
                _instance = new SpreadsheetService();
            return _instance;
        }

        private void Setup()
        {
            var certificate = new X509Certificate2($@"D:\Old PC\Development\IEL\IELDiscordBotOrig\WebAppAPI\bin\Debug\netcoreapp3.1\key.p12", "notasecret", X509KeyStorageFlags.Exportable);
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

    }
}