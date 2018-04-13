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

        [Command(Name = "Log", ShortDescription = "get event log", Description = "Retrieves the log file, if file logging is enabled. The log is either sent as message or as file.")]
        [Argument(Name = "Send method", Type = typeof(string), Description = "How to return the log. Available options are \"print\" (send as message) and \"file\" (send text file).", Optional = true, DefaultValue = "file")]
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
                    bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Unknown argument \"" + arguments.First() + "\". Use \"print\" or \"file.\"");
        }

        [Command(Name = "Exit", ShortDescription = "stop the bot", Description = "Stops the bot.")]
        [Argument(Name = "Seconds until exit", Type = typeof(int), Description = "Delay until the bot is stopped.", Optional = true, DefaultValue = "5")]
        internal static void Exit(Bot bot, Message message, IEnumerable<string> arguments)
        {
            _exit(bot, message, arguments, false);
        }

        [Command(Name = "Restart", ShortDescription = "restart the bot", Description = "Restarts the bot.")]
        [Argument(Name = "Seconds until restart", Type = typeof(int), Description = "Delay until the bot is restarted.", Optional = true, DefaultValue = "5")]
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

        [Command(Name = "Set AdminOnly", ShortDescription = "set who can use a command", Description = "Configures a comamnd to be available to all users or to admins only.")]
        [Argument(Name = "Command", Type = typeof(string), Description = "Name of the command, with or without the leading slash.")]
        [Argument(Name = "AdminOnly", Type = typeof(bool), Description = "If the command should be available to admins only (false - all users, true - admins only).")]
        internal static void SetAdminOnly(Bot bot, Message message, IEnumerable<string> arguments)
        {
            if (arguments.Count() < 2)
            {
                bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Please provide a command and a boolean.");
                return;
            }
            if (!bot.Commands.TryGetValue(arguments.First().StartsWith('/') ? arguments.First() : "/" + arguments.First(), out var command))
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

        [Command(Name = "Help", ShortDescription = "get help about available commands", Description = "Shows a list of available commands, or information about a given command.")]
        [Argument(Name = "Command", Type = typeof(string), Description = "Name of the command to get information on, with or without the leading slash.", Optional = true)]
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
                result += "\n\nThis command takes no arguments.";
            else
            {
                result += "\n\nArguments:";
                foreach (var argumentAttribute in argumentAttributes)
                {
                    result += "\n- " + argumentAttribute.Name + "\n  ";

                    result +=
                        argumentAttribute.Type == typeof(string) ? "Text" :
                        argumentAttribute.Type == typeof(int) ? "Integer" :
                        argumentAttribute.Type == typeof(float) ? "Decimal" :
                        argumentAttribute.Type == typeof(bool) ? "Boolean" : "Unknown type";

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
