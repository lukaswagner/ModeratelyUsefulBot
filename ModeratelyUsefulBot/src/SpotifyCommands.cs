using SpotifyAPI.Web;
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

        internal static void SetUpSpotify()
        {
            _spotify = new SpotifyWebAPI()
            {
                AccessToken = Config.GetDefault("spotify/token", ""),
                TokenType = "Bearer",
                UseAuth = true
            };
            FullTrack track = _spotify.GetTrack("6lAl0AUvqBHBKMRj2Hh9LP");
            Console.WriteLine(track.Name);
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
