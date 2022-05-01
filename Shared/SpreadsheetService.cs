using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Shared
{
    public class SpreadsheetService
    {
        private ServiceAccountCredential _sheetsCredential;
        private readonly string[] Scopes = { SheetsService.Scope.Spreadsheets };
        private const string ApplicationName = "IEL Discord Bot .NET Application";
        private const string SpreadsheetID = "13bG8ZQX4DsgcsIWgnh3uJRpxsrhbtu0UtH94WQ_hixk";
        private const string ServiceAccountEmail = "ieldiscordbot@inspired-rock-284217.iam.gserviceaccount.com";
        private SheetsService service;
        private IList<IList<object>> _latestValues = null;
        private int _rowToEdit = 2;


        public async Task MakeRequest(string sectionToEdit, List<object> obj, bool incrementRow = true)
        {
            ValueRange v = new ValueRange
            {
                MajorDimension = "ROWS",
                Values = new List<IList<object>> { obj }
            };
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
            _rowToEdit += _latestValues.Count - 1;
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

    }
}
