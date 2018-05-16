using RedditSharp;
using RedditSharp.Things;
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Bot.Types;

// ReSharper disable UnusedMember.Global

namespace ModeratelyUsefulBot
{
    internal static class RedditCommands
    {
        private const string Tag = "Reddit";
        private static readonly Reddit Reddit;
        private static readonly Subreddit Dankmemes;
        private static readonly int PostOffset;
        private static readonly Random Random = new Random();

        static RedditCommands()
        {
            var webAgent = new BotWebAgent(
                Config.GetDefault("reddit/auth/username", "", "credentials"),
                Config.GetDefault("reddit/auth/password", "", "credentials"),
                Config.GetDefault("reddit/auth/clientId", "", "credentials"),
                Config.GetDefault("reddit/auth/clientSecret", "", "credentials"),
                Config.GetDefault("reddit/auth/redirectUri", "", "credentials"));
            Reddit = new Reddit(webAgent, false);
            Dankmemes = Reddit.GetSubreddit("/r/dankmemes");
            // check number of pinned posts
            var offset = 0;
            var posts = Dankmemes.Hot.Take(3).GetEnumerator();
            while (posts.MoveNext() && posts.Current.IsStickied)
                offset++;
            posts.Dispose();
            PostOffset = offset;

            Log.Info(Tag, "Reddit API setup done.");
        }

        [Command(Name = "Get random meme", ShortDescription = "get random meme", Description = "Posts a random meme from the first page of /r/dankmemes.")]
        internal static void GetRandomMeme(this Command command, Message message, IEnumerable<string> arguments)
        {
            var offset = Random.Next(25);
            var post = Dankmemes.Hot.Skip(PostOffset + offset).Take(1).First();
            var photo = new FileToSend(post.Url);
            command.Bot.BotClient.SendPhotoAsync(message.Chat.Id, photo, post.Title);
        }

        internal static void LinkTopMeme(Bot bot, ChatId chatId)
        {
            Log.Debug(Tag, "Sending meme.");
            var post = Dankmemes.Hot.Skip(PostOffset).Take(1).First();
            bot.BotClient.SendTextMessageAsync(chatId, "It is " + DateTime.Now.DayOfWeek + ", my dudes[.](" + post.Url + ")", Telegram.Bot.Types.Enums.ParseMode.Markdown);
        }
    }
}
