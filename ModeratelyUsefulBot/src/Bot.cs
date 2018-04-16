using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;

namespace ModeratelyUsefulBot
{
    class Bot
    {
        private static string _tag = "Bot";
        internal TelegramBotClient BotClient;
        internal Dictionary<string, Command> Commands;
        private List<TimedCommand> _timedCommands;
        private Command _fallbackCommand;
        public List<int> Admins;
        internal string Name;

        internal Bot(string token, List<Command> commands, List<TimedCommand> timedCommands, List<int> admins, string name = "", string fallbackMessage = "Sorry, but I don't know how to do that.")
        {
            BotClient = new TelegramBotClient(token);
            
            Commands = commands.ToDictionary(cmd => cmd.Name, cmd => { cmd.Bot = this; return cmd; });
            _fallbackCommand = new Command("", (c, m, a) => c.BotClient.SendTextMessageAsync(m.Chat.Id, fallbackMessage), this);

            _timedCommands = timedCommands;
            _timedCommands.ForEach(tc => tc.Bot = this);

            Admins = admins;

            Name = name == "" ? _tag : (_tag + " (" + name + ")");
            if (Name.Length > Log.TagLength)
                Name = Name.Substring(0, Log.TagLength);

            BotClient.OnUpdate += _onUpdate;
            BotClient.StartReceiving();

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
            if(!checkArg(Config.Get(path + "/token", out string token, "credentials"), "No token found in config.")) return null;
            var hasCustomFallbackMessage = Config.DoesPropertyExist(path + "/fallbackMessage");
            string fallbackMessage = "";
            if (hasCustomFallbackMessage) Config.Get(path + "/fallbackMessage", out fallbackMessage);
            string logName = Config.GetDefault(path + "/logName", "");

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
                return new Bot(token, commands, timedCommands, admins, logName, fallbackMessage);
            else
                return new Bot(token, commands, timedCommands, admins, logName);
        }

        internal void StopReceiving() => BotClient?.StopReceiving();

        private async void _check()
        {
            var me = await BotClient.GetMeAsync();
            Log.Info(Name, "Hello! My name is " + me.FirstName + ".");
        }

        private void _onUpdate(object sender, UpdateEventArgs e)
        {
            var type = e.Update.Type;
            if (type == Telegram.Bot.Types.Enums.UpdateType.MessageUpdate)
            {
                var message = e.Update.Message;
                Log.Info(Name, "Received message from " + _getSenderInfo(message) + ": " + message.Text);
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

                if (!Commands.TryGetValue(name, out Command command))
                    command = _fallbackCommand;

                if (command.AdminOnly && !Admins.Contains(message.From.Id))
                {
                    BotClient.SendTextMessageAsync(message.Chat.Id, "Don't tell me what to do!");
                    return;
                }

                command.Invoke(message, arguments);
            }
            catch (Exception ex)
            {
                Log.Error(Name, "Error while reacting to command \"" + message.Text + "\":\n" + ex.ToString());
                BotClient.SendTextMessageAsync(message.Chat.Id, "OOPSIE WOOPSIE!! Uwu We made a fucky wucky!! A wittle fucko boingo! The code monkeys at our headquarters are working VEWY HAWD to fix this!");
            }
        }

        internal void RunTimedCommands()
        {
            _timedCommands.ForEach(tc => tc.RunIfTimeUp());
        }
    }
}
