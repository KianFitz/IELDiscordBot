using IELDiscordBotPOC.Classes.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Text;

namespace IELDiscordBotPOC.Classes.Factories
{
    class IELContextFactory : IDesignTimeDbContextFactory<IELContext>
    {
        public IELContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<IELContext>();
            optionsBuilder.UseMySQL(BuildConnectionString());

            return new IELContext(optionsBuilder.Options);
        }

        private string BuildConnectionString()
        {
            return new MySqlConnectionStringBuilder()
            {
                Server = _config["database:server"],
                Password = _config["database:password"],
                Database = _config["database:db"],
                UserID = _config["database:user"],
                Port = uint.Parse(_config["database:port"])
            }
            .ConnectionString;

            return new MySqlConnectionStringBuilder()
            {
                Server = "localhost",
                Password = "ielbotdev123",
                Database = "ielbot",
                UserID = "ielbot",
                Port = 3306
            }
            .ConnectionString;
        }
    }
}
