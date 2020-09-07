using System;
using System.Collections.Generic;
using System.Text;

namespace IELDiscordBot.Classes.Models
{
    public class TRNObject
    {
        public Data data { get; set; }
    }

    public class Data
    {
        public Platforminfo platformInfo { get; set; }
        public Userinfo userInfo { get; set; }
        public Metadata metadata { get; set; }
        public Segment[] segments { get; set; }
        public Availablesegment[] availableSegments { get; set; }
        public DateTime expiryDate { get; set; }
    }

    public class Platforminfo
    {
        public string platformSlug { get; set; }
        public object platformUserId { get; set; }
        public string platformUserHandle { get; set; }
        public string platformUserIdentifier { get; set; }
        public string avatarUrl { get; set; }
        public object additionalParameters { get; set; }
    }

    public class Userinfo
    {
        public int? userId { get; set; }
        public bool isPremium { get; set; }
        public bool isVerified { get; set; }
        public bool isInfluencer { get; set; }
        public object countryCode { get; set; }
        public object customAvatarUrl { get; set; }
        public object customHeroUrl { get; set; }
        public object[] socialAccounts { get; set; }
    }

    public class Metadata
    {
        public Lastupdated lastUpdated { get; set; }
        public int playerId { get; set; }
    }

    public class Lastupdated
    {
        public DateTime? value { get; set; }
        public DateTime? displayValue { get; set; }
    }

    public class Segment
    {
        public string type { get; set; }
        public Attributes attributes { get; set; }
        public Metadata1 metadata { get; set; }
        public DateTime expiryDate { get; set; }
        public Stats stats { get; set; }
    }

    public class Attributes
    {
        public int playlistId { get; set; }
        public int season { get; set; }
    }

    public class Metadata1
    {
        public string name { get; set; }
    }

    public class Stats
    {
        public Wins wins { get; set; }
        public Goals goals { get; set; }
        public Mvps mVPs { get; set; }
        public Saves saves { get; set; }
        public Assists assists { get; set; }
        public Shots shots { get; set; }
        public Goalshotratio goalShotRatio { get; set; }
        public Score score { get; set; }
        public Seasonrewardlevel seasonRewardLevel { get; set; }
        public Seasonrewardwins seasonRewardWins { get; set; }
        public Tier tier { get; set; }
        public Division division { get; set; }
        public Matchesplayed matchesPlayed { get; set; }
        public Winstreak winStreak { get; set; }
        public Rating rating { get; set; }
    }

    public class Wins
    {
        public object? rank { get; set; }
        public float? percentile { get; set; }
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
    }

    public class Goals
    {
        public object? rank { get; set; }
        public object? percentile { get; set; }
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

    public class Mvps
    {
        public object? rank { get; set; }
        public object? percentile { get; set; }
        public string displayName { get; set; }
        public string displayCategory { get; set; }
        public string category { get; set; }
        public Metadata4 metadata { get; set; }
        public int value { get; set; }
        public string displayValue { get; set; }
        public string displayType { get; set; }
    }

    public class Metadata4
    {
    }

    public class Saves
    {
        public object? rank { get; set; }
        public object? percentile { get; set; }
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

    public class Assists
    {
        public object? rank { get; set; }
        public object? percentile { get; set; }
        public string displayName { get; set; }
        public string displayCategory { get; set; }
        public string category { get; set; }
        public Metadata6 metadata { get; set; }
        public int value { get; set; }
        public string displayValue { get; set; }
        public string displayType { get; set; }
    }

    public class Metadata6
    {
    }

    public class Shots
    {
        public object? rank { get; set; }
        public object? percentile { get; set; }
        public string displayName { get; set; }
        public string displayCategory { get; set; }
        public string category { get; set; }
        public Metadata7 metadata { get; set; }
        public int value { get; set; }
        public string displayValue { get; set; }
        public string displayType { get; set; }
    }

    public class Metadata7
    {
    }

    public class Goalshotratio
    {
        public object? rank { get; set; }
        public object? percentile { get; set; }
        public string displayName { get; set; }
        public string displayCategory { get; set; }
        public string category { get; set; }
        public Metadata8 metadata { get; set; }
        public float value { get; set; }
        public string displayValue { get; set; }
        public string displayType { get; set; }
    }

    public class Metadata8
    {
    }

    public class Score
    {
        public object? rank { get; set; }
        public float? percentile { get; set; }
        public string displayName { get; set; }
        public string displayCategory { get; set; }
        public string category { get; set; }
        public Metadata9 metadata { get; set; }
        public float value { get; set; }
        public string displayValue { get; set; }
        public string displayType { get; set; }
    }

    public class Metadata9
    {
    }

    public class Seasonrewardlevel
    {
        public object? rank { get; set; }
        public float? percentile { get; set; }
        public string displayName { get; set; }
        public string displayCategory { get; set; }
        public string category { get; set; }
        public Metadata10 metadata { get; set; }
        public int value { get; set; }
        public string displayValue { get; set; }
        public string displayType { get; set; }
    }

    public class Metadata10
    {
        public string iconUrl { get; set; }
        public string rankName { get; set; }
    }

    public class Seasonrewardwins
    {
        public object? rank { get; set; }
        public object? percentile { get; set; }
        public string displayName { get; set; }
        public string displayCategory { get; set; }
        public string category { get; set; }
        public Metadata11 metadata { get; set; }
        public int value { get; set; }
        public string displayValue { get; set; }
        public string displayType { get; set; }
    }

    public class Metadata11
    {
    }

    public class Tier
    {
        public object? rank { get; set; }
        public object? percentile { get; set; }
        public string displayName { get; set; }
        public string displayCategory { get; set; }
        public string category { get; set; }
        public Metadata12 metadata { get; set; }
        public int value { get; set; }
        public string displayValue { get; set; }
        public string displayType { get; set; }
    }

    public class Metadata12
    {
        public string iconUrl { get; set; }
        public string name { get; set; }
    }

    public class Division
    {
        public object? rank { get; set; }
        public object? percentile { get; set; }
        public string displayName { get; set; }
        public string displayCategory { get; set; }
        public string category { get; set; }
        public Metadata13 metadata { get; set; }
        public int value { get; set; }
        public string displayValue { get; set; }
        public string displayType { get; set; }
    }

    public class Metadata13
    {
        public string name { get; set; }
        public int deltaDown { get; set; }
        public int deltaUp { get; set; }
    }

    public class Matchesplayed
    {
        public object? rank { get; set; }
        public object? percentile { get; set; }
        public string displayName { get; set; }
        public string displayCategory { get; set; }
        public string category { get; set; }
        public Metadata14 metadata { get; set; }
        public int value { get; set; }
        public string displayValue { get; set; }
        public string displayType { get; set; }
    }

    public class Metadata14
    {
    }

    public class Winstreak
    {
        public object? rank { get; set; }
        public object? percentile { get; set; }
        public string displayName { get; set; }
        public string displayCategory { get; set; }
        public string category { get; set; }
        public Metadata15 metadata { get; set; }
        public int value { get; set; }
        public string displayValue { get; set; }
        public string displayType { get; set; }
    }

    public class Metadata15
    {
        public string type { get; set; }
    }

    public class Rating
    {
        public int? rank { get; set; }
        public float? percentile { get; set; }
        public string displayName { get; set; }
        public string displayCategory { get; set; }
        public string category { get; set; }
        public Metadata16 metadata { get; set; }
        public int value { get; set; }
        public string displayValue { get; set; }
        public string displayType { get; set; }
    }

    public class Metadata16
    {
    }

    public class Availablesegment
    {
        public string type { get; set; }
        public Attributes1 attributes { get; set; }
        public Metadata17 metadata { get; set; }
    }

    public class Attributes1
    {
        public int season { get; set; }
    }

    public class Metadata17
    {
        public string name { get; set; }
    }

}


