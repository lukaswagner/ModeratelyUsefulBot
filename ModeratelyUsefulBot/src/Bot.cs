using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;

namespace ModeratelyUsefulBot
{
    class Bot
    {
        private TelegramBotClient _botClient;
        private Dictionary<string, Command> _commands;
        private Command _fallbackCommand;

        internal Bot(string token, List<Command> commands, string fallbackMessage = "Sorry, but I don't know how to do that.")
        {
            _botClient = new TelegramBotClient(token);
            
            _commands = commands.ToDictionary(cmd => cmd.Name, cmd => { cmd.BotClient = _botClient; return cmd; });
            _fallbackCommand = new Command("", (c, m, a) => c.SendTextMessageAsync(m.Chat.Id, fallbackMessage), _botClient);

            _botClient.OnUpdate += _onUpdate;
            _botClient.StartReceiving();

            _check();
        }

        internal void StopReceiving() => _botClient?.StopReceiving();

        private async void _check()
        {
            var me = await _botClient.GetMeAsync();
            Console.WriteLine("Hello! My name is " + me.FirstName);
        }

        private void _onUpdate(object sender, UpdateEventArgs e)
        {
            var type = e.Update.Type;
            if (type == Telegram.Bot.Types.Enums.UpdateType.MessageUpdate)
            {
                var message = e.Update.Message;
                Console.WriteLine("Received Message: " + message.Text);
                if (message.Text.StartsWith('/'))
                    _reactToCommand(message);
            }
        }

        private void _reactToCommand(Message message)
        {
            try
            {
                var split = message.Text.Split(' ');
                var name = split[0].ToLower();
                var containsUsername = name.IndexOf('@');
                if(containsUsername > -1)
                    name = name.Substring(0, containsUsername);
                var arguments = split.Skip(1);

                if (!_commands.TryGetValue(name, out Command command))
                    command = _fallbackCommand;

                command.Invoke(message, arguments);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while reacting to command \"" + message.Text + "\":\n" + ex.ToString());
                _botClient.SendTextMessageAsync(message.Chat.Id, "OOPSIE WOOPSIE!! Uwu We made a fucky wucky!! A wittle fucko boingo! The code monkeys at our headquarters are working VEWY HAWD to fix this!");
            }
        }
    }
}
