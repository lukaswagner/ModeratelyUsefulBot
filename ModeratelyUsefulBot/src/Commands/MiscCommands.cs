using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModeratelyUsefulBot.Helper;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

// ReSharper disable UnusedMember.Global

namespace ModeratelyUsefulBot.Commands
{
    internal static class MiscCommands
    {
        [Command(Name = "Ping", ShortDescription = "test bot status", Description = "Checks if the Bot is available. Replies with the given text, or a sample text if none is specified.")]
        [Argument(Name = "Reply text", Type = typeof(string), Description = "The text which the bot will reply with.", Optional = true, DefaultValue = "pong")]
        internal static void Ping(this Command command, Message message, IEnumerable<string> arguments)
        {
            var argList = arguments.ToList();
            command.Say(message, argList.Any() ? string.Join(' ', argList) : "pong");
        }

        [Command(Name = "Log", ShortDescription = "get event log", Description = "Retrieves the log file, if file logging is enabled. The log is either sent as message or as file.")]
        [Argument(Name = "Send method", Type = typeof(string), Description = "How to return the log. Available options are \"print\" (send as message) and \"file\" (send text file).", Optional = true, DefaultValue = "file")]
        internal static void GetLog(this Command command, Message message, IEnumerable<string> arguments)
        {
            if (string.IsNullOrEmpty(Log.FilePath))
            {
                command.Say(message, "Logging to file is disabled. Can't show current log file.");
                return;
            }

            var fileName = Log.FilePath.Split('/').Last();
            using (var fs = new FileStream(Log.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var argList = arguments.ToList();
                if (!argList.Any() || argList.First().ToLower() == "print")
                {
                    const int maxLength = 4096;
                    var header = "Current log file (" + fileName.Replace("_", "\\_") + "):\n";
                    const string codeHeader = "```\n";
                    const string codeFooter = "\n```";
                    const string partText = "(part {0} of {1})";

                    var remainingLength = maxLength - header.Length - codeHeader.Length - codeFooter.Length;

                    using (var sr = new StreamReader(fs))
                    {
                        var log = sr.ReadToEnd();
                        if (log.Length < remainingLength)
                            command.Say(message, header + codeHeader + log + codeFooter, Telegram.Bot.Types.Enums.ParseMode.Markdown);
                        else
                        {
                            sr.BaseStream.Position = 0;
                            sr.DiscardBufferedData();
                            // assume the part count will not exceed 999999
                            remainingLength -= partText.Length + 6;

                            var parts = new List<List<string>> { new List<string>() };
                            var curLength = 0;
                            var curPart = parts.Last();

                            while (!sr.EndOfStream)
                            {
                                var line = sr.ReadLine();
                                if (line == null)
                                    continue;

                                if (curLength + curPart.Count - 1 + line.Length < remainingLength)
                                {
                                    curLength += line.Length;
                                    curPart.Add(line);
                                }
                                else
                                {
                                    curLength = line.Length;
                                    curPart = new List<string> { line };
                                    parts.Add(curPart);
                                }
                            }

                            var partCount = parts.Count;
                            for (var i = 0; i < partCount; i++)
                                command.Say(message, header + codeHeader + string.Join('\n', parts[i]) + codeFooter + string.Format(partText, i + 1, partCount), Telegram.Bot.Types.Enums.ParseMode.Markdown).Wait();
                        }
                    }


                }

                else if (argList.First().ToLower() == "file")
                {
                    command.Bot.BotClient.SendDocumentAsync(message.Chat.Id, new FileToSend(fileName, fs)).Wait();
                }
                else
                    command.Say(message, "Unknown argument \"" + argList.First() + "\". Use \"print\" or \"file.\"");
            }
        }

        [Command(Name = "Log a lot", ShortDescription = "logs a lot of things", Description = "Logs a given amount of messages with a given length each. Used for debugging.")]
        [Argument(Name = "Number of logs", Type = typeof(int), Description = "Number of messages to log.", Optional = true, DefaultValue = "1")]
        [Argument(Name = "Number of characters", Type = typeof(int), Description = "Number of characters per log.", Optional = true, DefaultValue = "10")]
        internal static void LogALot(this Command command, Message message, IEnumerable<string> arguments)
        {
            var logs = 1;
            var charsPerLog = 10;
            var argList = arguments.ToList();
            if (argList.Any())
                int.TryParse(argList.First(), out logs);
            if (argList.Count > 1)
                int.TryParse(argList.Skip(1).First(), out charsPerLog);

            for (var i = 0; i < logs; i++)
                Log.Info(command.Bot.TagWithName, new string((char)0x262d, charsPerLog));
        }

        [Command(Name = "Help", ShortDescription = "get help about available commands", Description = "Shows a list of available commands, or information about a given command.")]
        [Argument(Name = "Command", Type = typeof(string), Description = "Name of the command to get information on, with or without the leading slash.", Optional = true)]
        internal static void Help(this Command command, Message message, IEnumerable<string> arguments)
        {
            var argList = arguments.ToList();
            command.Say(message, !argList.Any() ? _getCommandList(command.Bot, message.From.Id) : _getCommandInfo(command.Bot, message.From.Id, argList.First()));
        }

        private static string _getCommandList(Bot bot, int user)
        {
            var isAdmin = bot.Admins.Contains(user);
            var result = "These are the commands available to you. Use /help followed by a command for more information on it.\n";
            foreach (var command in bot.Commands.Select(c => c.Value).Where(c => !c.AdminOnly || isAdmin).Distinct())
            {
                result += "\n" + string.Join(" or ", command.Names);
                if (Attribute.GetCustomAttribute(command.Action.Method, typeof(CommandAttribute)) is CommandAttribute commandAttribute)
                    result += " - " + commandAttribute.ShortDescription;
            }
            return result;
        }

        private static string _getCommandInfo(Bot bot, int user, string name)
        {
            if (!name.StartsWith('/'))
                name = '/' + name;
            if (!bot.Commands.TryGetValue(name, out var command))
                return "Could not find command " + name + ".";
            if (!(Attribute.GetCustomAttribute(command.Action.Method, typeof(CommandAttribute)) is CommandAttribute commandAttribute))
                return "No documentation available for command " + name + ".";

            var result = commandAttribute.Name + " - " + commandAttribute.Description;

            var argumentAttributes = Attribute.GetCustomAttributes(command.Action.Method, typeof(ArgumentAttribute)).Select(a => a as ArgumentAttribute).Where(a => a != null).ToList();
            if (!argumentAttributes.Any())
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

        [Command(Name = "Send Text", ShortDescription = "send predefined text", Description = "Sends a predefined text.")]
        internal static void SendText(this Command command, Message message, IEnumerable<string> arguments)
        {
            command.Say(message, command.Parameters["text"] as string);
        }

        [Command(Name = "Mention", ShortDescription = "mentions users", Description = "Replies to the message, mentioning a given list of users.")]
        internal static void Mention(this Command command, Message message, IEnumerable<string> arguments)
        {
            var result = "";

            if (command.Parameters["userList"] is List<object> users)
                foreach (var user in users)
                {
                    var chat = command.Bot.BotClient.GetChatAsync(new ChatId((int) user)).GetAwaiter().GetResult();
                    result += string.Format("[{0}](tg://user?id={1}) ", chat.FirstName, (int) user);
                }
            else
                result = "No users specified.";

            command.Reply(message, result, ParseMode.Markdown);
        }
    }
}
