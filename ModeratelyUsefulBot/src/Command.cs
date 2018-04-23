using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Telegram.Bot.Types;

namespace ModeratelyUsefulBot
{
    class Command
    {
        internal Action<Bot, Message, IEnumerable<string>> Action;

        private static string _tag = "Command";
        public IEnumerable<string> Names { private set; get; }
        public Bot Bot;
        public bool AdminOnly;

        public Command(IEnumerable<string> names, Action<Bot, Message, IEnumerable<string>> action, Bot bot = null, bool adminOnly = false)
        {
            Bot = bot;
            Names = names;
            Action = action;
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
            if (!checkArg(Config.DoesPropertyExist(path + "/name") || Config.DoesPropertyExist(path + "/names"), "No name found in config.")) return null;

            List<string> names = new List<string>();
            if (Config.DoesPropertyExist(path + "/name") && Config.Get(path + "/name", out string name))
                names.Add(name);
            else
            {
                var nameIndex = 1;
                while(Config.DoesPropertyExist(path + "/names/name[" + nameIndex + "]"))
                    if (Config.Get(path + "/names/name[" + nameIndex++ + "]", out name))
                        names.Add(name);
            }

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

            return new Command(names.Select(n => "/" + n), action, adminOnly: adminOnly);
        }

        public void Invoke(Message message, IEnumerable<string> arguments)
        {
            if (Bot == null)
                return;

            Action.Invoke(Bot, message, arguments);
        }
    }
}
