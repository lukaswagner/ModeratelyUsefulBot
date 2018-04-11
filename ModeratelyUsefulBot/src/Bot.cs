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
        private static string _tag = "Bot";
        private TelegramBotClient _botClient;
        private Dictionary<string, Command> _commands;
        private List<TimedCommand> _timedCommands;
        private Command _fallbackCommand;
        private List<int> _admins;

        internal Bot(string token, List<Command> commands, List<TimedCommand> timedCommands, List<int> admins, string fallbackMessage = "Sorry, but I don't know how to do that.")
        {
            _botClient = new TelegramBotClient(token);
            
            _commands = commands.ToDictionary(cmd => cmd.Name, cmd => { cmd.BotClient = _botClient; return cmd; });
            _fallbackCommand = new Command("", (c, m, a) => c.SendTextMessageAsync(m.Chat.Id, fallbackMessage), _botClient);

            _timedCommands = timedCommands;
            _timedCommands.ForEach(tc => tc.BotClient = _botClient);

            _admins = admins;

            _botClient.OnUpdate += _onUpdate;
            _botClient.StartReceiving();

            _check();
        }

        internal static Bot CreateBot(int index)
        {
            var path = "bots/bot[" + index + "]";

            bool checkArg(bool success, string message)
            {
                if (!success)
                    Log.Error(_tag, "Could not create bot with index " + index + ". " + message);
                return success;
            }

            if (!checkArg(Config.DoesPropertyExist(path), "No settings found in config.")) return null;
            if(!checkArg(Config.Get(path + "/token", out string token), "No token found in config.")) return null;
            var hasCustomFallbackMessage = Config.DoesPropertyExist(path + "/fallbackMessage");
            string fallbackMessage = "";
            if (hasCustomFallbackMessage) Config.Get(path + "/fallbackMessage", out fallbackMessage);

            var admins = new List<int>();
            if(Config.DoesPropertyExist(path + "/admins"))
            {
                var adminIndex = 1;
                while (Config.DoesPropertyExist(path + "/admins/admin[" + adminIndex + "]"))
                    admins.Add(Config.GetDefault(path + "/admins/admin[" + adminIndex++ + "]", 0));
            }

            var commands = new List<Command>();
            var commandIndex = 1;
            while (Config.DoesPropertyExist(path + "/commands/command[" + commandIndex + "]"))
            {
                var command = Command.CreateCommand(path, commandIndex++);
                if(command != null)
                    commands.Add(command);
            }

            var timedCommands = new List<TimedCommand>();
            var timedCommandIndex = 1;
            while (Config.DoesPropertyExist(path + "/timedCommands/timedCommand[" + timedCommandIndex + "]"))
            {
                var timedCommand = TimedCommand.CreateTimedCommand(path, timedCommandIndex++);
                if (timedCommand != null)
                    timedCommands.Add(timedCommand);
            }

            if (hasCustomFallbackMessage)
                return new Bot(token, commands, timedCommands, admins, fallbackMessage);
            else
                return new Bot(token, commands, timedCommands, admins);
        }

        internal void StopReceiving() => _botClient?.StopReceiving();

        private async void _check()
        {
            var me = await _botClient.GetMeAsync();
            Log.Info(_tag, "Hello! My name is " + me.FirstName + ".");
        }

        private void _onUpdate(object sender, UpdateEventArgs e)
        {
            var type = e.Update.Type;
            if (type == Telegram.Bot.Types.Enums.UpdateType.MessageUpdate)
            {
                var message = e.Update.Message;
                Log.Info(_tag, "Received message from " + _getSenderInfo(message) + ": " + message.Text);
                if (message.Text.StartsWith('/'))
                    _reactToCommand(message);
            }
        }

        private static string _getSenderInfo(Message message)
        {
            var result = message.From.Id + " (" + message.From.FirstName + " " + message.From.LastName + ")";
            if (message.Chat.Type == Telegram.Bot.Types.Enums.ChatType.Private)
                result += " in private chat";
            else
                result += " in group " + message.Chat.Id + " (" + message.Chat.Title + ")";
            return result;
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

                if (command.AdminOnly && !_admins.Contains(message.From.Id))
                {
                    _botClient.SendTextMessageAsync(message.Chat.Id, "Don't tell me what to do!");
                    return;
                }

                command.Invoke(message, arguments);
            }
            catch (Exception ex)
            {
                Log.Error(_tag, "Error while reacting to command \"" + message.Text + "\":\n" + ex.ToString());
                _botClient.SendTextMessageAsync(message.Chat.Id, "OOPSIE WOOPSIE!! Uwu We made a fucky wucky!! A wittle fucko boingo! The code monkeys at our headquarters are working VEWY HAWD to fix this!");
            }
        }

        internal void RunTimedCommands()
        {
            _timedCommands.ForEach(tc => tc.RunIfTimeUp());
        }
    }
}
