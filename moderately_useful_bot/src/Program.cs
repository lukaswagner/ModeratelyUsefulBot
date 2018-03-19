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
        private static Dictionary<string, Command> _commands;
        private static Command _fallbackCommand;

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
            var token = Config.GetDefault("telegram/token", "");
            _botClient = new TelegramBotClient(token);

            var commands = new List<Command>()
            {
                new Command(_botClient, "/ping", MiscCommands.Ping, true),
                new Command(_botClient, "/playlist", SpotifyCommands.SendPlaylistStats)
            };
            _commands = commands.ToDictionary(cmd => cmd.Name, cmd => cmd);
            _fallbackCommand = new Command(_botClient, "", _unknownCommand);

            _botClient.OnUpdate += _onUpdate;
            _botClient.StartReceiving();

            var me = await _botClient.GetMeAsync();
            Console.WriteLine("Hello! My name is " + me.FirstName);
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
                var name = split[0];
                var arguments = split.Skip(1);

                if (!_commands.TryGetValue(name, out Command command))
                    command = _fallbackCommand;

                command.Invoke(message, arguments);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while reacting to command \"" + message.Text + "\":\n" + ex.ToString());
            }
        }

        private static void _unknownCommand(TelegramBotClient botClient, Message message, IEnumerable<string> arguments)
        {
            botClient.SendTextMessageAsync(message.Chat.Id, "Sorry, but I don't know how to do that.");
        }
    }
}
