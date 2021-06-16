using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GhibliPlanner
{
    public class GhibliHelper
    {
        static WebClient client = new WebClient();

        static public string BaseURL = @"https://ghibliapi.herokuapp.com/";
        static public string FilmEndpoint = @"films";

        /// <summary>
        /// Creates endpoint URL path for downloading of films data
        /// </summary>
        /// <returns></returns>
        static string CreateURL()
        {
            return string.Concat(BaseURL,FilmEndpoint);
        }

        /// <summary>
        /// Retrieves films json data from API using WebClient
        /// </summary>
        /// <returns></returns>
        public static string GetFilms()
        {
            client = new WebClient();
            client.Headers.Add(HttpRequestHeader.ContentType, "application/json");

            //retrieve json data from endpoint
            string response = client.DownloadString(string.Concat(CreateURL()));
            
            return response;
        }

        /// <summary>
        /// Sends post request to passed in URL endpoint along with message and username
        /// </summary>
        /// <param name="URL"></param>
        /// <param name="msg"></param>
        /// <param name="username"></param>
        public static void SendToWebHook(string URL, string msg, string username)
        {
            HttpHelper.Post(URL, new NameValueCollection()
            {
                {"username",username },
                {"content",msg }
            });
        }
    }

    class HttpHelper
    {
        public static byte[] Post(string uri, NameValueCollection pairs)
        {
            using (WebClient webClient = new WebClient())
                return webClient.UploadValues(uri, pairs);
        }
    }

}
