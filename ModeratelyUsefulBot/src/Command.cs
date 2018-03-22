using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ModeratelyUsefulBot
{
    class Command
    {
        private Action<TelegramBotClient, Message, IEnumerable<string>> _action;

        public string Name { private set; get; }
        public TelegramBotClient BotClient;
        public bool AdminOnly;

        public Command(string name, Action<TelegramBotClient, Message, IEnumerable<string>> action, TelegramBotClient botClient = null, bool adminOnly = false)
        {
            BotClient = botClient;
            Name = name;
            _action = action;
            AdminOnly = adminOnly;
        }

        internal static Command CreateCommand(string botPath, int index)
        {
            var path = botPath + "/commands/command[" + index + "]";

            bool checkArg(bool success, string message)
            {
                if(!success)
                    Console.WriteLine("Could not create command with botPath " + botPath + " and index " + index + ". " + message);
                return success;
            }

            if (!checkArg(Config.DoesPropertyExist(path), "No settings found in config.")) return null;
            if (!checkArg(Config.Get(path + "/name", out string name), "No name found in config.")) return null;
            if (!checkArg(Config.Get(path + "/action", out string actionString), "No action found in config.")) return null;
            var adminOnly = Config.GetDefault(path + "/adminOnly", false);

            var splitAction = actionString.Split('.');
            if(!checkArg(splitAction.Length == 2, "Action should contain class and method name divided by a period.")) return null;
            var actionClass = Type.GetType("ModeratelyUsefulBot." + splitAction[0]);
            if (!checkArg(actionClass != null, "Could not find class " + splitAction[0] + ".")) return null;
            RuntimeHelpers.RunClassConstructor(actionClass.TypeHandle);
            var actionMethod = actionClass.GetMethod(splitAction[1], BindingFlags.Static | BindingFlags.NonPublic);
            if (!checkArg(actionMethod != null, "Could not find method " + splitAction[1] + ".")) return null;
            var action = (Action<TelegramBotClient, Message, IEnumerable<string>>)Delegate.CreateDelegate(typeof(Action<TelegramBotClient, Message, IEnumerable<string>>), actionMethod);

            return new Command("/" + name, action, adminOnly: adminOnly);
        }

        public void Invoke(Message message, IEnumerable<string> arguments)
        {
            if (BotClient == null)
                return;

            _action.Invoke(BotClient, message, arguments);
        }
    }
}
