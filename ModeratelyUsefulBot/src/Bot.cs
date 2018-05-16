using System;
using System.Collections.Generic;
using System.Linq;
using ModeratelyUsefulBot.Commands;
using ModeratelyUsefulBot.Helper;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;

namespace ModeratelyUsefulBot
{
    internal class Bot
    {
        private const string Tag = "Bot";
        internal TelegramBotClient BotClient;
        internal Dictionary<string, Command> Commands;
        private readonly List<TimedCommand> _timedCommands;
        private readonly Command _fallbackCommand;
        internal List<int> Admins;
        internal string Name;
        internal string TagWithName;

        internal Bot(string token, IEnumerable<Command> commands, List<TimedCommand> timedCommands, List<int> admins, string name = "", string fallbackMessage = "Sorry, but I don't know how to do that.")
        {
            BotClient = new TelegramBotClient(token);

            Commands = new Dictionary<string, Command>();
            foreach (var command in commands)
                foreach (var commandName in command.Names)
                {
                    command.Bot = this;
                    Commands.Add(commandName, command);
                }

            _fallbackCommand = new Command(null, ((Action<Command, Message, IEnumerable<string>>) MiscCommands.SendText).Method, this)
            {
                Parameters = {["text"] = fallbackMessage}
            };

            _timedCommands = timedCommands;
            _timedCommands.ForEach(tc => tc.Bot = this);

            Admins = admins;

            Name = name;
            TagWithName = name == "" ? Tag : Tag + " (" + name + ")";
            if (TagWithName.Length > Log.TagLength)
                TagWithName = TagWithName.Substring(0, Log.TagLength);

            BotClient.OnUpdate += _onUpdate;

            _check();
        }

        internal static Bot CreateBot(int index)
        {
            var path = "bots/bot[" + index + "]";

            bool CheckArg(bool success, string message)
            {
                if (!success)
                    Log.Error(Tag, "Could not create bot with index " + index + ". " + message);
                return success;
            }

            if (!CheckArg(Config.DoesPropertyExist(path), "No settings found in config.")) return null;
            if (!CheckArg(Config.Get(path + "/token", out string token, "credentials"), "No token found in config.")) return null;
            var hasCustomFallbackMessage = Config.DoesPropertyExist(path + "/fallbackMessage");
            var fallbackMessage = "";
            if (hasCustomFallbackMessage) Config.Get(path + "/fallbackMessage", out fallbackMessage);
            var name = Config.GetDefault(path + "/name", "");

            var admins = new List<int>();
            if (Config.DoesPropertyExist(path + "/admins"))
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
                if (command != null)
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

            var bot = hasCustomFallbackMessage ?
                new Bot(token, commands, timedCommands, admins, name, fallbackMessage) :
                new Bot(token, commands, timedCommands, admins, name);

            if (Config.GetDefault(path + "/autostart", true))
                bot.BotClient.StartReceiving();

            return bot;
        }

        private async void _check()
        {
            var me = await BotClient.GetMeAsync();
            Log.Info(TagWithName, "Hello! My name is " + me.FirstName + ". I am currently " + (BotClient.IsReceiving ? "enabled." : "disabled."));
        }

        private void _onUpdate(object sender, UpdateEventArgs e)
        {
            var type = e.Update.Type;
            if (type != Telegram.Bot.Types.Enums.UpdateType.MessageUpdate)
                return;
            var message = e.Update.Message;
            Log.Info(TagWithName, "Received message from " + _getSenderInfo(message) + ": " + message.Text);
            if (message.Text.StartsWith('/'))
                _reactToCommand(message);
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
                if (containsUsername > -1)
                    name = name.Substring(0, containsUsername);
                var arguments = split.Skip(1);

                if (!Commands.TryGetValue(name, out var command))
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
                Log.Error(TagWithName, "Error while reacting to command \"" + message.Text + "\":\n" + ex);
                BotClient.SendTextMessageAsync(message.Chat.Id, "OOPSIE WOOPSIE!! Uwu We made a fucky wucky!! A wittle fucko boingo! The code monkeys at our headquarters are working VEWY HAWD to fix this!");
            }
        }

        internal void RunTimedCommands()
        {
            _timedCommands.ForEach(tc => tc.RunIfTimeUp());
        }
    }
}
