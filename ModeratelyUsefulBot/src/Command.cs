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
        private static string _tag = "Command";

        internal Action<Message, IEnumerable<string>> Action;
        internal IEnumerable<string> Names;
        internal Bot Bot;
        internal bool AdminOnly;
        internal Dictionary<string, object> Parameters;
        internal Dictionary<string, object> Data = new Dictionary<string, object>();

        internal Command(IEnumerable<string> names, MethodInfo action, Bot bot = null, bool adminOnly = false, Dictionary<string, object> parameters = null)
        {
            Bot = bot;
            Names = names;
            Action = (Action<Message, IEnumerable<string>>)action.CreateDelegate(typeof(Action<Message, IEnumerable<string>>), this);
            AdminOnly = adminOnly;
            Parameters = parameters ?? new Dictionary<string, object>();
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

            var parameters = _parseCommandParameters(path);

            return new Command(names.Select(n => "/" + n), actionMethod, adminOnly: adminOnly, parameters: parameters);
        }

        private static Dictionary<string, object> _parseCommandParameters(string commandPath)
        {
            if (!Config.DoesPropertyExist(commandPath + "/parameters"))
                return null;

            var result = new Dictionary<string, object>();
            var parameterIndex = 0;

            bool checkArg(bool success, string message)
            {
                if (!success)
                    Log.Warn(_tag, "Could not parse command parameters for command " + commandPath + ". Problem with parameter " + parameterIndex + ": " + message);
                return success;
            }

            string parameterPath;
            while((parameterPath = commandPath + "/parameters/parameter[" + ++parameterIndex + "]").Length > 0 && Config.DoesPropertyExist(parameterPath))
            {
                if (!checkArg(Config.Get(parameterPath + "/name", out string name), "Could not read parameter name. Skipping parameter.") ||
                    !checkArg(Config.Get(parameterPath + "/type", out string type), "Could not read parameter type. Skipping parameter.") ||
                    !checkArg(Config.Get(parameterPath + "/value", out string value), "Could not read parameter value. Skipping parameter."))
                    continue;

                object parsedValue;
                switch (type.ToLower())
                {
                    case "string":
                        parsedValue = value;
                        break;
                    case "int":
                    case "integer":
                        if (!checkArg(int.TryParse(value, out int intValue), "Could not parse parameter value as integer. Skipping parameter."))
                            continue;
                        else
                            parsedValue = intValue;
                        break;
                    case "bool":
                    case "boolean":
                        if (!checkArg(bool.TryParse(value, out bool boolValue), "Could not parse parameter value as boolean. Skipping parameter."))
                            continue;
                        else
                            parsedValue = boolValue;
                        break;
                    default:
                        checkArg(false, "Unknown parameter type. Custom parameters should be passed as strings. Skipping parameter.");
                        continue;
                }

                result.Add(name, parsedValue);
            }

            return result;
        }

        internal void Invoke(Message message, IEnumerable<string> arguments)
        {
            if (Bot == null)
                return;

            Action.Invoke(message, arguments);
        }
    }
}
