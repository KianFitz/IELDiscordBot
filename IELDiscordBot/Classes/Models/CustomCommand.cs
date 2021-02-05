using System;
using System.Collections.Generic;
using System.Text;

namespace IELDiscordBot.Classes.Models
{
    public class CustomCommand
    {
        public int ID { get; set; }
        public string Command { get; set; }
        public string ReturnValue { get; set; }
    }
}
