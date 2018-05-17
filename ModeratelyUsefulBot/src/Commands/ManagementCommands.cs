using System;
using System.Collections.Generic;
using System.Linq;
using ModeratelyUsefulBot.Helper;
using Telegram.Bot.Types;

// ReSharper disable UnusedMember.Global

namespace ModeratelyUsefulBot.Commands
{
    internal static class ManagementCommands
    {
        // application management

        [Command(Name = "Exit", ShortDescription = "stop all bots", Description = "Stops the bot framework.")]
        [Argument(Name = "Seconds until exit", Type = typeof(int), Description = "Delay until the application is stopped.", Optional = true, DefaultValue = "5")]
        internal static void Exit(this Command command, Message message, IEnumerable<string> arguments)
        {
            _exit(command.Bot, message, arguments, false);
        }

        [Command(Name = "Restart", ShortDescription = "restart all bots", Description = "Restarts the bot framework.")]
        [Argument(Name = "Seconds until restart", Type = typeof(int), Description = "Delay until the application is restarted.", Optional = true, DefaultValue = "5")]
        internal static void Restart(this Command command, Message message, IEnumerable<string> arguments)
        {
            _exit(command.Bot, message, arguments, true);
        }
        private static void _exit(Bot bot, Message message, IEnumerable<string> arguments, bool requestRestart)
        {
            var argList = arguments.ToList();
            if (!argList.Any() || !int.TryParse(argList.First(), out var secondsUntilExit))
                secondsUntilExit = (int)((Action<int, bool>)Program.Exit).Method.GetParameters().First().DefaultValue;

            bot.BotClient.SendTextMessageAsync(message.Chat.Id, (requestRestart ? "Restarting" : "Shutting down") + " in " + secondsUntilExit + " seconds.");
            Program.Exit(secondsUntilExit, requestRestart);
        }

        // bot management

        [Command(Name = "List", ShortDescription = "list all bots", Description = "Lists all available bots.")]
        internal static void BotList(this Command command, Message message, IEnumerable<string> arguments)
        {
            var grouped = Program.Bots.GroupBy(b => b.BotClient.IsReceiving).ToDictionary(g => g.Key, g => g);

            var botList = "";

            void Print(IEnumerable<Bot> l) => botList = l.Aggregate(botList, (current, b) => current + "\n" + b.Name + " - " + b.BotClient.GetMeAsync().GetAwaiter().GetResult().FirstName);

            if (grouped.TryGetValue(true, out var active) && active.Any())
            {
                botList += "\n\nActive bots:";
                Print(active);
            }

            if (grouped.TryGetValue(false, out var inactive) && inactive.Any())
            {
                botList += "\n\nInactive bots:";
                Print(inactive);
            }

            command.Say(message, botList == "" ? "No bots avaiable (But who are you talking to?)." : "These are the available bots:" + botList);
        }

        [Command(Name = "Start", ShortDescription = "start a bot", Description = "Starts a bot.")]
        [Argument(Name = "Bot", Type = typeof(string), Description = "The bot to be started.")]
        internal static void Start(this Command command, Message message, IEnumerable<string> arguments)
        {
            var argList = arguments.ToList();
            if (!argList.Any())
            {
                command.Say(message, "Please provide a bot name.");
                return;
            }
            if (!_tryGetBot(command, message, argList.First(), out var target))
                return;
            if (target.BotClient.IsReceiving)
            {
                command.Say(message, "The target bot is already started.");
                return;
            }
            target.BotClient.StartReceiving();
            var msg = "Started bot " + target.Name + ".";
            command.Say(message, msg);
            Log.Info(command.Bot.TagWithName, msg);
        }

        [Command(Name = "Stop", ShortDescription = "stop a bot", Description = "Stops a bot.")]
        [Argument(Name = "Bot", Type = typeof(string), Description = "The bot to be stopped.")]
        [Argument(Name = "Self stop allowed", Type = typeof(bool), Description = "If the bot should stop itself.", Optional = true, DefaultValue = "false")]
        internal static void Stop(this Command command, Message message, IEnumerable<string> arguments)
        {
            var argList = arguments.ToList();
            if (!argList.Any())
            {
                command.Say(message, "Please provide a bot name.");
                return;
            }
            if (!_tryGetBot(command, message, argList.First(), out var target))
                return;
            if (target == command.Bot && (argList.Count < 2 || !bool.TryParse(argList.Skip(1).First(), out var self) || !self))
            {
                command.Say(message, "If you really want the bot to stop itself, provide \"true\" as second argument.");
                return;
            }
            if (!target.BotClient.IsReceiving)
            {
                command.Say(message, "The target bot is already stopped.");
                return;
            }
            target.BotClient.StopReceiving();
            var msg = "Stopped bot " + target.Name + ".";
            command.Say(message, msg);
            Log.Info(command.Bot.TagWithName, msg);
        }

        [Command(Name = "Set AdminOnly", ShortDescription = "set who can use a command", Description = "Configures a command to be available to all users or to admins only.")]
        [Argument(Name = "Bot", Type = typeof(string), Description = "Name of the bot whose command should be edited.")]
        [Argument(Name = "Command", Type = typeof(string), Description = "Name of the command, with or without the leading slash.")]
        [Argument(Name = "AdminOnly", Type = typeof(bool), Description = "If the command should be available to admins only (false - all users, true - admins only).")]
        internal static void SetAdminOnly(this Command command, Message message, IEnumerable<string> arguments)
        {
            var argList = arguments as string[] ?? arguments.ToArray();
            if (argList.Length < 3)
            {
                command.Say(message, "Please provide a bot, a command and a boolean.");
                return;
            }

            if (!_tryGetBot(command, message, argList.First(), out var target))
                return;
            arguments = argList.Skip(1);

            if (!target.Commands.TryGetValue(arguments.First().StartsWith('/') ? arguments.First() : "/" + arguments.First(), out var cmd))
            {
                command.Say(message, "Could not find command.");
                return;
            }
            arguments = arguments.Skip(1);

            if (!bool.TryParse(arguments.First(), out var adminOnly))
            {
                command.Say(message, "Could not parse boolean.");
                return;
            }
            cmd.AdminOnly = adminOnly;
            command.Say(message, "Command " + string.Join(" aka ", cmd.Names) + " of bot " + target.Name + " is now available " + (adminOnly ? "to admins only." : "to everyone."));
        }

        private static bool _tryGetBot(Command command, Message message, string name, out Bot target)
        {
            var caseSensitive = Program.Bots.Where(b => b.Name == name).ToList();
            if (caseSensitive.Any())
            {
                target = caseSensitive.First();
                return true;
            }
            var caseInsensitive = Program.Bots.Where(b => b.Name.ToLower() == name.ToLower()).ToList();
            if (!caseInsensitive.Any())
            {
                command.Say(message, "Could not find a bot with the given name.");
                target = null;
                return false;
            }
            if (caseInsensitive.Count > 1)
            {
                command.Say(message, "Multiple bots with similar names found. Please specify the target name case sensitive.");
                target = null;
                return false;
            }
            target = caseInsensitive.First();
            return true;
        }

        [Command(Name = "Commands Markup", ShortDescription = "get command markup for all bots", Description = "Creates a list of available commands of all bots, formatted to be sent to the Botfather.")]
        [Argument(Name = "Include admin commands", Type = typeof(bool), Description = "If admin commands should be included in the list.", Optional = true, DefaultValue = "false")]
        internal static void CommandMarkupAllBots(this Command command, Message message, IEnumerable<string> arguments)
        {
            var includeAdminCommands = false;

            var argList = arguments.ToList();
            if (argList.Any())
                bool.TryParse(argList.First().ToLower(), out includeAdminCommands);

            foreach (var bot in Program.Bots)
            {
                command.Say(message, "Commands for bot @" + bot.Username + ":" + MiscCommands.GetCommandList(bot, includeAdminCommands, false, true));
            }
        }
    }
}
