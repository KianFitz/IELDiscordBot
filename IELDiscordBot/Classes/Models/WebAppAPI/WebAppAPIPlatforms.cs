using System;
using System.Collections.Generic;
using System.Text;

namespace IELDiscordBot.Classes.Models.WebAppAPI
{

    public class PlatformsAPIResponse
    {
        public Platform[] Platforms { get; set; }
    }

    public class Platform
    {
        public int internal_id { get; set; }
        public string? id { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public bool active { get; set; }
        public object? activeUntil { get; set; }
    }


    public class Rootobject
    {
        public Class1[] Property1 { get; set; }
    }

    public class Class1
    {
        public int internal_id { get; set; }
        public string id { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public bool active { get; set; }
        public object activeUntil { get; set; }
    }

}
