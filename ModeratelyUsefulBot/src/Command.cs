using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ModeratelyUsefulBot
{
    class Command
    {
        private static List<int> _admins;

        private Action<TelegramBotClient, Message, IEnumerable<string>> _action;
        private bool _adminOnly;

        public string Name { private set; get; }
        public TelegramBotClient BotClient;

        static Command()
        {
            _admins = new List<int>();
            var index = 1;
            while(Config.Get("telegram/admins/id[" + index++ + "]", out int id))
                _admins.Add(id);
        }

        public Command(string name, Action<TelegramBotClient, Message, IEnumerable<string>> action, TelegramBotClient botClient = null, bool adminOnly = false)
        {
            BotClient = botClient;
            Name = name;
            _action = action;
            _adminOnly = adminOnly;
        }

        public void Invoke(Message message, IEnumerable<string> arguments)
        {
            if (BotClient == null)
                return;

            if(_adminOnly && !_admins.Contains(message.From.Id))
            {
                BotClient.SendTextMessageAsync(message.Chat.Id, "I don't want to do that.");
                return;
            }

            _action.Invoke(BotClient, message, arguments);
        }
    }
}
