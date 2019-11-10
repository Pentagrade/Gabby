﻿namespace Gabby.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Discord;
    using Discord.Commands;
    using Discord.WebSocket;
    using Gabby.Data;
    using Gabby.Models;
    using Microsoft.Extensions.Configuration;

    public sealed class StartupService
    {
        private readonly CommandService _commands;
        private readonly IConfigurationRoot _config;
        private readonly DiscordSocketClient _discord;
        private readonly IServiceProvider _provider;

        // DiscordSocketClient, CommandService, and IConfigurationRoot are injected automatically from the IServiceProvider
        public StartupService(
            IServiceProvider provider,
            DiscordSocketClient discord,
            CommandService commands,
            IConfigurationRoot config)
        {
            this._provider = provider;
            this._config = config;
            this._discord = discord;
            this._commands = commands;

            this._discord.Connected += this.OnConnected;
        }

        /// <exception cref="T:System.Exception">
        ///     Please enter your bot's token into the `_config.yml` file found in the
        ///     applications root directory.
        /// </exception>
        internal async Task StartAsync()
        {
            var discordToken = this._config["Tokens:Discord"]; // Get the discord token from the config file
            if (string.IsNullOrWhiteSpace(discordToken))
                throw new Exception(
                    "Please enter your bot's token into the `_config.yml` file found in the applications root directory.");

            await this._discord.LoginAsync(TokenType.Bot, discordToken).ConfigureAwait(false); // Login to discord
            await this._discord.StartAsync().ConfigureAwait(false); // Connect to the websocket
            await this._discord.SetActivityAsync(new Game("with all my friends")).ConfigureAwait(false);
            await this._commands.AddModulesAsync(Assembly.GetEntryAssembly(), this._provider)
                .ConfigureAwait(false); // Load commands and modules into the command service
        }

        private async Task OnConnected()
        {
            var recordedGuilds = await DynamoSystem.ScanItemAsync<GuildInfo>();
            var guildsToUpdate = new List<GuildInfo>();
            foreach (var rGuild in recordedGuilds)
            {
                var match = this._discord.Guilds.First(x => x.Id.ToString() == rGuild.GuildGuid);
                if (match == null)
                {
                    await DynamoSystem.DeleteItemAsync(rGuild);
                    continue;
                }

                rGuild.GuildName = match.Name;
                guildsToUpdate.Add(rGuild);
            }

            foreach (var uGuild in guildsToUpdate) await DynamoSystem.UpdateItemAsync(uGuild);
        }
    }
}