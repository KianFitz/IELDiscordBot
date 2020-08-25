using Discord.Commands;
using IELDiscordBot.Classes.Models;
using IELDiscordBotPOC.Classes.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Text;

namespace IELDiscordBotPOC.Classes.Database
{
    public class IELContext : DbContext
    {
        internal DbSet<Team> Teams { get; set; }
        internal DbSet<DBConfigSettings> ConfigSettings { get; set; }
        internal DbSet<CustomCommand> CustomCommands { get; set; }

        public IELContext(DbContextOptions<IELContext> options) : base(options) { }
        //public IELContext() : base ()
        //{

        //}

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Team>().HasKey("ID");
            modelBuilder.Entity<Team>().Property("ID").IsRequired();
            modelBuilder.Entity<Team>().Property("Name").HasMaxLength(100).IsRequired();
            modelBuilder.Entity<Team>().Property("Abbreviation").HasMaxLength(10).IsRequired();
            modelBuilder.Entity<Team>().Property("Role").IsRequired();
            modelBuilder.Entity<Team>().Property("CaptainID").IsRequired(false);

            modelBuilder.Entity<DBConfigSettings>().HasKey("Subsection", "Key");
            modelBuilder.Entity<DBConfigSettings>().Property("Subsection").HasMaxLength(100).IsRequired();
            modelBuilder.Entity<DBConfigSettings>().Property("Key").HasMaxLength(50).IsRequired();
            modelBuilder.Entity<DBConfigSettings>().Property("Value").HasMaxLength(50).IsRequired();

            modelBuilder.Entity<CustomCommand>().HasKey("ID");
            modelBuilder.Entity<CustomCommand>().Property("ID").IsRequired();
            modelBuilder.Entity<CustomCommand>().Property("Command").HasMaxLength(30).IsRequired();
            modelBuilder.Entity<CustomCommand>().Property("ReturnValue").HasMaxLength(5000).IsRequired();
        }
    }
}
