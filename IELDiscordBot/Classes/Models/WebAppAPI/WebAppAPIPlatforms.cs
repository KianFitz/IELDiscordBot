namespace IELDiscordBot.Classes.Models.WebAppAPI
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class PlatformsAPIResponse
    {
        public Platform[] Platforms { get; set; }
    }

    public class Platform
    {
#nullable enable
        public string? id { get; set; }
        public object? activeUntil { get; set; }
#nullable disable
        public int internal_id { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public bool active { get; set; }
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
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

