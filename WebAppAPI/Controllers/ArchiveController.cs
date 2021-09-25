using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebAppAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ArchiveController : ControllerBase
    {
        private readonly ILogger<ArchiveController> _logger;
        public ArchiveController(ILogger<ArchiveController> logger)
        {
            _logger = logger;
        }

        private readonly string folder = Environment.GetEnvironmentVariable("ARCHIVE_EXPORT_PATH", EnvironmentVariableTarget.Machine);

        [HttpGet]
        public async Task GetAsync(string fileName, string category, bool darkMode)
        {
            Response.StatusCode = 302;
            Response.Redirect($"/archive/{category}/{(darkMode ? "Dark" : "Light")}/{fileName}.html");
        }
    }
}
