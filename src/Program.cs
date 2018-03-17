using System;
using System.IO;
using System.Threading.Tasks;
using Telegram.Bot;

namespace moderately_useful_bot
{
    class Program
    {
        private static TelegramBotClient _botClient;

        private static void Main(string[] args)
        {
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
            using (StreamReader sr = new StreamReader("data/token.txt"))
            {
                var token = sr.ReadLine();
                Console.WriteLine("Token: " + token);
                _botClient = new TelegramBotClient(token);
                _botClient.OnUpdate += _onUpdate;
                _botClient.StartReceiving();
                var me = await _botClient.GetMeAsync();
                Console.WriteLine("Hello! My name is " + me.FirstName);
            }
        }

        private static void _onUpdate(object sender, Telegram.Bot.Args.UpdateEventArgs e)
        {
            var type = e.Update.Type;
            if(type == Telegram.Bot.Types.Enums.UpdateType.MessageUpdate)
            {
                var message = e.Update.Message;
                Console.WriteLine("Received Message: " + message.Text);
                _botClient.SendTextMessageAsync(message.Chat.Id, "You said \"" + message.Text + "\"");
            }
        }
    }
}
