using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web.Enums;
using SpotifyAPI.Web.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace spotify_token_generator
{
    class Program
    {
        static SpotifyWebAPI _spotify;
        static ImplicitGrantAuth auth;

        static void Main(string[] args)
        {
            using (StreamReader sr = new StreamReader("data/clientId.txt"))
            {
                auth = new ImplicitGrantAuth()
                {
                    ClientId = sr.ReadLine(),
                    RedirectUri = "http://localhost",
                    Scope = Scope.UserReadPrivate,
                };
                auth.StartHttpServer();
                auth.OnResponseReceivedEvent += _callback;
                auth.DoAuth();
}
            

            Console.ReadLine();
        }

        static void _callback(Token token, string state)
        {
            Console.WriteLine(token.AccessToken);

            auth.StopHttpServer();
        }
    }
}
