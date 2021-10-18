using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
        public async Task<string> InsertSignup([FromBody] Signup signup)
        {
            try
            {
                List<object> objList = new List<object>
                {
                    DateTime.Now.ToLongDateString(),
                    signup.user.id,
                    signup.user.name,
                    "'" + signup.user.discordId,
                    signup.user.location,
                    @$"https://webapp.imperialesportsleague.co.uk/user/{signup.user.id}",
                    signup.user.referral,
                    signup.feedback
                };
                objList.AddRange(signup.socialAccounts.Where(x => x.active && x.type == "twitter").Select(x => x.platform_name).Take(17));

                int rowNumber = SpreadsheetService.Instance().GetNextAvailableRow();
                await SpreadsheetService.Instance().MakeRequest($"Player Data!A:Y{rowNumber}", objList).ConfigureAwait(false);

                return "200";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }

    public class Signup
    {
        public int id;
        public string feedback;
        public User user;
        public Platform[] platforms;
        public SocialAccount[] socialAccounts;
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
        public string? platform_name;
        public bool active;
    }

    public class SocialAccount
    {
        public int id;
        public string type;
        public string? platform_id;
        public string? platform_name;
        public bool active;
    }
}
