﻿namespace Gabby
{
    using System;
    using System.Threading.Tasks;
    using Amazon.DynamoDBv2;
    using Discord;
    using Discord.Commands;
    using Discord.WebSocket;
    using Gabby.Handlers;
    using Gabby.Services;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Victoria;
    using MusicService = Gabby.Services.MusicService;

    public sealed class Startup
    {
        internal static IConfigurationRoot StaticConfiguration;

        // ReSharper disable once UnusedParameter.Local
        public Startup(string[] args)
        {
            var builder = new ConfigurationBuilder() // Create a new instance of the config builder
                .SetBasePath(AppContext.BaseDirectory) // Specify the default location for the config file
                .AddYamlFile("_config.yml"); // Add this (yaml encoded) file to the configuration
            this.Configuration = builder.Build(); // Build the configuration
            StaticConfiguration = this.Configuration;
        }

        private IConfigurationRoot Configuration { get; }

        internal static async Task RunAsync(string[] args)
        {
            var startup = new Startup(args);
            await startup.RunAsync();
        }

        private async Task RunAsync()
        {
            var services = new ServiceCollection(); // Create a new instance of a service collection
            this.ConfigureServices(services);

            var provider = services.BuildServiceProvider(); // Build the service provider
            provider.GetRequiredService<LoggingService>(); // Start the logging service
            provider.GetRequiredService<CommandHandler>(); // Start the command handler service
            provider.GetRequiredService<PairHandler>();
            provider.GetRequiredService<GuildHandler>();

            provider.GetRequiredService<MusicService>();

            await provider.GetRequiredService<StartupService>().StartAsync(); // Start the startup service
            await Task.Delay(-1); // Keep the program alive
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // var victoriaConfig = new Configuration
            // {
            //     Password = StaticConfiguration["LavaLink:Password"],
            //     LogSeverity = LogSeverity.Verbose,
            //     AutoDisconnect = true
            // };

            services.AddAWSService<IAmazonDynamoDB>();
            services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
                {
                    // Add discord to the collection
                    LogLevel = LogSeverity.Verbose, // Tell the logger to give Verbose amount of info
                    MessageCacheSize = 1000 // Cache 1,000 messages per channel
                }))
                .AddSingleton(new CommandService(new CommandServiceConfig
                {
                    // Add the command service to the collection
                    LogLevel = LogSeverity.Verbose, // Tell the logger to give Verbose amount of info
                    DefaultRunMode = RunMode.Async // Force all commands to run async by default
                }))
                .AddSingleton<CommandHandler>() // Add the command handler to the collection
                .AddSingleton<PairHandler>()
                .AddSingleton<GuildHandler>()
                .AddSingleton<StartupService>() // Add startup service to the collection
                .AddSingleton<LoggingService>() // Add logging service to the collection
                // .AddSingleton(new LavaRestClient(victoriaConfig))
                // .AddSingleton<LavaSocketClient>()
                .AddSingleton(new LavaConfig
                {
                    Hostname = StaticConfiguration["LavaLink:Host"],
                    Port = Convert.ToUInt16(StaticConfiguration["LavaLink:Port"]),
                    Authorization = StaticConfiguration["LavaLink:Password"],
                    SelfDeaf = false
                })
                .AddSingleton<LavaNode>()
                .AddSingleton<MusicService>()
                .AddSingleton<Random>() // Add random to the collection
                .AddSingleton(this.Configuration); // Add the configuration to the collection
        }
    }
}