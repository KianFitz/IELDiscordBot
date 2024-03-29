﻿using IELDiscordBot.Classes.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MySql.Data.MySqlClient;

namespace IELDiscordBot.Classes.Factories
{
    internal class IELContextFactory : IDesignTimeDbContextFactory<IELContext>
    {
        public IELContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<IELContext>();
            optionsBuilder.UseMySQL(BuildConnectionString());

            return new IELContext(optionsBuilder.Options);
        }

        private string BuildConnectionString()
        {
            //return new MySqlConnectionStringBuilder()
            //{
            //    Server = _config["database:server"],
            //    Password = _config["database:password"],
            //    Database = _config["database:db"],
            //    UserID = _config["database:user"],
            //    Port = uint.Parse(_config["database:port"])
            //}
            //.ConnectionString;

            return new MySqlConnectionStringBuilder()
            {
                Server = "localhost",
                Password = "ielbotdev123",
#if DEBUG
                Database = "ielbot",
#endif
#if RELEASE
                Database = "ielbot_live",
#endif
                UserID = "ielbot",
                Port = 3306
            }
            .ConnectionString;
        }
    }
}
