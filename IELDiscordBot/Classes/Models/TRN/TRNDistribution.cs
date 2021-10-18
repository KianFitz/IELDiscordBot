using System;
using System.Collections.Generic;
using System.Text;

namespace IELDiscordBot.Classes.Models.TRN
{

    public class TRNDistribution
    {
        public Data data { get; set; }
    }

    public class Data
    {
        public string[] tiers { get; set; }
        public string[] divisions { get; set; }
        public Playlist[] playlists { get; set; }
        public Division[] data { get; set; }
    }

    public class Playlist
    {
        public int key { get; set; }
        public string value { get; set; }
    }

    public class Division
    {
        public int id { get; set; }
        public int tier { get; set; }
        public int playlist { get; set; }
        public int players { get; set; }
        public int division { get; set; }
        public int minMMR { get; set; }
        public int maxMMR { get; set; }
    }

}
