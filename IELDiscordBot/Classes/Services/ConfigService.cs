using IELDiscordBot.Classes.Utilities;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace IELDiscordBot.Classes.Services
{
    public class ConfigService
    {
        public static IConfigurationRoot GetConfiguration()
        {

            try
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Utilities.Utilities.GetBasePath())
                    .AddJsonFile(Constants.ConfigFileName, false, true);

                return builder.Build();
            }
            catch
            {
                throw new FileNotFoundException();
            }
        }
    }
}
