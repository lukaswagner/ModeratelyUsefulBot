using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;

namespace moderately_useful_bot
{
    class Program
    {
        private static Bot _moderatelyUsefulBot;
        private static Bot _additionalTestBot;

        private static void Main(string[] args)
        {
            SpotifyCommands.SetUpSpotify();
            _startBot();
            Console.WriteLine("Type \"exit\" to stop the bot.");
            var running = true;
            while(running)
                if(Console.ReadLine() == "exit")
                {
                    _moderatelyUsefulBot.StopReceiving();
                    _additionalTestBot.StopReceiving();
                    running = false;
                }
        }

        private static void _startBot()
        {
            var token1 = Config.GetDefault("telegram/token[1]", "");

            var commands1 = new List<Command>()
            {
                new Command("/ping", MiscCommands.Ping, adminOnly: true),
                new Command("/playlist", SpotifyCommands.SendPlaylistStats)
            };

            _moderatelyUsefulBot = new Bot(token1, commands1, "Sorry, but I don't know how to do that. I'm just moderately useful, after all.");

            var token2 = Config.GetDefault("telegram/token[2]", "");

            var commands2 = new List<Command>()
            {
                new Command("/ping", MiscCommands.Ping)
            };

            _additionalTestBot = new Bot(token2, commands2);
        }
    }
}
