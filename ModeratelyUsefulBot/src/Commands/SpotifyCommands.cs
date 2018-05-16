using System.Collections.Generic;
using System.Linq;
using ModeratelyUsefulBot.Helper;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web.Enums;
using SpotifyAPI.Web.Models;
using Telegram.Bot.Types;

// ReSharper disable UnusedMember.Global

namespace ModeratelyUsefulBot.Commands
{
    internal static class SpotifyCommands
    {
        private class CachedPlaylist
        {
            internal IOrderedEnumerable<PlaylistTrack> Tracks;
            internal IEnumerable<IGrouping<string, PlaylistTrack>> GroupedTracks;
            internal IDictionary<string, int> Counts;
            internal IOrderedEnumerable<KeyValuePair<string, int>> Durations;
            internal int TotalDuration;
            internal IOrderedEnumerable<KeyValuePair<string, double>> Popularities;
            internal double TotalPopularity;
            internal string SnapshotId;
        }

        private const string Tag = "Spotify";
        private static readonly SpotifyWebAPI Spotify;
        private static readonly AutorizationCodeAuth Auth;
        private static Token _token;

        static SpotifyCommands()
        {
            Auth = new AutorizationCodeAuth
            {
                ClientId = Config.GetDefault("spotify/auth/clientId", "", "credentials"),
                RedirectUri = "http://localhost",
                Scope = Scope.UserReadPrivate
            };
            _refreshToken();

            Spotify = new SpotifyWebAPI
            {
                AccessToken = _token.AccessToken,
                TokenType = _token.TokenType,
                UseAuth = true
            };

            Log.Info(Tag, "Spotify API setup done.");
        }

        private static void _refreshToken()
        {
            _token = Auth.RefreshToken(Config.GetDefault("spotify/auth/refreshToken", "", "credentials"), Config.GetDefault("spotify/auth/clientSecret", "", "credentials"));
            if (Spotify != null)
                Spotify.AccessToken = _token.AccessToken;
        }

        [Command(Name = "Playlist statistics", ShortDescription = "show spotify playlist stats", Description = "Shows statistics for the spotify playlist.")]
        [Argument(Name = "Statistics select", Type = typeof(string), Description = "Use \"tracks\", \"time\", \"popularity\" or \"full\" to select which statistics are calculated.", Optional = true, DefaultValue = "tracks")]
        internal static void SendPlaylistStats(this Command command, Message message, IEnumerable<string> arguments)
        {
            // check for valid settings
            if (!command.Parameters.ContainsKey("playlistUser"))
            {
                command.Bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Playlist user not specified in config.");
                return;
            }

            if (!command.Parameters.ContainsKey("playlistId"))
            {
                command.Bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Playlist id not specified in config.");
                return;
            }

            // refresh token if necessary
            if (_token.IsExpired())
                _refreshToken();

            // check for "refresh" argument
            var argList = arguments.ToList();
            if (argList.Any() && argList.First().ToLower() == "refresh")
            {
                _loadPlaylist(command);
                command.Bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Ok, I refreshed the playlist.");
                return;
            }

            // send placeholder, get playlist and do calculations
            var placeholderMessage = command.Bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Crunching the latest data, just for you. Hang tight...");

            if (_cachedPlaylistOutdated(command))
                _loadPlaylist(command);

            string answer;

            if (!argList.Any())
                answer = _getTrackCount(command);
            else
                switch (argList.First().ToLower())
                {
                    case "tracks":
                    case "songs":
                    case "count":
                        answer = _getTrackCount(command);
                        break;
                    case "duration":
                    case "time":
                        answer = _getDuration(command);
                        break;
                    case "popularity":
                    case "scores":
                        answer = _getPopularity(command);
                        break;
                    case "full":
                    case "all":
                        answer = _getTrackCount(command) + "\n\n" + _getDuration(command) + "\n\n" + _getPopularity(command);
                        break;
                    default:
                        answer = "Unknown argument. Use \"tracks\", \"time\", \"popularity\" or \"full\".";
                        break;
                }

            placeholderMessage.Wait();
            command.Bot.BotClient.EditMessageTextAsync(message.Chat.Id, placeholderMessage.Result.MessageId, answer);
        }

        private static bool _cachedPlaylistOutdated(Command command)
        {
            if (!command.Data.ContainsKey("cachedPlaylist"))
                return true;
            var playlist = Spotify.GetPlaylist(
                command.Parameters["playlistUser"] as string,
                command.Parameters["playlistId"] as string,
                "snapshot_id");
            return playlist.SnapshotId != (command.Data["cachedPlaylist"] as CachedPlaylist)?.SnapshotId;
        }

        private static void _loadPlaylist(Command command)
        {
            var playlist = Spotify.GetPlaylist(
                command.Parameters["playlistUser"] as string,
                command.Parameters["playlistId"] as string,
                "snapshot_id,tracks.total,tracks.next,tracks.items(added_by.display_name,added_by.id,track.duration_ms,track.popularity)");
            var paging = playlist.Tracks;
            var tracks = paging.Items;

            while (paging.HasNextPage())
            {
                paging = Spotify.GetNextPage(paging);
                tracks.AddRange(paging.Items);
            }

            command.Data["cachedPlaylist"] = new CachedPlaylist
            {
                Tracks = tracks.OrderBy(t => t.AddedAt),
                GroupedTracks = tracks.GroupBy(track => track.AddedBy.Id),
                SnapshotId = playlist.SnapshotId
            };
        }

        private static string _getTrackCount(Command command)
        {
            if (!(command.Data["cachedPlaylist"] is CachedPlaylist cachedPlaylist))
                return "Internal error while retrieving playlist";

            if (cachedPlaylist.Counts == null)
                _calculateBasicStats(command);

            if (cachedPlaylist.Counts == null)
                return "Internal error while processing playlist";

            var result = "The playlist currently contains " + cachedPlaylist.Tracks.Count() + " songs.\n\nHere's who added how many:";

            return cachedPlaylist.Counts.Keys.Aggregate(result, (current, key) => current + ("\n" + key + ": " + cachedPlaylist.Counts[key]));
        }

        private static void _calculateBasicStats(Command command)
        {
            if (command.Data["cachedPlaylist"] is CachedPlaylist cachedPlaylist)
                cachedPlaylist.Counts = cachedPlaylist.GroupedTracks
                    .OrderByDescending(group => group.Count())
                    .ToDictionary(group => Spotify.GetPublicProfile(group.Key).DisplayName ?? group.Key,
                        group => group.Count());
        }

        private static string _getDuration(Command command)
        {
            if (!(command.Data["cachedPlaylist"] is CachedPlaylist cachedPlaylist))
                return "Internal error while retrieving playlist";

            if (cachedPlaylist.Durations == null)
                _calculateAdditionalStats(command);

            if (cachedPlaylist.Durations == null)
                return "Internal error while processing playlist";

            const int millisPerHour = 60 * 60 * 1000;
            const int millisPerMinute = 60 * 1000;
            var result = "The playlist's total duration is " + cachedPlaylist.TotalDuration / millisPerHour + " hours and " + cachedPlaylist.TotalDuration % millisPerHour / millisPerMinute + " minutes.\n\nHere's who added how much:";

            return cachedPlaylist.Durations.Aggregate(result, (current, pair) => current + "\n" + pair.Key + ": " + pair.Value / millisPerHour + "h" + pair.Value % millisPerHour / millisPerMinute + "m");
        }

        private static string _getPopularity(Command command)
        {
            if (!(command.Data["cachedPlaylist"] is CachedPlaylist cachedPlaylist))
                return "Internal error while retrieving playlist";

            if (cachedPlaylist.Popularities == null)
                _calculateAdditionalStats(command);

            if (cachedPlaylist.Popularities == null)
                return "Internal error while processing playlist";

            var result = "The playlist's average popularity score is " + cachedPlaylist.TotalPopularity.ToString("##0.00") + ".\n\nHere's who added the most popular songs:";

            return cachedPlaylist.Popularities.Aggregate(result, (current, pair) => current + "\n" + pair.Key + ": " + pair.Value.ToString("##0.00"));
        }

        private static void _calculateAdditionalStats(Command command)
        {
            if (!(command.Data["cachedPlaylist"] is CachedPlaylist cachedPlaylist)) return;

            cachedPlaylist.Durations = cachedPlaylist.GroupedTracks
                .ToDictionary(group => Spotify.GetPublicProfile(group.Key).DisplayName ?? group.Key,
                    group => group.Sum(track => track.Track.DurationMs))
                .OrderByDescending(pair => pair.Value);
            cachedPlaylist.TotalDuration = cachedPlaylist.Durations.Sum(pair => pair.Value);

            cachedPlaylist.Popularities = cachedPlaylist.GroupedTracks
                .ToDictionary(group => Spotify.GetPublicProfile(group.Key).DisplayName ?? group.Key,
                    group => group.Average(track => track.Track.Popularity))
                .OrderByDescending(pair => pair.Value);
            cachedPlaylist.TotalPopularity = cachedPlaylist.Popularities.Average(pair => pair.Value);
        }
    }
}
