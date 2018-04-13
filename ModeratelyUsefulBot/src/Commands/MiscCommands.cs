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
        [Argument(Name = "Reply text", Type = typeof(string), Description = "The text which the bot will reply with.", Optional = true, DefaultValue = "pong")]
        internal static void Ping(Bot bot, Message message, IEnumerable<string> arguments)
        {
            bot.BotClient.SendTextMessageAsync(message.Chat.Id, arguments.Count() > 0 ? String.Join(' ', arguments) : "pong");
        }

        internal static void GetLog(Bot bot, Message message, IEnumerable<string> arguments)
        {
            if (Log.FilePath == null || Log.FilePath == "")
            {
                bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Logging to file is disabled. Can't show current log file.");
                return;
            }

            var fileName = Log.FilePath.Split('/').Last();
            using (var fs = new FileStream(Log.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                if (arguments.Count() == 0 || arguments.First().ToLower() == "print")
                    using (var sr = new StreamReader(fs))
                        bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Current log file (" + fileName.Replace("_", "\\_") + "):\n```\n" + sr.ReadToEnd() + "\n```", Telegram.Bot.Types.Enums.ParseMode.Markdown);
                else if (arguments.First().ToLower() == "file")
                {
                    bot.BotClient.SendDocumentAsync(message.Chat.Id, new FileToSend(fileName, fs)).Wait();
                }
                else
                    bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Unknown argument \"" + arguments.First() + "\". Use print or file.");
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
            bot.BotClient.SendTextMessageAsync(message.Chat.Id, arguments.Count() == 0 ? _getCommandList(bot, message.From.Id) : _getCommandInfo(bot, message.From.Id, arguments.First()));
        }

        private static string _getCommandList(Bot bot, int user)
        {
            var isAdmin = bot.Admins.Contains(user);
            var result = "These are the commands available to you. Use /help followed by a command for more information on it.\n";
            foreach (var command in bot.Commands.Select(c => c.Value).Where(c => !c.AdminOnly || isAdmin))
            {
                result += "\n" + command.Name;
                if (Attribute.GetCustomAttribute(command.Action.Method, typeof(CommandAttribute)) is CommandAttribute commandAttribute)
                    result += " - " + commandAttribute.ShortDescription;
            }
            return result;
        }

        private static string _getCommandInfo(Bot bot, int user, string name)
        {
            if (!name.StartsWith('/'))
                name = '/' + name;
            if (!bot.Commands.TryGetValue(name, out Command command))
                return "Could not find command " + name + ".";
            if (!(Attribute.GetCustomAttribute(command.Action.Method, typeof(CommandAttribute)) is CommandAttribute commandAttribute))
                return "No documentation available for command " + name + ".";

            string result = commandAttribute.Name + " - " + commandAttribute.Description;

            var argumentAttributes = Attribute.GetCustomAttributes(command.Action.Method, typeof(ArgumentAttribute)).Select(a => a as ArgumentAttribute).Where(a => a != null);
            if (argumentAttributes.Count() == 0)
                result += "\n\nThis command takes no attributes.";
            else
            {
                result += "\n\nAttributes:";
                foreach (var argumentAttribute in argumentAttributes)
                {
                    result += "\n- " + argumentAttribute.Name + "\n  ";

                    result +=
                        argumentAttribute.Type == typeof(string) ? "Text" :
                        argumentAttribute.Type == typeof(int) ? "Integer" :
                        argumentAttribute.Type == typeof(float) ? "Decimal" : "Unknown type";

                    if (argumentAttribute.Optional)
                        result += ", optional";
                    if (argumentAttribute.DefaultValue != null)
                        result += ", default: " + argumentAttribute.DefaultValue;
                    result += ".\n  " + argumentAttribute.Description;
                }
            }

            if (command.AdminOnly && !bot.Admins.Contains(user))
                result += "\n\nYou can't use this command, it is available to admins only.";

            return result;
        }
    }
}
