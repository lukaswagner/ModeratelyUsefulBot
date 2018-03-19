using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web.Enums;
using SpotifyAPI.Web.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SpotifyTokenGenerator
{
    class Program
    {
        private static AutorizationCodeAuth _auth;
        private static string _clientSecret;

        static void Main(string[] args)
        {
            using (StreamReader sr = new StreamReader("data/clientData.txt"))
            {
                _auth = new AutorizationCodeAuth()
                {
                    ClientId = sr.ReadLine(),
                    RedirectUri = "http://localhost",
                    Scope = Scope.UserReadPrivate,
                };
                _auth.StartHttpServer();
                _clientSecret = sr.ReadLine();
                _auth.OnResponseReceivedEvent += _callback;
                _auth.DoAuth();
            }

            Console.ReadLine();
        }

        static void _callback(AutorizationCodeAuthResponse response)
        {
            Token token = _auth.ExchangeAuthCode(response.Code, _clientSecret);

            Console.WriteLine(token.RefreshToken);

            _auth.StopHttpServer();
        }
    }
}
