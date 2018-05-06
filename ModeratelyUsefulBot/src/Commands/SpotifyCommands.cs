using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web.Enums;
using SpotifyAPI.Web.Models;
using System.Collections.Generic;
using System.Linq;
using Telegram.Bot.Types;

namespace ModeratelyUsefulBot
{
    static class SpotifyCommands
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

        private static string _tag = "Spotify";
        private static SpotifyWebAPI _spotify;
        private static AutorizationCodeAuth _auth;
        private static Token _token;

        static SpotifyCommands()
        {
            _auth = new AutorizationCodeAuth()
            {
                ClientId = Config.GetDefault("spotify/auth/clientId", "", "credentials"),
                RedirectUri = "http://localhost",
                Scope = Scope.UserReadPrivate,
            };
            _refreshToken();

            _spotify = new SpotifyWebAPI()
            {
                AccessToken = _token.AccessToken,
                TokenType = _token.TokenType,
                UseAuth = true
            };

            Log.Info(_tag, "Spotify API setup done.");
        }

        private static void _refreshToken()
        {
            _token = _auth.RefreshToken(Config.GetDefault("spotify/auth/refreshToken", "", "credentials"), Config.GetDefault("spotify/auth/clientSecret", "", "credentials"));
            if(_spotify != null)
                _spotify.AccessToken = _token.AccessToken;
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
            if(arguments.Count() > 0 && arguments.First().ToLower() == "refresh")
            {
                _loadPlaylist(command);
                command.Bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Ok, I refreshed the playlist.");
                return;
            }

            // send placeholder, get playlist and do calculations
            var placeholderMessage = command.Bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Crunching the latest data, just for you. Hang tight...");

            if(_cachedPlaylistOutdated(command))
                _loadPlaylist(command);

            string answer;

            if (arguments.Count() == 0)
                answer = _getTrackCount(command);
            else
                switch (arguments.First().ToLower())
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
            var playlist = _spotify.GetPlaylist(
                command.Parameters["playlistUser"] as string, 
                command.Parameters["playlistId"] as string, 
                "snapshot_id");
            return playlist.SnapshotId != (command.Data["cachedPlaylist"] as CachedPlaylist)?.SnapshotId;
        }

        private static void _loadPlaylist(Command command)
        {
            var playlist = _spotify.GetPlaylist(
                command.Parameters["playlistUser"] as string, 
                command.Parameters["playlistId"] as string, 
                "snapshot_id,tracks.total,tracks.next,tracks.items(added_by.display_name,added_by.id,track.duration_ms,track.popularity)");
            var paging = playlist.Tracks;
            var tracks = paging.Items;
            var total = paging.Total;

            while (paging.HasNextPage())
            {
                paging = _spotify.GetNextPage(paging);
                tracks.AddRange(paging.Items);
            }

            command.Data["cachedPlaylist"] = new CachedPlaylist()
            {
                Tracks = tracks.OrderBy(t => t.AddedAt),
                GroupedTracks = tracks.GroupBy(track => track.AddedBy.Id),
                SnapshotId = playlist.SnapshotId
            };
        }

        private static string _getTrackCount(Command command)
        {
            var cachedPlaylist = command.Data["cachedPlaylist"] as CachedPlaylist;
            if (cachedPlaylist.Counts == null)
                _calculateBasicStats(command);

            string result = "The playlist currently contains " + cachedPlaylist.Tracks.Count() + " songs.\n\nHere's who added how many:";

            foreach(var key in cachedPlaylist.Counts.Keys)
                result += "\n" + key + ": " + cachedPlaylist.Counts[key];

            return result;
        }

        private static void _calculateBasicStats(Command command)
        {
            var cachedPlaylist = command.Data["cachedPlaylist"] as CachedPlaylist;
            cachedPlaylist.Counts = cachedPlaylist.GroupedTracks
                .OrderByDescending(group => group.Count())
                .ToDictionary(group => _spotify.GetPublicProfile(group.Key).DisplayName ?? group.Key, group => group.Count());
        }

        private static string _getDuration(Command command)
        {
            var cachedPlaylist = command.Data["cachedPlaylist"] as CachedPlaylist;
            if (cachedPlaylist.Durations == null)
                _calculateAdditionalStats(command);

            var millisPerHour = 60 * 60 * 1000;
            var millisPerMinute = 60 * 1000;
            var result = "The playlist's total duration is " + cachedPlaylist.TotalDuration / millisPerHour + " hours and " + cachedPlaylist.TotalDuration % millisPerHour / millisPerMinute + " minutes.\n\nHere's who added how much:";

            foreach (var pair in cachedPlaylist.Durations)
                result += "\n" + pair.Key + ": " + pair.Value / millisPerHour + "h" + pair.Value % millisPerHour / millisPerMinute + "m";

            return result;
        }

        private static string _getPopularity(Command command)
        {
            var cachedPlaylist = command.Data["cachedPlaylist"] as CachedPlaylist;
            if (cachedPlaylist.Popularities == null)
                _calculateAdditionalStats(command);

            var result = "The playlist's average popularity score is " + cachedPlaylist.TotalPopularity.ToString("##0.00") + ".\n\nHere's who added the most popular songs:";
            foreach (var pair in cachedPlaylist.Popularities)
                result += "\n" + pair.Key + ": " + pair.Value.ToString("##0.00");

            return result;
        }

        private static void _calculateAdditionalStats(Command command)
        {
            var cachedPlaylist = command.Data["cachedPlaylist"] as CachedPlaylist;
            cachedPlaylist.Durations = cachedPlaylist.GroupedTracks
                .ToDictionary(group => _spotify.GetPublicProfile(group.Key).DisplayName ?? group.Key, group => group.Sum(track => track.Track.DurationMs))
                .OrderByDescending(pair => pair.Value);
            cachedPlaylist.TotalDuration = cachedPlaylist.Durations.Sum(pair => pair.Value);

            cachedPlaylist.Popularities = cachedPlaylist.GroupedTracks
                .ToDictionary(group => _spotify.GetPublicProfile(group.Key).DisplayName ?? group.Key, group => group.Average(track => track.Track.Popularity))
                .OrderByDescending(pair => pair.Value);
            cachedPlaylist.TotalPopularity = cachedPlaylist.Popularities.Average(pair => pair.Value);
        }
    }
}
