using RedditSharp;
using RedditSharp.Things;
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Bot.Types;

namespace ModeratelyUsefulBot
{
    static class RedditCommands
    {
        private static string _tag = "Reddit";
        private static Reddit _reddit;
        private static Subreddit _dankmemes;
        private static int _postOffset;
        private static Random _random = new Random();

        static RedditCommands()
        {
            var webAgent = new BotWebAgent(
                Config.GetDefault("reddit/auth/username", ""),
                Config.GetDefault("reddit/auth/password", ""),
                Config.GetDefault("reddit/auth/clientId", ""),
                Config.GetDefault("reddit/auth/clientSecret", ""),
                Config.GetDefault("reddit/auth/redirectUri", ""));
            _reddit = new Reddit(webAgent, false);
            _dankmemes = _reddit.GetSubreddit("/r/dankmemes");
            // check number of pinned posts
            var offset = 0;
            var posts = _dankmemes.Hot.Take(3).GetEnumerator();
            while (posts.MoveNext() && posts.Current.IsStickied)
                offset++;
            _postOffset = offset;

            Log.Info(_tag, "Reddit API setup done.");
        }

        [Command(Name = "Get random meme", ShortDescription = "get random meme", Description = "Posts a random meme from the first page of /r/dankmemes.")]
        internal static void GetRandomMeme(Bot bot, Message message, IEnumerable<string> arguments)
        {
            int offset = _random.Next(25);
            var post = _dankmemes.Hot.Skip(_postOffset + offset).Take(1).First();
            var photo = new FileToSend(post.Url);
            bot.BotClient.SendPhotoAsync(message.Chat.Id, photo, post.Title);
        }

        internal static void LinkTopMeme(Bot bot, ChatId chatId)
        {
            Log.Debug(_tag, "Sending meme.");
            var post = _dankmemes.Hot.Skip(_postOffset).Take(1).First();
            bot.BotClient.SendTextMessageAsync(chatId, "It is " + DateTime.Now.DayOfWeek.ToString() + ", my dudes[.](" + post.Url + ")", Telegram.Bot.Types.Enums.ParseMode.Markdown);
        }
    }
}
