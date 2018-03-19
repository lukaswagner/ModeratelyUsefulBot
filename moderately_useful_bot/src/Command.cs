using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace moderately_useful_bot
{
    class Command
    {
        private static List<int> _admins;

        private TelegramBotClient _botClient;
        private Action<TelegramBotClient, Message, IEnumerable<string>> _action;
        private bool _adminOnly;

        public string Name { private set; get; }

        static Command()
        {
            _admins = new List<int>();
            var index = 1;
            while(Config.Get("telegram/admins/id[" + index++ + "]", out int id))
                _admins.Add(id);
        }

        public Command(TelegramBotClient botClient, string name, Action<TelegramBotClient, Message, IEnumerable<string>> action, bool adminOnly = false)
        {
            _botClient = botClient;
            Name = name;
            _action = action;
            _adminOnly = adminOnly;
        }

        public void Invoke(Message message, IEnumerable<string> arguments)
        {
            if(_adminOnly && !_admins.Contains(message.From.Id))
            {
                _botClient.SendTextMessageAsync(message.Chat.Id, "I don't want to do that.");
                return;
            }

            _action.Invoke(_botClient, message, arguments);
        }
    }
}
