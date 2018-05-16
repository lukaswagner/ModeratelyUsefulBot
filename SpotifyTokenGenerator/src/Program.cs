using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web.Enums;
using System;
using System.IO;

namespace SpotifyTokenGenerator
{
    internal class Program
    {
        private static AutorizationCodeAuth _auth;
        private static string _clientSecret;

        private static void Main()
        {
            using (var sr = new StreamReader("data/clientData.txt"))
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

        private static void _callback(AutorizationCodeAuthResponse response)
        {
            var token = _auth.ExchangeAuthCode(response.Code, _clientSecret);

            Console.WriteLine(token.RefreshToken);

            _auth.StopHttpServer();
        }
    }
}
