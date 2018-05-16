using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModeratelyUsefulBot
{
    internal class Program
    {
        private const string Tag = "Main";
        internal static List<Bot> Bots;
        private static Timer _timer;
        private static bool _running = true;
        private static readonly AutoResetEvent ExitHandle = new AutoResetEvent(false);
        private static bool _requestRestart;

        private static int Main(string[] args)
        {
            Log.Enable(Config.GetDefault("log/consoleLevel", "Info"), Config.GetDefault("log/fileLevel", "Off"));
            Log.Info(Tag, "Starting...");
            _startBots();
            var now = DateTime.Now;
            var minute = now.Minute + 1;
            _timer = new Timer(_runTimedCommands, null, new DateTime(now.Year, now.Month, now.Day, now.Hour + minute / 60, minute % 60, 0) - now, TimeSpan.FromMinutes(1));
            Log.Info(Tag, "Type \"exit\" to stop the bot.");

            _checkConsoleInput();
            ExitHandle.WaitOne();
            return _requestRestart ? 1 : 0;
        }

        private static void _startBots()
        {
            Bots = new List<Bot>();
            var botIndex = 1;
            while (Config.DoesPropertyExist("bots/bot[" + botIndex + "]"))
            {
                var bot = Bot.CreateBot(botIndex++);
                if (bot != null)
                    Bots.Add(bot);
            }
        }

        private static void _runTimedCommands(object state)
        {
            Log.Debug(Tag, "Running timed commands.");
            Bots.ForEach(b => b.RunTimedCommands());
        }

        private static void _checkConsoleInput()
        {
            var currentLine = "";
            while (_running)
                // can't use Console.ReadLine - it would block the thread, preventing shutdown via command
                if (!Console.KeyAvailable)
                    Thread.Sleep(100);
                else
                {
                    var key = Console.ReadKey();
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
            switch (input)
            {
                case "exit":
                    Exit();
                    break;
                case "restart":
                    Exit(requestRestart: true);
                    break;
                default:
                    Log.Info(Tag, "Unknown console input: " + input);
                    break;
            }
        }

        internal static async void Exit(int secondsUntilExit = 5, bool requestRestart = false)
        {
            Log.Info(Tag, "Shutting down in " + secondsUntilExit + " seconds.");
            _running = false;
            _requestRestart = requestRestart;

            // always wait a bit to ensure the bot has confirmed receiving the message
            await Task.Delay(500);

            foreach (var bot in Bots)
                bot.BotClient.StopReceiving();

            await Task.Delay(secondsUntilExit * 1000);

            Log.Disable();
            ExitHandle.Set();
        }
    }
}
