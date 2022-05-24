using System;

namespace IELDiscordBot.Classes.Models.DSN.Segments
{
    public class TRNSegment
    {
        public Datum[] data { get; set; }
    }

    public class Datum
    {
        public string type { get; set; }
        public Attributes attributes { get; set; }
        public Metadata metadata { get; set; }
        public DateTime expiryDate { get; set; }
        public Stats stats { get; set; }
    }

    public class Attributes
    {
        public int playlistId { get; set; }
        public int season { get; set; }
    }

    public class Metadata
    {
        public string name { get; set; }
    }

    public class Stats
    {
        public Tier tier { get; set; }
        public Division division { get; set; }
        public Matchesplayed matchesPlayed { get; set; }
        public Winstreak winStreak { get; set; }
        public Rating rating { get; set; }
    }

    public class Tier
    {
        public object rank { get; set; }
        public object percentile { get; set; }
        public string displayName { get; set; }
        public string displayCategory { get; set; }
        public string category { get; set; }
        public Metadata1 metadata { get; set; }
        public int value { get; set; }
        public string displayValue { get; set; }
        public string displayType { get; set; }
    }

    public class Metadata1
    {
        public string iconUrl { get; set; }
        public string name { get; set; }
    }

    public class Division
    {
        public object rank { get; set; }
        public object percentile { get; set; }
        public string displayName { get; set; }
        public string displayCategory { get; set; }
        public string category { get; set; }
        public Metadata2 metadata { get; set; }
        public int value { get; set; }
        public string displayValue { get; set; }
        public string displayType { get; set; }
    }

    public class Metadata2
    {
        public string name { get; set; }
    }

    public class Matchesplayed
    {
        public object rank { get; set; }
        public object percentile { get; set; }
        public string displayName { get; set; }
        public string displayCategory { get; set; }
        public string category { get; set; }
        public Metadata3 metadata { get; set; }
        public int value { get; set; }
        public string displayValue { get; set; }
        public string displayType { get; set; }
    }

    public class Metadata3
    {
    }

    public class Winstreak
    {
        public object rank { get; set; }
        public object percentile { get; set; }
        public string displayName { get; set; }
        public string displayCategory { get; set; }
        public string category { get; set; }
        public Metadata4 metadata { get; set; }
        public int? value { get; set; }
        public string displayValue { get; set; }
        public string displayType { get; set; }
    }

    public class Metadata4
    {
    }

    public class Rating
    {
        public object rank { get; set; }
        public object percentile { get; set; }
        public string displayName { get; set; }
        public string displayCategory { get; set; }
        public string category { get; set; }
        public Metadata5 metadata { get; set; }
        public int value { get; set; }
        public string displayValue { get; set; }
        public string displayType { get; set; }
    }

    public class Metadata5
    {
    }

}
