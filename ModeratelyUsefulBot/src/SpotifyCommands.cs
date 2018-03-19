using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web.Enums;
using SpotifyAPI.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ModeratelyUsefulBot
{
    static class SpotifyCommands
    {
        private static SpotifyWebAPI _spotify;
        private static AutorizationCodeAuth _auth;
        private static Token _token;

        internal static void SetUpSpotify()
        {
            _auth = new AutorizationCodeAuth()
            {
                ClientId = Config.GetDefault("spotify/auth/clientId", ""),
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
            FullTrack track = _spotify.GetTrack("6lAl0AUvqBHBKMRj2Hh9LP");
            Console.WriteLine(track.Name);
        }

        private static void _refreshToken()
        {
            _token = _auth.RefreshToken(Config.GetDefault("spotify/auth/refreshToken", ""), Config.GetDefault("spotify/auth/clientSecret", ""));
            if(_spotify != null)
                _spotify.AccessToken = _token.AccessToken;
        }

        internal static void SendPlaylistStats(TelegramBotClient botClient, Message message, IEnumerable<string> arguments)
        {
            if (!Config.Get("spotify/playlist/user", out string user))
            {
                botClient.SendTextMessageAsync(message.Chat.Id, "Playlist user not specified in config.");
                return;
            }

            if (!Config.Get("spotify/playlist/id", out string id))
            {
                botClient.SendTextMessageAsync(message.Chat.Id, "Playlist id not specified in config.");
                return;
            }

            if(_token.IsExpired())
                _refreshToken();

            var placeholderMessage = botClient.SendTextMessageAsync(message.Chat.Id, "Crunching the latest data, just for you. Hang tight...");

            var paging = _spotify.GetPlaylistTracks(user, id, "total,next,items(added_by.display_name,added_by.id)");
            var tracks = paging.Items;
            var total = paging.Total;
            while (paging.HasNextPage())
            {
                paging = _spotify.GetNextPage(paging);
                tracks.AddRange(paging.Items);
            }

            var userObj = _spotify.GetPublicProfile(user);

            var groupedTracks = tracks.GroupBy(track => track.AddedBy.Id);
            var counts = groupedTracks.OrderByDescending(group => group.Count()).Select(group => (_spotify.GetPublicProfile(group.Key).DisplayName ?? group.Key) + ": " + group.Count());

            placeholderMessage.Wait();
            botClient.EditMessageTextAsync(message.Chat.Id, placeholderMessage.Result.MessageId, String.Join('\n', counts));
        }
    }
}
