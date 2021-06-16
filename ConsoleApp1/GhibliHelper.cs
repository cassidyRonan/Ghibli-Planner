using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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

        static string CreateURL()
        {
            return string.Concat(BaseURL,FilmEndpoint);
        }

        public static MovieFile GetFilm(string filmName)
        {
            client = new WebClient();
            client.Headers.Add(HttpRequestHeader.ContentType, "application/json");
                    
            string response = client.DownloadString(string.Concat(CreateURL()));

            if (!string.IsNullOrEmpty(response))
            {
                MovieFile flm = JsonConvert.DeserializeObject<List<MovieFile>>(response)[0];

                //MainWindow.Instance.TxtBlkThreadInfo.Text = string.Concat(">", Thread.CurrentThread.Name, " has successfully gotten film info.");
                return flm;
            }

            //MainWindow.Instance.TxtBlkThreadInfo.Text = string.Concat(">", Thread.CurrentThread.Name, " is unable to access GetFilms() because its busy.");
            
            return null;

        }

        public static string GetFilms()
        {
            client = new WebClient();
            client.Headers.Add(HttpRequestHeader.ContentType, "application/json");

            string response = client.DownloadString(string.Concat(CreateURL()));

            //if (!string.IsNullOrEmpty(response))
            //{
            //    List<MovieFile> lst = JsonConvert.DeserializeObject<List<MovieFile>>(response);

            //    //MainWindow.Instance.TxtBlkThreadInfo.Text = string.Concat(">", Thread.CurrentThread.Name, " has successfully gotten film info.");
            //    return lst;
            //}

            //MainWindow.Instance.TxtBlkThreadInfo.Text = string.Concat(">",Thread.CurrentThread.Name," is unable to access GetFilms() because its busy.");
            return response;
        }
    }

}
