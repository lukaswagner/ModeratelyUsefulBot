using System;
using System.Collections.Generic;
using System.Linq;
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

        private static void Main(string[] args)
        {
            Log.Enable(Config.GetDefault("log/level", "Info"));
            _startBots();
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
    }
}
