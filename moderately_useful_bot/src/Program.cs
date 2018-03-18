using SpotifyAPI.Web;
using SpotifyAPI.Web.Models;
using System;
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
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while reacting to command \"" + message.Text + "\": " + ex.ToString());
            }
        }
    }
}
