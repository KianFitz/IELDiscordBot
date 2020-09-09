using System;

namespace IELDiscordBot.Classes.Models.DSN
{

    public class TRNMMRObject
    {
        public Data data { get; set; }
    }

    public class Data
    {
        public Unranked[] Unranked { get; set; }
        public Duel[] Duel { get; set; }
        public Duo[] Duos { get; set; }
        public Solostandard[] SoloStandard { get; set; }
        public Standard[] Standard { get; set; }
        public Hoop[] Hoops { get; set; }
        public Rumble[] Rumble { get; set; }
        public Dropshot[] Dropshot { get; set; }
        public Snowday[] Snowday { get; set; }
    }

    public class Unranked
    {
        public int rating { get; set; }
        public string tier { get; set; }
        public string division { get; set; }
        public int tierId { get; set; }
        public int divisionId { get; set; }
        public DateTime collectDate { get; set; }
    }

    public class Duel
    {
        public int rating { get; set; }
        public string tier { get; set; }
        public string division { get; set; }
        public int tierId { get; set; }
        public int divisionId { get; set; }
        public DateTime collectDate { get; set; }
    }

    public class Duo
    {
        public int rating { get; set; }
        public string tier { get; set; }
        public string division { get; set; }
        public int tierId { get; set; }
        public int divisionId { get; set; }
        public DateTime collectDate { get; set; }
    }

    public class Solostandard
    {
        public int rating { get; set; }
        public string tier { get; set; }
        public string division { get; set; }
        public int tierId { get; set; }
        public int divisionId { get; set; }
        public DateTime collectDate { get; set; }
    }

    public class Standard
    {
        public int rating { get; set; }
        public string tier { get; set; }
        public string division { get; set; }
        public int tierId { get; set; }
        public int divisionId { get; set; }
        public DateTime collectDate { get; set; }
    }

    public class Hoop
    {
        public int rating { get; set; }
        public string tier { get; set; }
        public string division { get; set; }
        public int tierId { get; set; }
        public int divisionId { get; set; }
        public DateTime collectDate { get; set; }
    }

    public class Rumble
    {
        public int rating { get; set; }
        public string tier { get; set; }
        public string division { get; set; }
        public int tierId { get; set; }
        public int divisionId { get; set; }
        public DateTime collectDate { get; set; }
    }

    public class Dropshot
    {
        public int rating { get; set; }
        public string tier { get; set; }
        public string division { get; set; }
        public int tierId { get; set; }
        public int divisionId { get; set; }
        public DateTime collectDate { get; set; }
    }

    public class Snowday
    {
        public int rating { get; set; }
        public string tier { get; set; }
        public string division { get; set; }
        public int tierId { get; set; }
        public int divisionId { get; set; }
        public DateTime collectDate { get; set; }
    }

}
