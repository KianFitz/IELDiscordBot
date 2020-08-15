using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace IELDiscordBotPOC.Classes.Modules
{
    public class ModModule : ModuleBase<SocketCommandContext>
    {
        #region Custom Commands
        [Command("customcommand")]
        [Alias("cc")]
        public async Task HandleCustomCommandAsync(string op, string name, [Remainder] string text)
        {
            switch (op)
            {
                case "a":
                case "add":
                    await HandleAddCustomCommandAsync(name, text);
                    break;
                case "r":
                case "remove":
                    await HandleRemoveCustomCommandAsync(name);
                    break;
                case "u":
                case "update":
                    await HandleUpdateCustomCommandAsync(name, text);
                    break;
            }
        }

        private async Task HandleUpdateCustomCommandAsync(string name, string text)
        {
            throw new NotImplementedException();
        }

        private async Task HandleRemoveCustomCommandAsync(string name)
        {
            throw new NotImplementedException();
        }

        private async Task HandleAddCustomCommandAsync(string name, string text)
        {
            throw new NotImplementedException();
        }
        #endregion
        
    }
}
