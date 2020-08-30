using Discord.Commands;
using IELDiscordBotPOC.Classes.Database;
using IELDiscordBotPOC.Classes.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IELDiscordBotPOC.Classes.Modules
{
    public class ConfigModule : ModuleBase<SocketCommandContext>
    {
        private IELContext _db;
        public ConfigModule(IELContext db) : base()
        {
            _db = db;
        }

        [Command("config")]
        [Summary("Updates a config setting")]
        public async Task HandleConfigCommandAsync(string sub, string key, string value)
        {
            switch (sub)
            {
                case "Roles":
                case "Channels":
                    await HandleConfigSubCommandAsync(sub, key, value).ConfigureAwait(false);
                    return;
            }

        }

        private async Task HandleConfigSubCommandAsync(string sub, string key, string value)
        {
            DBConfigSettings config = _db.ConfigSettings.FirstOrDefault(config => config.Subsection == sub && config.Key == key);
            if (config != null)
            {
                if (string.IsNullOrEmpty(value))
                {
                    _db.Remove(config);
                    await Context.Channel.SendMessageAsync("Deleted DB Setting");
                    await _db.SaveChangesAsync().ConfigureAwait(false);
                    return;
                }

                config.Value = value;
                _db.Entry(config).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
            }
            else
            {
                if (string.IsNullOrEmpty(value))
                {
                    await Context.Channel.SendMessageAsync("New DB Setting cannot have blank value.\r\n" +
                        "Syntax: !config section key value\r\n" +
                        "Example: !config Channels Log #test-channel");
                    return;
                }

                config = new DBConfigSettings();
                config.Subsection = sub;
                config.Key = key;
                config.Value = value;

                _db.ConfigSettings.Add(config);
            }

            await _db.SaveChangesAsync().ConfigureAwait(false);
            await Context.Channel.SendMessageAsync("Updated DB Setting");
        }
    }
}
