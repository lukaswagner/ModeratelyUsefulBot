using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using Telegram.Bot.Types;

namespace ModeratelyUsefulBot
{
    static class MiscCommands
    {
        [Command(Name = "Ping", ShortDescription = "test bot status", Description = "Checks if the Bot is available. Replies with the given text, or a sample text if none is specified.")]
        [Argument(Name = "Reply text", Description = "The text which the bot will reply with.", Optional = true, DefaultValue = "pong")]
        internal static void Ping(Bot bot, Message message, IEnumerable<string> arguments)
        {
            bot.BotClient.SendTextMessageAsync(message.Chat.Id, arguments.Count() > 0 ? String.Join(' ', arguments) : "pong");
        }

        internal static void GetLog(Bot bot, Message message, IEnumerable<string> arguments)
        {
            if (arguments.Count() == 0 || arguments.First().ToLower() == "print")
            {
                if (Log.FilePath == null || Log.FilePath == "")
                {
                    bot.BotClient.SendTextMessageAsync(message.Chat.Id, arguments.Count() > 0 ? String.Join(' ', arguments) : "Logging to file is disabled. Can't show current log file.");
                    return;
                }
                using (var fs = new FileStream(Log.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                    bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Current log file (" + Log.FilePath.Replace("_", "\\_") + "):\n```\n" + sr.ReadToEnd() + "\n```", Telegram.Bot.Types.Enums.ParseMode.Markdown);
            }
        }

        internal static void Exit(Bot bot, Message message, IEnumerable<string> arguments)
        {
            _exit(bot, message, arguments, false);
        }

        internal static void Restart(Bot bot, Message message, IEnumerable<string> arguments)
        {
            _exit(bot, message, arguments, true);
        }

        private static void _exit(Bot bot, Message message, IEnumerable<string> arguments, bool requestRestart)
        {
            if (arguments.Count() == 0 || !int.TryParse(arguments.First(), out int secondsUntilExit))
                secondsUntilExit = (int)((Action<int, bool>)Program.Exit).Method.GetParameters().First().DefaultValue;

            bot.BotClient.SendTextMessageAsync(message.Chat.Id, (requestRestart ? "Restarting" : "Shutting down") + " in " + secondsUntilExit + " seconds.");
            Program.Exit(secondsUntilExit, requestRestart);
        }

        internal static void SetAdminOnly(Bot bot, Message message, IEnumerable<string> arguments)
        {
            if (arguments.Count() < 2)
            {
                bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Please provide a command and a boolean.");
                return;
            }
            if (!bot.Commands.TryGetValue("/" + arguments.First(), out var command))
            {
                bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Could not find command.");
                return;
            }
            if (!bool.TryParse(arguments.Skip(1).First(), out var adminOnly))
            {
                bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Could not parse boolean.");
                return;
            }
            command.AdminOnly = adminOnly;
            bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Command " + command.Name + " is now available " + (adminOnly ? "to admins only." : "to everyone."));
        }

        internal static void Help(Bot bot, Message message, IEnumerable<string> arguments)
        {
            var isAdmin = bot.Admins.Contains(message.From.Id);
            var result = "You can use these commands:";
            foreach(var command in bot.Commands.Select(c => c.Value).Where(c => !c.AdminOnly || isAdmin))
            {
                result += "\n" + command.Name;
                if (Attribute.GetCustomAttribute(command.Action.Method, typeof(CommandAttribute)) is CommandAttribute commandAttribute)
                    result += " - " + commandAttribute.ShortDescription;
            }
            bot.BotClient.SendTextMessageAsync(message.Chat.Id, result);
        }
    }
}
