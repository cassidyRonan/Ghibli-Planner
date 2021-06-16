using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GhibliPlanner
{
    [Serializable]
    public class Ghibli
    {
        Thread RetrievalThread;

        public List<MovieFile> Movies = new List<MovieFile>();
        public List<DiscordRecord> DiscordRecords = new List<DiscordRecord>();

        #region Discord Records

        public void AddRecord(DiscordRecord record)
        {
            if (Monitor.TryEnter(DiscordRecords))
            {
                DiscordRecords.Add(record);
                Monitor.Exit(DiscordRecords);
                Monitor.PulseAll(DiscordRecords);
            }
        }

        public void RemoveRecord(string serverName)
        {
            if (Monitor.TryEnter(DiscordRecords))
            {
                DiscordRecord record = DiscordRecords.Where(d => d.ServerName == serverName).SingleOrDefault();
                DiscordRecords.Remove(record);
                Monitor.Exit(DiscordRecords);
                Monitor.Pulse(DiscordRecords);
            }
        }

        #endregion

        #region Movie Files

        public MovieFile GetMovie(string movieName)
        {
            MovieFile movie = new MovieFile();
            try
            {
                Thread.MemoryBarrier(); //Gaurantees that if thread is run after Refresh List Movies then it will retrieve new data

                Monitor.Enter(Movies);

                movie = Movies.Where(m => m.title == movieName).SingleOrDefault();

                Monitor.Exit(Movies);
                Monitor.Pulse(Movies);

                Thread.MemoryBarrier();
            }
            catch (ThreadInterruptedException)
            {
                //MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.TxtBlkThreadInfo.Text = string.Concat(Thread.CurrentThread.Name, " - Get Movie has been interrupted."));
            }

            return movie;
        }

        public void SetMovies(object json)
        {
            try
            {
                if (Monitor.TryEnter(Movies))
                {
                    List<MovieFile> mov;

                    if (Movies.Count > 0)
                    {
                        Monitor.Pulse(Movies);
                        Monitor.Wait(Movies);
                        Movies.Clear();
                        mov = Utility.ConvertFromJson((string)json);
                        //MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.LstBxGhibliMovies.ItemsSource = Movies);
                    }
                    else
                    {
                        Movies.Clear();
                        mov = Utility.ConvertFromJson((string)json);
                        //MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.LstBxGhibliMovies.ItemsSource = Movies);
                    }

                    foreach (var item in mov)
                    {
                            Movies.Add(item);
                    }

                    Monitor.Pulse(Movies);
                    Monitor.Exit(Movies);
                }
            }
            catch (ThreadInterruptedException)
            {
                //MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.TxtBlkThreadInfo.Text = string.Concat(Thread.CurrentThread.Name," - Set Movies has been interrupted."));
            }
        }

        #endregion

        #region Thread Creation

        public void RetrieveFilms()
        {
            Thread thread = new Thread(new ParameterizedThreadStart(SetMovies));
            thread.Name = "Create Retrieval Thread";
            thread.Priority = ThreadPriority.Highest;

            RetrievalThread = thread;
            RetrievalThread.Start(GhibliHelper.GetFilms());
        }

        #endregion
    }

    [Serializable]
    public class MovieFile
    {
        public string id;
        public string title;
        public string original_title;
        public string original_title_romanised;
        public string description;
        public string director;
        public string producer;
        public string release_date;
        public int running_time;
        public int rt_score;

        public List<string> people;
        public List<string> species;
        public List<string> locations;
        public List<string> vehicles;

        public string url;

        public string MovieInfo()
        {
            return string.Concat(title, "\n", "Running Time:", running_time, "\n", "Release Date:", release_date, "\n", "Description:\n", description);
        }

        public override string ToString()
        {
            return title;
        }
    }

    [Serializable]
    public class DiscordRecord
    {
        public string ServerName;
        public string WebhookURL;
    }

    [Serializable]
    public class EventRecord
    {
        public string MovieTitle;
        public DateTime Date;
    }
}
