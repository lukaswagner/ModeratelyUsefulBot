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
        private static List<Bot> _bots;

        private static void Main(string[] args)
        {
            _startBots();
            Console.WriteLine("Type \"exit\" to stop the bot.");
            var running = true;
            while(running)
                if(Console.ReadLine() == "exit")
                {
                    foreach (var bot in _bots)
                        bot.StopReceiving();
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
