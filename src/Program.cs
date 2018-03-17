using System;
using System.IO;
using System.Threading.Tasks;

namespace moderately_useful_bot
{
    class Program
    {
        static void Main(string[] args)
        {
            TestApiAsync().Wait();
            Console.ReadLine();
        }

        static async Task TestApiAsync()
        {
            using (StreamReader sr = new StreamReader("data/token.txt"))
            {
                var token = sr.ReadLine();
                Console.WriteLine("Token: " + token);
                var botClient = new Telegram.Bot.TelegramBotClient(token);
                var me = await botClient.GetMeAsync();
                Console.WriteLine("Hello! My name is " + me.FirstName);
            }
        }
    }
}
