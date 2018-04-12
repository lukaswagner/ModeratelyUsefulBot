using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Telegram.Bot.Types;

namespace ModeratelyUsefulBot
{
    class Command
    {
        private Action<Bot, Message, IEnumerable<string>> _action;

        private static string _tag = "Command";
        public string Name { private set; get; }
        public Bot Bot;
        public bool AdminOnly;

        public Command(string name, Action<Bot, Message, IEnumerable<string>> action, Bot bot = null, bool adminOnly = false)
        {
            Bot = bot;
            Name = name;
            _action = action;
            AdminOnly = adminOnly;
        }

        internal static Command CreateCommand(string botPath, int index)
        {
            var path = botPath + "/commands/command[" + index + "]";

            bool checkArg(bool success, string message)
            {
                if (!success)
                    Log.Error(_tag, "Could not create command with botPath " + botPath + " and index " + index + ". " + message);
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
            var action = (Action<Bot, Message, IEnumerable<string>>)Delegate.CreateDelegate(typeof(Action<Bot, Message, IEnumerable<string>>), actionMethod);

            return new Command("/" + name, action, adminOnly: adminOnly);
        }

        public void Invoke(Message message, IEnumerable<string> arguments)
        {
            if (Bot == null)
                return;

            _action.Invoke(Bot, message, arguments);
        }
    }
}
