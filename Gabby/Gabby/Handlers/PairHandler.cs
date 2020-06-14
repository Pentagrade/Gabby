﻿namespace Gabby.Handlers
{
    using System;
    using System.Threading.Tasks;
    using DSharpPlus;
    using DSharpPlus.EventArgs;
    using Gabby.Services;
    using JetBrains.Annotations;

    public sealed class PairHandler
    {
        private readonly DiscordClient _discord;

        public PairHandler(
            DiscordClient discord)
        {
            this._discord = discord;

            this._discord.VoiceStateUpdated += this.OnUserVoiceStateUpdatedAsync;
        }

        private async Task OnUserVoiceStateUpdatedAsync([NotNull] VoiceStateUpdateEventArgs args)
        {
            if (args.User.Id == this._discord.CurrentUser.Id) return;

            try
            {
                await ChannelPairService.HandleChannelPair(args.User, args.Before, args.After, this._discord)
                    .ConfigureAwait(false);
            }
            catch(Exception e)
            {
                var a = 1;
            }
        }
    }
}