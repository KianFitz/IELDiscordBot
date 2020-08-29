using System;
using System.Collections.Generic;
using System.Text;

namespace IELDiscordBot.Classes.Models
{
    public class ManualPeakOverride
    {
        public string Platform { get; set; }
        public string User { get; set; }
        public int Season { get; set; }
        public int Peak { get; set; }
    }
}
