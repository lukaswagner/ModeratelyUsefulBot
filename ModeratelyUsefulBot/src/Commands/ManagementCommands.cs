using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Telegram.Bot.Types;

namespace ModeratelyUsefulBot
{
    static class ManagementCommands
    {
        // application management

        [Command(Name = "Exit", ShortDescription = "stop all bots", Description = "Stops the bot framework.")]
        [Argument(Name = "Seconds until exit", Type = typeof(int), Description = "Delay until the application is stopped.", Optional = true, DefaultValue = "5")]
        internal static void Exit(Bot bot, Message message, IEnumerable<string> arguments)
        {
            _exit(bot, message, arguments, false);
        }

        [Command(Name = "Restart", ShortDescription = "restart all bots", Description = "Restarts the bot framework.")]
        [Argument(Name = "Seconds until restart", Type = typeof(int), Description = "Delay until the application is restarted.", Optional = true, DefaultValue = "5")]
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

        // bot management

        [Command(Name = "List", ShortDescription = "list all bots", Description = "Lists all available bots.")]
        internal static void BotList(Bot bot, Message message, IEnumerable<string> arguments)
        {
            var grouped = Program.Bots.GroupBy(b => b.BotClient.IsReceiving).ToDictionary(g => g.Key, g => g);

            var botList = "";

            void print(IEnumerable<Bot> l)
            {
                foreach (var b in l)
                    botList += "\n" + b.Name + " - " + b.BotClient.GetMeAsync().GetAwaiter().GetResult().FirstName;
            }

            if (grouped.TryGetValue(true, out var active) && active.Count() > 0)
            {
                botList += "\n\nActive bots:";
                print(active);
            }

            if (grouped.TryGetValue(false, out var inactive) && inactive.Count() > 0)
            {
                botList += "\n\nInactive bots:";
                print(inactive);
            }

            bot.BotClient.SendTextMessageAsync(message.Chat.Id, botList == "" ? "No bots avaiable (But who are you talking to?)." : "These are the available bots:" + botList);
        }

        [Command(Name = "Start", ShortDescription = "start a bot", Description = "Starts a bot.")]
        [Argument(Name = "Bot", Type = typeof(string), Description = "The bot to be started.")]
        internal static void Start(Bot bot, Message message, IEnumerable<string> arguments)
        {
            if(arguments.Count() == 0)
            {
                bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Please provide a bot name.");
                return;
            }
            if (!_tryGetBot(bot, message, arguments.First(), out Bot target))
                return;
            if (target.BotClient.IsReceiving)
            {
                bot.BotClient.SendTextMessageAsync(message.Chat.Id, "The target bot is already started.");
                return;
            }
            target.BotClient.StartReceiving();
            var msg = "Started bot " + target.Name + ".";
            bot.BotClient.SendTextMessageAsync(message.Chat.Id, msg);
            Log.Info(bot.Tag, msg);
        }

        [Command(Name = "Stop", ShortDescription = "stop a bot", Description = "Stops a bot.")]
        [Argument(Name = "Bot", Type = typeof(string), Description = "The bot to be stopped.")]
        [Argument(Name = "Self stop allowed", Type = typeof(bool), Description = "If the bot should stop itself.", Optional = true, DefaultValue = "false")]
        internal static void Stop(Bot bot, Message message, IEnumerable<string> arguments)
        {
            if (arguments.Count() == 0)
            {
                bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Please provide a bot name.");
                return;
            }
            if (!_tryGetBot(bot, message, arguments.First(), out Bot target))
                return;
            if (target == bot && (arguments.Count() < 2 || !bool.TryParse(arguments.Skip(1).First(), out var self) || !self))
            {
                bot.BotClient.SendTextMessageAsync(message.Chat.Id, "If you really want the bot to stop itself, provide \"true\" as second argument.");
                return;
            }
            if (!target.BotClient.IsReceiving)
            {
                bot.BotClient.SendTextMessageAsync(message.Chat.Id, "The target bot is already stopped.");
                return;
            }
            target.BotClient.StopReceiving();
            var msg = "Stopped bot " + target.Name + ".";
            bot.BotClient.SendTextMessageAsync(message.Chat.Id, msg);
            Log.Info(bot.Tag, msg);
        }

        [Command(Name = "Set AdminOnly", ShortDescription = "set who can use a command", Description = "Configures a command to be available to all users or to admins only.")]
        [Argument(Name = "Bot", Type = typeof(string), Description = "Name of the bot whose command should be edited.")]
        [Argument(Name = "Command", Type = typeof(string), Description = "Name of the command, with or without the leading slash.")]
        [Argument(Name = "AdminOnly", Type = typeof(bool), Description = "If the command should be available to admins only (false - all users, true - admins only).")]
        internal static void SetAdminOnly(Bot bot, Message message, IEnumerable<string> arguments)
        {
            if (arguments.Count() < 3)
            {
                bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Please provide a bot, a command and a boolean.");
                return;
            }

            if (!_tryGetBot(bot, message, arguments.First(), out Bot target))
                return;
            arguments = arguments.Skip(1);

            if (!target.Commands.TryGetValue(arguments.First().StartsWith('/') ? arguments.First() : "/" + arguments.First(), out var command))
            {
                bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Could not find command.");
                return;
            }
            arguments = arguments.Skip(1);

            if (!bool.TryParse(arguments.First(), out var adminOnly))
            {
                bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Could not parse boolean.");
                return;
            }
            command.AdminOnly = adminOnly;
            bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Command " + string.Join(" aka ",  command.Names) + " of bot " + target.Name + " is now available " + (adminOnly ? "to admins only." : "to everyone."));
        }

        private static bool _tryGetBot(Bot bot, Message message, string name, out Bot target)
        {
            var caseSensitive = Program.Bots.Where(b => b.Name == name);
            if(caseSensitive.Count() > 0)
            {
                target = caseSensitive.First();
                return true;
            }
            var caseInsensitive = Program.Bots.Where(b => b.Name.ToLower() == name.ToLower());
            if(caseInsensitive.Count() == 0)
            {
                bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Could not find a bot with the given name.");
                target = null;
                return false;
            }
            if(caseInsensitive.Count() > 1)
            {
                bot.BotClient.SendTextMessageAsync(message.Chat.Id, "Multiple bots with similar names found. Please specify the target name case sensitive.");
                target = null;
                return false;
            }
            target = caseInsensitive.First();
            return true;
        }
    }
}
