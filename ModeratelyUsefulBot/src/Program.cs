using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModeratelyUsefulBot
{
    class Program
    {
        private static string _tag = "Main";
        private static List<Bot> _bots;
        private static Timer _timer;
        private static bool _running = true;
        private static AutoResetEvent _exitHandle = new AutoResetEvent(false);
        private static bool _requestRestart = false;

        private static int Main(string[] args)
        {
            Log.Enable(Config.GetDefault("log/consoleLevel", "Info"), Config.GetDefault("log/fileLevel", "Off"));
            Log.Info(_tag, "Starting...");
            _startBots();
            var now = DateTime.Now;
            var minute = now.Minute + 1;
            _timer = new Timer(_runTimedCommands, null, (new DateTime(now.Year, now.Month, now.Day, now.Hour + minute / 60, minute % 60, 0)) - now, TimeSpan.FromMinutes(1));
            Log.Info(_tag, "Type \"exit\" to stop the bot.");

            _checkConsoleInput();
            _exitHandle.WaitOne();
            return _requestRestart ? 1 : 0;
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

        private static void _checkConsoleInput()
        {
            var currentLine = "";
            ConsoleKeyInfo key;
            while (_running)
                // can't use Console.ReadLine - it would block the thread, preventing shutdown via command
                if (!Console.KeyAvailable)
                    Thread.Sleep(100);
                else
                {
                    key = Console.ReadKey();
                    if (key.Key == ConsoleKey.Enter)
                    {
                        _handleConsoleInput(currentLine);
                        currentLine = "";
                    }
                    else
                        currentLine += key.KeyChar;
                }
        }

        private static void _handleConsoleInput(string input)
        {
            if (input == "exit")
                Exit();
            if (input == "restart")
                Exit(requestRestart: true);
        }

        internal static async void Exit(int secondsUntilExit = 5, bool requestRestart = false)
        {
            Log.Info(_tag, "Shutting down in " + secondsUntilExit + " seconds.");
            _running = false;
            _requestRestart = requestRestart;

            // always wait a bit to ensure the bot has confirmed receiving the message
            await Task.Delay(500);

            foreach (var bot in _bots)
                bot.StopReceiving();

            await Task.Delay(secondsUntilExit * 1000);

            Log.Disable();
            _exitHandle.Set();
        }
    }
}
