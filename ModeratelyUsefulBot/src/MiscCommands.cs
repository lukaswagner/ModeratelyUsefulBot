using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ModeratelyUsefulBot
{
    static class MiscCommands
    {
        internal static void Ping(TelegramBotClient botClient, Message message, IEnumerable<string> arguments)
        {
            botClient.SendTextMessageAsync(message.Chat.Id, arguments.Count() > 0 ? String.Join(' ', arguments) : "pong");
        }

        internal static void GetLog(TelegramBotClient botClient, Message message, IEnumerable<string> arguments)
        {
            if(arguments.Count() == 0 || arguments.First().ToLower() == "print")
            {
                if(Log.FilePath == null || Log.FilePath == "")
                {
                    botClient.SendTextMessageAsync(message.Chat.Id, arguments.Count() > 0 ? String.Join(' ', arguments) : "Logging to file is disabled. Can't show current log file.");
                    return;
                }
                using (var fs = new FileStream(Log.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                    botClient.SendTextMessageAsync(message.Chat.Id, "Current log file (" + Log.FilePath.Replace("_", "\\_") + "):\n```\n" + sr.ReadToEnd() + "\n```", Telegram.Bot.Types.Enums.ParseMode.Markdown);
            }
        }

        internal static void Exit(TelegramBotClient botClient, Message message, IEnumerable<string> arguments)
        {
            if(arguments.Count() > 0 && int.TryParse(arguments.First(), out int secondsUntilExit))
            {
                botClient.SendTextMessageAsync(message.Chat.Id, "Shutting down in " + secondsUntilExit + " seconds.");
                Program.Exit(secondsUntilExit);
            }
            else
            {
                botClient.SendTextMessageAsync(message.Chat.Id, "Shutting down in " + ((Action<int>)Program.Exit).Method.GetParameters().First().DefaultValue + " seconds.");
                Program.Exit();
            }
        }
    }
}
