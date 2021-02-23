using Discord.Commands;
using IELDiscordBot.Classes.Models;
using IELDiscordBot.Classes.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace IELDiscordBot.Classes.Modules
{
    public class CustomCommandModule : ModuleBase<SocketCommandContext>
    {
        private IELContext _db;
        public CustomCommandModule(IELContext db)
        {
            _db = db;
        }

        [Command("custom")]
        [Alias("cc")]
        public async Task HandleCustomCommandEditAsync(string op, string key, string value = null)
        {
            switch (op)
            {
                case "a":
                case "add":
                    await HandleCustomCommandAddAsync(key, value);
                    return;
                case "u":
                case "update":
                    await HandleCustomCommandUpdateAsync(key, value);
                    return;
                case "d":
                case "delete":
                    await HandleCustomCommandDeleteAsync(key);
                    return;
                default:
                    // SEND UNKNOWN OPCODE
                    break;
            }
        }

        private async Task HandleCustomCommandAddAsync(string key, string value)
        {
            CustomCommand cc = _db.CustomCommands.FirstOrDefault(x => x.Command == key);
            if (cc != null)
            {
                await Context.Channel.SendMessageAsync($"Custom Command: {key} already exists").ConfigureAwait(false);
            }
            else
            {
                // TODO: ADD Validation so that it can't overlap with real commands.

                cc = new CustomCommand
                {
                    Command = key,
                    ReturnValue = value
                };
                _db.Add(cc);
                await _db.SaveChangesAsync();
                await Context.Channel.SendMessageAsync($"Custom Command: {key} was created.").ConfigureAwait(false);
            }
        }

        private async Task HandleCustomCommandUpdateAsync(string key, string value)
        {
            CustomCommand cc = _db.CustomCommands.FirstOrDefault(x => x.Command == key);
            if (cc != null)
            {
                cc.ReturnValue = value;

                _db.Add(cc);
                await _db.SaveChangesAsync();
                await Context.Channel.SendMessageAsync($"Custom Command: {key} was updated.").ConfigureAwait(false);
            }
            else
            {
                await Context.Channel.SendMessageAsync($"Custom Command: {key} does not exist").ConfigureAwait(false);
            }
        }

        private async Task HandleCustomCommandDeleteAsync(string key)
        {
            CustomCommand cc = _db.CustomCommands.FirstOrDefault(x => x.Command == key);
            if (cc != null)
            {
                _db.Remove(cc);
                await _db.SaveChangesAsync();
                await Context.Channel.SendMessageAsync($"Custom Command: {key} was deleted.").ConfigureAwait(false);
            }
            else
            {
                await Context.Channel.SendMessageAsync($"Custom Command: {key} does not exist").ConfigureAwait(false);
            }
        }
    }
}
