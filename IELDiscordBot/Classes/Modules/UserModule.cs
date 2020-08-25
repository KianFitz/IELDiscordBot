using Discord;
using Discord.Commands;
using IELDiscordBot.Classes.Models;
using IELDiscordBot.Classes.Utilities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace IELDiscordBotPOC.Classes.Modules
{
    public class UserModule : ModuleBase<SocketCommandContext>
    { 
        [Command("verify")]
        public async Task HandleVerifyCommandAsync()
        {
            Captcha c = new Captcha();
            c.UserID = Context.User.Id;
            c.ServerID = Context.Guild.Id;

            var captchaImage = CaptchaGenerator.Generate(ref c);
            captchaImage.Seek(0, System.IO.SeekOrigin.Begin);

            var message = await Context.User.SendFileAsync(captchaImage, "Captcha.jpeg").ConfigureAwait(false);
            c.MessageID = message.Id;

            Utilities.Utilities.OutstandingCaptchas.Add(c);
        }
    }
}
