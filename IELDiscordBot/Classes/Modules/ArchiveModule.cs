﻿using Discord;
using Discord.Commands;
using System;
using System.Threading.Tasks;
using DiscordChatExporter.Core.Exporting;
using DiscordChatExporter.Core.Discord;
using DiscordChatExporter.Core.Discord.Data;
using DiscordChatExporter.Core.Exporting.Partitioning;
using DiscordChatExporter.Core.Exporting.Filtering;
using IELDiscordBot.Classes.Utilities;
using System.Globalization;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NLog;
using System.IO;
using System.Collections.Generic;
using System.Web;

namespace IELDiscordBot.Classes.Modules
{
    [Group("archive")]
    public class ArchiveModule : ModuleBase<SocketCommandContext>
    {
        const string HARDCODED_ARCHIVE_PATH = "/home/nellag/bot/IELDiscordBot/WebAppAPI/archive";

        private readonly DiscordSocketClient _client;
        private readonly IConfigurationRoot _config;
        private readonly ChannelExporter _exporter;
        private readonly Logger _log = LogManager.GetCurrentClassLogger();

        public ArchiveModule(DiscordSocketClient client, IConfigurationRoot config)
        {
            _client = client;
            _config = config;
            _exporter = new ChannelExporter(new AuthToken(AuthTokenKind.Bot, _config["tokens:live"].ToString()));
        }

        [Command("create")]
        public async Task HandleArchiveCommand(ITextChannel channel, [Remainder] string category)
        {
            if (string.IsNullOrEmpty(category)) return;

            category = new CultureInfo("en-GB", false).TextInfo.ToTitleCase(category);
            string env = HARDCODED_ARCHIVE_PATH;

            Guild g = new Guild(new Snowflake(Context.Guild.Id), Context.Guild.Name, Context.Guild.IconUrl);
            var discordCategory = channel.CategoryId.HasValue ? await channel.GetCategoryAsync() : null;
            ChannelCategory cc = new ChannelCategory(new Snowflake(channel.CategoryId.Value), discordCategory.Name, discordCategory.Position);
            Channel c = new Channel(new Snowflake(channel.Id), ChannelKind.GuildTextChat, g.Id, cc, channel.Name, channel.Position, channel.Topic);

            ExportRequest req = new ExportRequest(g, c, env + $"/{category}/Light/{channel.Name}.html", ExportFormat.HtmlLight,
                null, null, PartitionLimit.Null, MessageFilter.Null, true, true, "yyyy-MM-dd hh:mm:ss");

            ExportRequest req2 = new ExportRequest(g, c, env + $"/{category}/Dark/{channel.Name}.html", ExportFormat.HtmlDark,
                null, null, PartitionLimit.Null, MessageFilter.Null, true, true, "yyyy-MM-dd hh:mm:ss");


            var message = await Context.Channel.SendMessageAsync("", false, Embeds.Archiving(Context.User, channel, "Starting")).ConfigureAwait(false);
            Progress<int> progress = new Progress<int>();
            progress.ProgressChanged += OnUpdate;

            try
            {
                await message.ModifyAsync(x => x.Embed = Embeds.Archiving(Context.User, channel, "Exporting Light Mode")).ConfigureAwait(false);
                await _exporter.ExportChannelAsync(req).ConfigureAwait(false);
                await message.ModifyAsync(x => x.Embed = Embeds.Archiving(Context.User, channel, "Exporting Dark Mode")).ConfigureAwait(false);
                await _exporter.ExportChannelAsync(req2).ConfigureAwait(false);
                await message.ModifyAsync(x => x.Embed = Embeds.Archiving(Context.User, channel, "Done"));
            }
            catch (Exception ex)
            {
                await message.ModifyAsync(x =>
                {
                    x.Content = $"Error occured: {ex}";
                    x.Embed = null;
                });
            }

            async void OnUpdate(object sender, int e)
            {
                await message.ModifyAsync(x => x.Content = $"Progress: {e}%").ConfigureAwait(false);
            }
        }

        public struct Info
        {
            public string Path;
            public string Name;
            public string Category;
            public string GetUrl(bool darkMode) { return HttpUtility.UrlEncode($"http://{botUrl}/api/archive?fileName={Name}&category={Category}&darkMode={darkMode}"); }
        }

        const string botUrl = "webapp.imperialesportsleague.co.uk:2102";

        [Command("web")]
        public async Task HandleArchiveWebCommand() => await Context.Channel.SendMessageAsync($"URL to Archive Browser is: http://{botUrl}/archive/");

        [Command("search")]
        public async Task HandleArchiveSearchCommand(string name = "")
        {
            string env = HARDCODED_ARCHIVE_PATH;
            
            DirectoryInfo di = new DirectoryInfo(env);
            var fileInfos = di.GetFiles($"*{name}*.html", SearchOption.AllDirectories);

            List<Info> infos = new List<Info>();

            foreach (var fileInfo in fileInfos)
            {
                if (fileInfo.DirectoryName.Contains("Dark")) continue;

                var fileName = fileInfo.Name;
                var diName = fileInfo.Directory.FullName;
                var directory = diName.Substring(diName.IndexOf("archive"));

                Info info = new Info();
                info.Category = GetCategory(diName);
                info.Name = fileName;
                info.Path = directory;

                infos.Add(info);

                //TODO: Please find a better way to extract from a path.
                string GetCategory(string diName)
                {
                    diName = diName.Substring(diName.IndexOf("archive/") + 8);
                    diName = diName.Substring(0, diName.IndexOf("/"));
                    return diName;
                }
            }

            await Context.Channel.SendMessageAsync("", false, Embeds.ArchiveSearch(infos));
        }
    }
}
