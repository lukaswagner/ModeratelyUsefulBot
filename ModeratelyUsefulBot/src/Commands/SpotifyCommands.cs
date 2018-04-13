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
            public IOrderedEnumerable<PlaylistTrack> Tracks;
            public IEnumerable<IGrouping<string, PlaylistTrack>> GroupedTracks;
            public IDictionary<string, int> Counts;
            public IOrderedEnumerable<KeyValuePair<string, int>> Durations;
            public int TotalDuration;
            public IOrderedEnumerable<KeyValuePair<string, double>> Popularities;
            public double TotalPopularity;
            public string SnapshotId;
        }

        private static string _tag = "Spotify";
        private static SpotifyWebAPI _spotify;
        private static AutorizationCodeAuth _auth;
        private static Token _token;
        private static CachedPlaylist _cachedPlaylist;
        private static string _userId;
        private static string _playlistId;

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

            if (Config.Get("spotify/playlist/user", out string user))
                _userId = user;
            else
                Log.Error(_tag, "Playlist user not specified in config.");

            if (Config.Get("spotify/playlist/id", out string playlist))
                _playlistId = playlist;
            else
                Log.Error(_tag, "Playlist id not specified in config.");

            if (_userId != null && _playlistId != null)
                _loadPlaylist();

            Log.Info(_tag, "Spotify API setup done.");
        }

        private static void _refreshToken()
        {
            _token = _auth.RefreshToken(Config.GetDefault("spotify/auth/refreshToken", "", "credentials"), Config.GetDefault("spotify/auth/clientSecret", "", "credentials"));
            if(_spotify != null)
                _spotify.AccessToken = _token.AccessToken;
        }

        [Command(Name = "Playlist statistics", ShortDescription = "show spotify playlist stats", Description = "Shows statistics for the spotify playlist.")]
        [Argument(Name = "Statistics select", Type = typeof(string), Description = "Use \"full\" to retrieve additional statistics.", Optional = true)]
        internal static void SendPlaylistStats(Bot bot, Message message, IEnumerable<string> arguments)
        {
            // check for valid settings
            if (_userId == null)
            {
                bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Playlist user not specified in config.");
                return;
            }

            if (_playlistId == null)
            {
                bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Playlist id not specified in config.");
                return;
            }

            // refresh token if necessary
            if (_token.IsExpired())
                _refreshToken();

            // check for "refresh" argument
            if(arguments.Count() > 0 && arguments.First().ToLower() == "refresh")
            {
                _loadPlaylist();
                bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Ok, I refreshed the playlist.");
                return;
            }

            // send placeholder, get playlist and do calculations
            var placeholderMessage = bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Crunching the latest data, just for you. Hang tight...");

            if(_cachedPlaylistOutdated())
                _loadPlaylist();

            var answer = (arguments.Count() > 0 && arguments.First().ToLower() == "full") ? _getFullStats() : _getBasicStats();

            placeholderMessage.Wait();
            bot.BotClient.EditMessageTextAsync(message.Chat.Id, placeholderMessage.Result.MessageId, answer);
        }

        private static bool _cachedPlaylistOutdated()
        {
            var playlist = _spotify.GetPlaylist(_userId, _playlistId, "snapshot_id");
            return playlist.SnapshotId != _cachedPlaylist.SnapshotId;
        }

        private static void _loadPlaylist()
        {
            var playlist = _spotify.GetPlaylist(_userId, _playlistId, "snapshot_id,tracks.total,tracks.next,tracks.items(added_by.display_name,added_by.id,track.duration_ms,track.popularity)");
            var paging = playlist.Tracks;
            var tracks = paging.Items;
            var total = paging.Total;

            while (paging.HasNextPage())
            {
                paging = _spotify.GetNextPage(paging);
                tracks.AddRange(paging.Items);
            }

            _cachedPlaylist = new CachedPlaylist()
            {
                Tracks = tracks.OrderBy(t => t.AddedAt),
                GroupedTracks = tracks.GroupBy(track => track.AddedBy.Id),
                SnapshotId = playlist.SnapshotId
            };
        }

        private static string _getBasicStats()
        {
            if (_cachedPlaylist.Counts == null)
                _calculateBasicStats();

            string result = "The playlist currently contains " + _cachedPlaylist.Tracks.Count() + " songs.\n\nHere's who added how many:";

            foreach(var key in _cachedPlaylist.Counts.Keys)
                result += "\n" + key + ": " + _cachedPlaylist.Counts[key];

            return result;
        }

        private static void _calculateBasicStats()
        {
            _cachedPlaylist.Counts = _cachedPlaylist.GroupedTracks
                .OrderByDescending(group => group.Count())
                .ToDictionary(group => _spotify.GetPublicProfile(group.Key).DisplayName ?? group.Key, group => group.Count());
        }

        private static string _getFullStats()
        {
            string result = _getBasicStats();

            if (_cachedPlaylist.Durations == null)
                _calculateAdditionalStats();

            var millisPerHour = 60 * 60 * 1000;
            var millisPerMinute = 60 * 1000;
            result += "\n\nThe playlist's total duration is " + _cachedPlaylist.TotalDuration / millisPerHour + " hours and " + _cachedPlaylist.TotalDuration % millisPerHour / millisPerMinute + " minutes.\n\nHere's who added how much:";
            foreach (var pair in _cachedPlaylist.Durations)
                result += "\n" + pair.Key + ": " + pair.Value / millisPerHour + "h" + pair.Value % millisPerHour / millisPerMinute + "m";

            result += "\n\nThe playlist's average popularity score is " + _cachedPlaylist.TotalPopularity.ToString("##0.00") + ".\n\nHere's who added the most popular songs:";
            foreach (var pair in _cachedPlaylist.Popularities)
                result += "\n" + pair.Key + ": " + pair.Value.ToString("##0.00");

            return result;
        }

        private static void _calculateAdditionalStats()
        {
            _cachedPlaylist.Durations = _cachedPlaylist.GroupedTracks
                .ToDictionary(group => _spotify.GetPublicProfile(group.Key).DisplayName ?? group.Key, group => group.Sum(track => track.Track.DurationMs))
                .OrderByDescending(pair => pair.Value);
            _cachedPlaylist.TotalDuration = _cachedPlaylist.Durations.Sum(pair => pair.Value);

            _cachedPlaylist.Popularities = _cachedPlaylist.GroupedTracks
                .ToDictionary(group => _spotify.GetPublicProfile(group.Key).DisplayName ?? group.Key, group => group.Average(track => track.Track.Popularity))
                .OrderByDescending(pair => pair.Value);
            _cachedPlaylist.TotalPopularity = _cachedPlaylist.Popularities.Average(pair => pair.Value);
        }
    }
}
