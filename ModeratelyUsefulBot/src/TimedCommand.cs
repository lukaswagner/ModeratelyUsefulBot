using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Telegram.Bot.Types;

namespace ModeratelyUsefulBot
{
    class TimedCommand
    {
        private static string _tag = "TimedCommand";
        private Action<Bot, ChatId> _action;
        private DateTime _next;
        private TimeSpan _timeSpan;
        private ChatId _chatId;
        internal Bot Bot;

        internal TimedCommand(DateTime start, TimeSpan timeSpan, Action<Bot, ChatId> action, ChatId chatId, Bot bot = null)
        {
            var now = DateTime.Now;
            var next = start;
            while (next < now)
                next += timeSpan;
            _next = next;
            _timeSpan = timeSpan;
            _action = action;
            _chatId = chatId;
            Bot = bot;
        }

        internal static TimedCommand CreateTimedCommand(string botPath, int index)
        {
            var path = botPath + "/timedCommands/timedCommand[" + index + "]";

            bool checkArg(bool success, string message)
            {
                if (!success)
                    Log.Error(_tag, "Could not create timedCommand with botPath " + botPath + " and index " + index + ". " + message);
                return success;
            }

            if (!checkArg(Config.DoesPropertyExist(path), "No settings found in config.")) return null;
            if (!checkArg(Config.Get(path + "/action", out string actionString), "No action found in config.")) return null;
            if (!checkArg(Config.Get(path + "/start", out string startString), "No start found in config.")) return null;
            if (!checkArg(Config.Get(path + "/timeSpan", out string timeSpanString), "No timespan found in config.")) return null;
            if (!checkArg(Config.Get(path + "/chatId", out int chatId), "No chatId found in config.")) return null;

            if (!checkArg(DateTime.TryParse(startString, out DateTime start), "Could not parse start.")) return null;
            if (!checkArg(TimeSpan.TryParse(timeSpanString, CultureInfo.InvariantCulture, out TimeSpan timeSpan), "Could not parse timespan.")) return null;

            var splitAction = actionString.Split('.');
            if (!checkArg(splitAction.Length == 2, "Action should contain class and method name divided by a period.")) return null;
            var actionClass = Type.GetType("ModeratelyUsefulBot." + splitAction[0]);
            if (!checkArg(actionClass != null, "Could not find class " + splitAction[0] + ".")) return null;
            RuntimeHelpers.RunClassConstructor(actionClass.TypeHandle);
            var actionMethod = actionClass.GetMethod(splitAction[1], BindingFlags.Static | BindingFlags.NonPublic);
            if (!checkArg(actionMethod != null, "Could not find method " + splitAction[1] + ".")) return null;
            var action = (Action<Bot, ChatId>)Delegate.CreateDelegate(typeof(Action<Bot, ChatId>), actionMethod);

            return new TimedCommand(start, timeSpan, action, new ChatId(chatId));
        }

        internal void RunIfTimeUp()
        {
            var now = DateTime.Now;
            if (_next <= now)
            {
                while (_next < now)
                    _next += _timeSpan;
                _action.Invoke(Bot, _chatId);
            }
        }
    }
}
