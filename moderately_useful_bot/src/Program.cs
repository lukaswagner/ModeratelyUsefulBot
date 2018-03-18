using SpotifyAPI.Web;
using SpotifyAPI.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;

namespace moderately_useful_bot
{
    class Program
    {
        private static TelegramBotClient _botClient;
        private static SpotifyWebAPI _spotify;

        private static void Main(string[] args)
        {
            _setUpSpotify();
            _startBot().Wait();
            Console.WriteLine("Type \"exit\" to stop the bot.");
            var running = true;
            while(running)
                if(Console.ReadLine() == "exit")
                {
                    _botClient?.StopReceiving();
                    running = false;
                }
        }

        private static async Task _startBot()
        {
            var token = Config.GetDefault("telegram/token", "");
            Console.WriteLine("Token: " + token);
            _botClient = new TelegramBotClient(token);
            _botClient.OnUpdate += _onUpdate;
            _botClient.StartReceiving();
            var me = await _botClient.GetMeAsync();
            Console.WriteLine("Hello! My name is " + me.FirstName);
        }

        private static void _setUpSpotify()
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

        private static void _onUpdate(object sender, UpdateEventArgs e)
        {
            var type = e.Update.Type;
            if(type == Telegram.Bot.Types.Enums.UpdateType.MessageUpdate)
            {
                var message = e.Update.Message;
                Console.WriteLine("Received Message: " + message.Text);
                if (message.Text.StartsWith('/'))
                    _reactToCommand(message);
            }
        }

        private static void _reactToCommand(Message message)
        {
            try
            {
                var split = message.Text.Split(' ');
                var command = split[0];
                var arguments = split.Skip(1);

                switch (command)
                {
                    case "/ping":
                        _botClient.SendTextMessageAsync(message.Chat.Id, arguments.Count() > 0 ? String.Join(' ', arguments) : "pong");
                        break;
                    case "/playlist":
                        _sendPlaylistStats(message, arguments);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while reacting to command \"" + message.Text + "\": " + ex.ToString());
            }
        }

        private static void _sendPlaylistStats(Message message, IEnumerable<string> arguments)
        {
            if(!Config.Get("spotify/playlist/user", out string user))
            {
                _botClient.SendTextMessageAsync(message.Chat.Id, "Playlist user not specified in config.");
                return;
            }

            if (!Config.Get("spotify/playlist/id", out string id))
            {
                _botClient.SendTextMessageAsync(message.Chat.Id, "Playlist id not specified in config.");
                return;
            }

            var placeholderMessage = _botClient.SendTextMessageAsync(message.Chat.Id, "Crunching the latest data, just for you. Hang tight...");

            var paging = _spotify.GetPlaylistTracks(user, id, "total,next,items(added_by.display_name,added_by.id)");
            var tracks = paging.Items;
            var total = paging.Total;
            while(paging.HasNextPage())
            {
                paging = _spotify.GetNextPage(paging);
                tracks.AddRange(paging.Items);
            }

            var userObj = _spotify.GetPublicProfile(user);

            var groupedTracks = tracks.GroupBy(track => track.AddedBy.Id);
            var counts = groupedTracks.OrderByDescending(group => group.Count()).Select(group => (_spotify.GetPublicProfile(group.Key).DisplayName ?? group.Key) + ": " + group.Count());

            placeholderMessage.Wait();
            _botClient.EditMessageTextAsync(message.Chat.Id, placeholderMessage.Result.MessageId, String.Join('\n', counts));
        }
    }
}
