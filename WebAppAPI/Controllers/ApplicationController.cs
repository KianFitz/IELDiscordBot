using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebAppAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ApplicationController
    {
        private readonly ILogger<ApplicationController> _logger;
        public ApplicationController(ILogger<ApplicationController> logger)
        {
            _logger = logger;
        }

        [HttpPost]
        public async Task<int> InsertSignup([FromBody] Signup signup)
        {
            try
            {
                List<object> objList = new List<object>();

                objList.Add(DateTime.Now);
                objList.Add(signup.user.id);
                objList.Add(signup.user.name);
                objList.Add("'" + signup.user.discordId);
                objList.Add(signup.user.location);
                objList.Add(@$"https://webapp.imperialesportsleague.co.uk/user/{signup.user.id}");
                objList.Add(signup.user.referral);
                objList.Add("@null");

                int rowNumber = SpreadsheetService.Instance().GetNextAvailableRow();
                await SpreadsheetService.Instance().MakeRequest($"Player Data!A:H{rowNumber}", objList).ConfigureAwait(false);

                return 200;
            }
            catch (Exception)
            {
                return 500;
            }
        }
    }

    public class Signup
    {
        public int id;
        public string feedback;
        public User user;
        public Platform[] platforms;
    }

    public class User
    {
        public int id;
        public string name;
        public ulong discordId;
        public string location;
        public string referral;
    }

    public class Platform
    {
        public int id;
        public string type;
        public string? platform_id;
        public string? platform_type;
        public bool active;
    }
}
