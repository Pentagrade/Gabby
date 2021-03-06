﻿using Gabby.Models;
using Victoria.Interfaces;

namespace Gabby.Modules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Discord;
    using Discord.Commands;
    using Discord.WebSocket;
    using Handlers;
    using Services;
    using JetBrains.Annotations;
    using Victoria;
    using Victoria.Enums;
    using Victoria.Responses.Rest;

    [UsedImplicitly]
    public sealed class MusicModule : ModuleBase<SocketCommandContext>
    {
        private readonly LavaNode _lavaNode;
        private readonly MusicService _musicService;
        private static readonly IEnumerable<int> Range = Enumerable.Range(1900, 2000);

        public MusicModule(LavaNode lavaNode, MusicService musicService)
        {
            this._lavaNode = lavaNode;
            this._musicService = musicService;
        }

        [UsedImplicitly]
        [Command("Join")]
        [Summary("The bot will join the channel the user is connected to, you can also set the volume too")]
        public async Task JoinAsync(ushort volume = 100)
        {
            if (volume > 100 || volume < 1)
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("Sorry, I can't work out how loud you want me to be, try a number between 1 and 100", Color.Red));
                return;
            }

            if (this._lavaNode.HasPlayer(this.Context.Guild))
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("I'm already connected to a voice channel!", Color.Orange));
                return;
            }

            var voiceState = this.Context.User as IVoiceState;

            if (voiceState?.VoiceChannel == null)
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("You must be connected to a voice channel!", Color.Red));
                return;
            }

            await this._lavaNode.JoinAsync(voiceState.VoiceChannel, this.Context.Channel as ITextChannel);

            if (_musicService.MusicTrackQueues.All(x => x.GuildId != this.Context.Guild.Id))
                _musicService.MusicTrackQueues.Add(new GuildTrackQueue(this.Context.Guild.Id));

            //this._lavaNode.TryGetPlayer(this.Context.Guild, out var player);
            //await player.UpdateVolumeAsync(volume);
            await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse($"Joined {voiceState.VoiceChannel.Name}, my volume is {volume}"));
        }

        [UsedImplicitly]
        [Command("Leave")]
        [Summary("The bot will leave the channel it is currently connected to")]
        public async Task LeaveAsync()
        {
            if (!this._lavaNode.TryGetPlayer(this.Context.Guild, out var player))
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("I'm not connected to any voice channels"));
                return;
            }

            var voiceChannel = ((IVoiceState) this.Context.User).VoiceChannel ?? player.VoiceChannel;
            if (voiceChannel == null)
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("Not sure which voice channel to disconnect from."));
                return;
            }

            await this._lavaNode.LeaveAsync(voiceChannel);
            await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse($"I've left {voiceChannel.Name}"));
        }

        private async Task<SearchResponse> ValidationCheck([CanBeNull] string query, SearchType? type = null)
        {
            SearchResponse searchResponse;

            if (string.IsNullOrWhiteSpace(query)) await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("Please provide search terms."));

            if (!this._lavaNode.HasPlayer(this.Context.Guild))
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("I'm not connected to a voice channel."));


            searchResponse = type switch
            {
                SearchType.Youtube => await this._lavaNode.SearchYouTubeAsync(query),
                SearchType.Soundcloud => await this._lavaNode.SearchSoundCloudAsync(query),
                null => await this._lavaNode.SearchAsync(query),
            };

            if (searchResponse.LoadStatus == LoadStatus.LoadFailed ||
                searchResponse.LoadStatus == LoadStatus.NoMatches)
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse($"I wasn't able to find anything for `{query}`."));

            return searchResponse;
        }

        private async Task EnqueueTracks(SearchResponse searchResponse, LavaPlayer player)
        {
            if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name))
            {
                foreach (var track in searchResponse.Tracks)
                {
                    player.Queue.Enqueue(track);
                    _musicService.MusicTrackQueues.Single(x => x.GuildId == this.Context.Guild.Id).QueuedItems
                        .Add(new QueuedItem(track, this.Context.User));
                }

                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse($"Enqueued {searchResponse.Tracks.Count} tracks."));
            }
            else
            {
                var track = searchResponse.Tracks[0];
                player.Queue.Enqueue(track);
                _musicService.MusicTrackQueues.Single(x => x.GuildId == this.Context.Guild.Id).QueuedItems
                    .Add(new QueuedItem(track, this.Context.User));
                await this.ReplyAsync("", false, (await ProduceNowPlayingEmbed(track, this.Context.User)).Build());
            }
        }

        private async Task EnqueueSingleTrack(SearchResponse searchResponse, LavaPlayer player)
        {
            var track = searchResponse.Tracks[0];

            if (!string.IsNullOrWhiteSpace(searchResponse.Playlist.Name))
            {
                for (var i = 0; i < searchResponse.Tracks.Count; i++)
                {
                    if (i == 0)
                    {
                        await player.PlayAsync(track);
                        _musicService.MusicTrackQueues.Single(x => x.GuildId == this.Context.Guild.Id).QueuedItems
                            .Add(new QueuedItem(track, this.Context.User));
                        await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse($"Now Playing: {track.Title}"));
                    }
                    else
                    {
                        player.Queue.Enqueue(searchResponse.Tracks[i]);
                        _musicService.MusicTrackQueues.Single(x => x.GuildId == this.Context.Guild.Id).QueuedItems
                            .Add(new QueuedItem(searchResponse.Tracks[i], this.Context.User));
                    }
                }

                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse($"Enqueued {searchResponse.Tracks.Count} tracks."));
            }
            else
            {
                await player.PlayAsync(track);
                _musicService.MusicTrackQueues.Single(x => x.GuildId == this.Context.Guild.Id).QueuedItems
                    .Add(new QueuedItem(track, this.Context.User));
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse($"Now Playing: {track.Title}"));
            }
        }

        [UsedImplicitly]
        [Command("Play")]
        [Summary("The bot will play/queue the provided music link")]
        public async Task PlayAsync([Remainder] [CanBeNull] string query)
        {
            var searchResponse = await this.ValidationCheck(query);

            var player = this._lavaNode.GetPlayer(this.Context.Guild);

            if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
            {
                await this.EnqueueTracks(searchResponse, player);
            }
            else
            {
                await this.EnqueueSingleTrack(searchResponse, player);
            }
        }

        public enum SearchType
        {
            Youtube,
            Soundcloud
        }

        [UsedImplicitly]
        [Command("Search")]
        [Summary("The bot will look for a YouTube or Soundcloud song to play matching your search")]
        public async Task SearchAsync(SearchType type, [Remainder] [CanBeNull] string query)
        {
            var searchResponse = await this.ValidationCheck(query, type);

            var player = this._lavaNode.GetPlayer(this.Context.Guild);

            if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused)
            {
                await this.EnqueueTracks(searchResponse, player);
            }
            else
            {
                await this.EnqueueSingleTrack(searchResponse, player);
            }
        }

        [UsedImplicitly]
        [Command("Pause")]
        [Summary("The bot will pause the currently playing track")]
        public async Task PauseAsync()
        {
            if (!this._lavaNode.TryGetPlayer(this.Context.Guild, out var player))
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("I'm not connected to a voice channel."));
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("I cannot pause when I'm not playing anything"));
                return;
            }

            await player.PauseAsync();
            await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse($"Paused: {player.Track.Title}"));
        }

        [UsedImplicitly]
        [Command("Resume")]
        [Summary("The bot will resume the currently playing track")]
        public async Task ResumeAsync()
        {
            if (!this._lavaNode.TryGetPlayer(this.Context.Guild, out var player))
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("I'm not connected to a voice channel."));
                return;
            }

            if (player.PlayerState != PlayerState.Paused)
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("I cannot resume when I'm not playing anything"));
                return;
            }

            await player.ResumeAsync();
            await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse($"Resumed: {player.Track.Title}"));
        }

        [UsedImplicitly]
        [Command("Stop")]
        [Summary("The bot will stop playing the current track and clear the queue")]
        public async Task StopAsync()
        {
            if (!this._lavaNode.TryGetPlayer(this.Context.Guild, out var player))
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("I'm not connected to a voice channel."));
                return;
            }

            if (player.PlayerState == PlayerState.Stopped)
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("Woaaah there, I can't stop the stopped forced."));
                return;
            }


            await player.StopAsync();
            await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("No longer playing anything."));
        }

        [UsedImplicitly]
        [Command("Skip")]
        [Summary("The bot will skip the currently playing track")]
        public async Task SkipAsync()
        {
            if (!this._lavaNode.TryGetPlayer(this.Context.Guild, out var player))
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("I'm not connected to a voice channel."));
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("Woaaah there, I can't skip when nothing is playing."));
                return;
            }

            var voiceChannelUsers = (player.VoiceChannel as SocketVoiceChannel).Users.Where(x => !x.IsBot).ToArray();
            if (this._musicService.VoteQueue.Contains(this.Context.User.Id))
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("You can't vote again."));
                return;
            }

            this._musicService.VoteQueue.Add(this.Context.User.Id);
            var percentage = this._musicService.VoteQueue.Count / voiceChannelUsers.Length * 100;
            if (percentage < 85)
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("You need more than 85% votes to skip this song."));
                return;
            }

            try
            {
                var oldTrack = player.Track;
                var currentTrack = await player.SkipAsync();

                _musicService.MusicTrackQueues.Single(x => x.GuildId == this.Context.Guild.Id).QueuedItems
                    .RemoveAll(x => x.Track.Id == oldTrack.Id);

                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse($"Skipped: {oldTrack.Title}\nNow Playing: {currentTrack.Title}"));
            }
            catch (Exception exception)
            {
                await this.ReplyAsync(exception.Message);
            }
        }

        [UsedImplicitly]
        [Command("remove")]
        public async Task DeleteFromQueue(int selection1, int selection2 = 0)
        {
            if (!this._lavaNode.TryGetPlayer(this.Context.Guild, out var player))
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("I'm not connected to a voice channel."));
                return;
            }
            if (selection2 == 0)
            {
                var track = (LavaTrack) player.Queue.ElementAt(selection1 - 1);

                _musicService.MusicTrackQueues.Single(x => x.GuildId == this.Context.Guild.Id).QueuedItems
                    .RemoveAll(x => x.Track.Id == track.Id);

                player.Queue.RemoveAt(selection1 - 1);

                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse($"Removed song {selection1} from the queue", Color.Green));
            }
            else
            {
                player.Queue.RemoveRange(selection1 - 1, selection2 - 2);
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse($"Removed songs {selection1} to {selection2} from the queue", Color.Green));
            }
        }

        [UsedImplicitly]
        [Command("Seek")]
        [Summary("The bot will seek to the chosen time of the currently playing track")]
        public async Task SeekAsync([Remainder] string time)
        {
            if (!this._lavaNode.TryGetPlayer(this.Context.Guild, out var player))
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("I'm not connected to a voice channel."));
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("Woaaah there, I can't seek when nothing is playing."));
                return;
            }


            var timeSpan = TimeSpan.Parse(time);
            await player.SeekAsync(timeSpan);
            await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse($"I've seeked `{player.Track.Title}` to {timeSpan}."));
        }

        [UsedImplicitly]
        [Command("Volume")]
        [Alias("Vol")]
        [Summary("The bot will adjust the volume to the selected amount")]
        public async Task VolumeAsync(ushort volume)
        {
            if (!this._lavaNode.TryGetPlayer(this.Context.Guild, out var player))
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("I'm not connected to a voice channel."));
                return;
            }

            try
            {
                await player.UpdateVolumeAsync(volume);
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse($"I've changed the player volume to {volume}."));
            }
            catch (Exception exception)
            {
                await this.ReplyAsync(exception.Message);
            }
        }

        [UsedImplicitly]
        [Command("NowPlaying")]
        [Alias("Np")]
        [Summary("The bot will display whats playing")]
        public async Task NowPlayingAsync()
        {
            if (!this._lavaNode.TryGetPlayer(this.Context.Guild, out var player))
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("I'm not connected to a voice channel."));
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("Woaaah there, I'm not playing any tracks."));
                return;
            }

            var track = player.Track;

            var requestingUser = _musicService.MusicTrackQueues.Single(x => x.GuildId == this.Context.Guild.Id)
                .QueuedItems.First().RequestingUser;

            var embed = await ProduceNowPlayingEmbed(track, requestingUser);

            await this.ReplyAsync(embed: embed.Build());
        }

        private async Task<EmbedBuilder> ProduceNowPlayingEmbed(LavaTrack track, SocketUser requestingUser)
        {
            return new EmbedBuilder
                {
                    Author = new EmbedAuthorBuilder
                    {
                        Name = "Now Playing!"
                    },
                    Title = track.Title,
                    Description = track.Author,
                    ThumbnailUrl = await track.FetchArtworkAsync(),
                    Url = track.Url,
                    Footer = new EmbedFooterBuilder
                    {
                        Text = $"This track was requested by {requestingUser.Username}#{requestingUser.Discriminator}",
                        IconUrl = requestingUser.GetAvatarUrl()
                    }
                }
                .AddField("Duration", $@"{track.Duration:mm\:ss}")
                .AddField("Position", $@"{track.Position:mm\:ss}");
        }

        [UsedImplicitly]
        [Command("Genius", RunMode = RunMode.Async)]
        [Summary("The bot will show lyrics for the current song from Genius")]
        public async Task ShowGeniusLyrics()
        {
            if (!this._lavaNode.TryGetPlayer(this.Context.Guild, out var player))
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("I'm not connected to a voice channel."));
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("Woaaah there, I'm not playing any tracks."));
                return;
            }

            var lyrics = await player.Track.FetchLyricsFromGeniusAsync();
            if (string.IsNullOrWhiteSpace(lyrics))
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse($"No lyrics found for {player.Track.Title}"));
                return;
            }

            var splitLyrics = lyrics.Split('\n');
            var stringBuilder = new StringBuilder();
            foreach (var line in splitLyrics)
            {
                if (Range.Contains(stringBuilder.Length))
                {
                    await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse($"```{stringBuilder}```"));
                    stringBuilder.Clear();
                }
                else
                {
                    stringBuilder.AppendLine(line);
                }
            }

            await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse($"```{stringBuilder}```"));
        }

        [UsedImplicitly]
        [Command("OVH", RunMode = RunMode.Async)]
        [Summary("The bot will show lyrics for the current song from OVH")]
        public async Task ShowOVHLyrics()
        {
            if (!this._lavaNode.TryGetPlayer(this.Context.Guild, out var player))
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("I'm not connected to a voice channel."));
                return;
            }

            if (player.PlayerState != PlayerState.Playing)
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("Woaaah there, I'm not playing any tracks."));
                return;
            }

            var lyrics = await player.Track.FetchLyricsFromOVHAsync();
            if (string.IsNullOrWhiteSpace(lyrics))
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse($"No lyrics found for {player.Track.Title}"));
                return;
            }

            var splitLyrics = lyrics.Split('\n');
            var stringBuilder = new StringBuilder();
            foreach (var line in splitLyrics)
                if (Range.Contains(stringBuilder.Length))
                {
                    await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse($"```{stringBuilder}```"));
                    stringBuilder.Clear();
                }
                else
                {
                    stringBuilder.AppendLine(line);
                }

            await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse($"```{stringBuilder}```"));
        }

        [UsedImplicitly]
        [Command("Queue")]
        [Summary("Displays the next 10 songs in the queue")]
        public async Task ExportQueue()
        {
            if (!this._lavaNode.TryGetPlayer(this.Context.Guild, out var player))
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("I'm not connected to a voice channel."));
                return;
            }

            if (player.Queue.Count == 0)
            {
                await this.ReplyAsync("", false, EmbedHandler.GenerateEmbedResponse("There is nothing queued to play right now."));
                return;
            }

            var embed = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    Name = "Queued Tracks (next 10)"
                },
                Footer = new EmbedFooterBuilder
                {
                    Text =
                        $"There is a total of {_musicService.MusicTrackQueues.Single(x => x.GuildId == this.Context.Guild.Id).QueuedItems.Count} songs in the queue",
                    IconUrl = this.Context.Client.CurrentUser.GetAvatarUrl()
                },
                Color = Color.Green
            };

            foreach (var queuedItem in _musicService.MusicTrackQueues.Single(x => x.GuildId == this.Context.Guild.Id).QueuedItems)
            {
                embed.Fields.Add(new EmbedFieldBuilder
                {
                    IsInline = false,
                    Name = $"[{queuedItem.Track.Title} - {queuedItem.Track.Author}]{queuedItem.Track.Url}",
                    Value =
                        $"Requested by {queuedItem.RequestingUser.Username}#{queuedItem.RequestingUser.Discriminator}"
                });

                if (embed.Fields.Count == 10) break;
            }

            await this.ReplyAsync("", false, embed.Build());
        }
    }
}