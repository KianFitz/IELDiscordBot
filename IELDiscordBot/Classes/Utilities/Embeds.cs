using Discord;
using IELDiscordBot.Classes.Models;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Renci.SshNet.Messages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using static IELDiscordBot.Classes.Modules.DSNModule;

namespace IELDiscordBotPOC.Classes.Utilities
{
    class Embeds
    {
        internal static Embed NewFreeAgent()
        {
            EmbedBuilder builder = new EmbedBuilder()
            { 
            };

            return builder.Build();
        }

        internal static Embed WelcomeToIEL()
        {
            EmbedBuilder builder = new EmbedBuilder()
            { 
            };

            return builder.Build();
        }

        internal static Embed DSNCalculation(List<DSNCalculationData> data, string user, string platform)
        {
            int mmr1 = 0;
            int mmr2 = 0;
            double dsn = 0;
            string gp = "";
            string mmr = "";
            foreach (var d in data)
            {
                gp += $"Season {d.Season}: `{d.GamesPlayed}`\r\n";
                mmr += $"Season {d.Season}: `{d.MaxMMR}`\r\n";

                if (d.MaxMMR > mmr1)
                {
                    if (mmr1 > mmr2)
                        mmr2 = mmr1;

                    mmr1 = d.MaxMMR;
                }
                else if (d.MaxMMR > mmr2)
                    mmr2 = d.MaxMMR;
            }

            if (mmr2 > 0)
                dsn = ((mmr1 * 0.75) + (mmr2 * 0.25));
            else
                dsn = (mmr1 * 0.75);

            // Dia 1 threshold
            if (mmr1 >= 935)
            {
                if (dsn < 1100)
                    dsn = 1100;
            }
            else
                dsn = 0;

            string finalString = $"ID: `{user}`\r\nPlatform: `{platform}`\r\n";
            finalString += "\r\n**Games Played:**\r\n";
            finalString += gp;
            finalString += "\r\n**MMRs:**\r\n";
            finalString += mmr;
            finalString += $"\r\n**DSN:** `{(dsn > 0 ? dsn.ToString() : "Illegal. Player has not reached Diamond 1!")}`";


            EmbedBuilder builder = new EmbedBuilder()
            {
                Color = Constants.SuccessColor,
                Description = finalString
            };
            return builder.Build();
        }
    }
}
