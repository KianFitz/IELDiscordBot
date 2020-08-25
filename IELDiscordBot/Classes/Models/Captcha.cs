using System;
using System.Collections.Generic;
using System.Text;

namespace IELDiscordBot.Classes.Models
{
    public class Captcha
    {
        public ulong UserID { get; set; }
        public ulong ServerID { get; set; }
        public string CaptchaCode { get; set; }
        public ulong MessageID { get; set; }
    }
}
