using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ModeratelyUsefulBot
{
    static class MiscCommands
    {
        internal static void Ping(TelegramBotClient botClient, Message message, IEnumerable<string> arguments)
        {
            botClient.SendTextMessageAsync(message.Chat.Id, arguments.Count() > 0 ? String.Join(' ', arguments) : "pong");
        }
    }
}
