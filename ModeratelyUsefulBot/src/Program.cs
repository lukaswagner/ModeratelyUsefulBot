using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;

namespace ModeratelyUsefulBot
{
    class Program
    {
        private static string _tag = "Main";
        private static List<Bot> _bots;
        private static Timer _timer;

        private static void Main(string[] args)
        {
            Log.Enable(Config.GetDefault("log/consoleLevel", "Info"), Config.GetDefault("log/fileLevel", "Off"));
            _startBots();
            var now = DateTime.Now;
            var minute = now.Minute + 1;
            _timer = new Timer(_runTimedCommands, null, (new DateTime(now.Year, now.Month, now.Day, now.Hour + minute / 60, minute % 60, 0)) - now, TimeSpan.FromMinutes(1));
            Log.Info(_tag, "Type \"exit\" to stop the bot.");
            var running = true;
            while(running)
                if(Console.ReadLine() == "exit")
                {
                    Log.Info(_tag, "Shutting down.");
                    foreach (var bot in _bots)
                        bot.StopReceiving();
                    Log.Disable();
                    running = false;
                }
        }

        private static void _startBots()
        {
            _bots = new List<Bot>();
            var botIndex = 1;
            while (Config.DoesPropertyExist("bots/bot[" + botIndex + "]"))
            {
                var bot = Bot.CreateBot(botIndex++);
                if (bot != null)
                    _bots.Add(bot);
            }
        }

        private static void _runTimedCommands(object state)
        {
            Log.Debug(_tag, "Running timed commands.");
            _bots.ForEach(b => b.RunTimedCommands());
        }
    }
}
