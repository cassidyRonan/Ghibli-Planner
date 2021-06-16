using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace GhibliPlanner
{
    [Serializable]
    public class Ghibli
    {
        //Isolated Storage Path Constants
        public const string discordFileName = "records.txt";
        public const string eventsFileName = "events.txt";
        public const string userDataDir = "UserData";

        IsolatedStorageFile isoFile;

        //Application Threads
        Thread SetMoviesThread;
        Thread GetMoviesThread;

        Thread DiscordSaveThread;
        Thread DiscordLoadThread;

        Thread EventSaveThread;
        Thread EventLoadThread;

        //Runtime Lists/Objects of Application data
        public List<MovieFile> Movies = new List<MovieFile>();
        public List<DiscordRecord> DiscordRecords = new List<DiscordRecord>();
        public List<EventRecord> EventRecords = new List<EventRecord>();

        public MovieFile RetrievedMovie = new MovieFile();

        //CTOR
        public Ghibli()
        {
            //Get user isolated storage file from assembly
            isoFile = IsolatedStorageFile.GetUserStoreForAssembly();

            //Creation of directory and files if they do not previously exist
            if(!isoFile.DirectoryExists(userDataDir))
            {
                isoFile.CreateDirectory(userDataDir);
            }

            if (!isoFile.FileExists(Path.Combine(userDataDir,discordFileName)))
            {
                isoFile.CreateFile(Path.Combine(userDataDir, discordFileName));
            }

            if (!isoFile.FileExists(Path.Combine(userDataDir, eventsFileName)))
            {
                isoFile.CreateFile(Path.Combine(userDataDir, eventsFileName));
            }

        }


        #region Discord Records

        /// <summary>
        /// Allows the addition of a discord record.
        /// </summary>
        /// <param name="record">Server Name and Webhook URL.</param>
        public void AddRecord(DiscordRecord record)
        {
            //Enter discord record list runtime object
            if (Monitor.TryEnter(DiscordRecords,3000))
            {
                //Add the record
                DiscordRecords.Add(record);
                Monitor.Exit(DiscordRecords);
            }
        }

        /// <summary>
        /// Allows the addition of an event record.
        /// </summary>
        /// <param name="record"></param>
        public void AddRecord(EventRecord record)
        {
            //Enter event record list runtime object
            if (Monitor.TryEnter(EventRecords, 3000))
            {
                //Add the record
                EventRecords.Add(record);
                Monitor.Exit(EventRecords);
            }
        }

        /// <summary>
        /// Allows the removal of a discord event
        /// </summary>
        /// <param name="serverName"></param>
        public void RemoveRecord(string serverName)
        {
            //Enter discord record list runtime object
            if (Monitor.TryEnter(DiscordRecords,3000))
            {
                //Finds the specified discord record where the server name matches
                DiscordRecord record = DiscordRecords.Where(d => d.ServerName == serverName).SingleOrDefault();

                //Removal of record 
                DiscordRecords.Remove(record);
                Monitor.Exit(DiscordRecords);
            }
        }

        /// <summary>
        /// Allows the remocal of an Event Record
        /// </summary>
        /// <param name="record"></param>
        public void RemoveRecord(EventRecord record)
        {
            //Enter event record list runtime object
            if (Monitor.TryEnter(EventRecords, 3000))
            {
                //Finds the specified event record where the movie name and date matches
                EventRecord rcrd = EventRecords.Where(d => d.MovieTitle == record.MovieTitle && d.Date.ToShortDateString() == record.Date.ToShortDateString()).SingleOrDefault();
                EventRecords.Remove(rcrd);
                Monitor.Exit(EventRecords);
            }
        }

        #endregion


        #region Movie Files

        /// <summary>
        /// Retrieves the movie object for the specified title
        /// </summary>
        public void GetMovie(object movieName)
        {
            //Checks the thread state to see if Set Movies is running and if so, it joins the current thread to it to ensure this runs after
            if (SetMoviesThread.ThreadState == ThreadState.Running)
                SetMoviesThread.Join();

            try
            {
                Thread.MemoryBarrier(); //Gaurantees that if thread is run after Refresh List Movies then it will retrieve new data

                if (Monitor.TryEnter(Movies, 3000))
                {
                    //Gets the movie matching the passed in title
                    RetrievedMovie = Movies.Where(m => m.title == (string)movieName).SingleOrDefault();

                    //Uses the dispatcher to update the UI with movie info
                    MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.TxtBlkMovieInfo.Text = RetrievedMovie.MovieInfo());

                    //Display of thread status on main UI
                    string thrdName = Thread.CurrentThread.Name;
                    MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.TxtBlkThreadInfo.Text = string.Concat(">", thrdName, " - Movie Info Retrieved and displayed."));
                    Monitor.Exit(Movies);
                }
            }
            catch (ThreadInterruptedException)
            {
                string thrdName = Thread.CurrentThread.Name;
                MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.TxtBlkThreadInfo.Text = string.Concat(">",thrdName, " - Get Movie has been interrupted."));
            }

        }

        /// <summary>
        /// Retrieves the movie list from the Ghibli REST API.
        /// </summary>
        /// <param name="req">Set Movie Request object</param>
        public void SetMovies(object req)
        {
            try
            {
                //Cast set movie request as class
                SetMovieRequest data = (SetMovieRequest)req;

                //Gain lock on movies, try for 3 seconds if unsuccessful
                if (Monitor.TryEnter(Movies,3000))
                {
                    List<MovieFile> movieList;

                    //If there are movies in the list, check if any other thread requires the Movies list object.
                    if (Movies.Count > 0 )
                    {
                        Monitor.Pulse(Movies);
                        Monitor.Wait(Movies,100); //Releases thread to object queue after 100ms
                    }

                    //Clears the movie list
                    Movies.Clear();

                    //Converts the json data into a movie list
                    movieList = Utility.ConvertFromJson(data.JsonData);

                    //Updates the Status bar progress text for this operation
                    MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.TxtBlkProgressMsg.Text = "Downloading:");

                    //Adding of movies to runtime core list
                    for (int i = 0; i < movieList.Count; i++)
                    {
                        //Adding items to list rather than setting entire list as it releases the lock on the object
                        Movies.Add(movieList[i]);
                        
                        //Calculation of the download progress or in this case the adding of movies to the list as the movies were all downloaded together
                        double percent = (((double)i + 1) / (double)movieList.Count);
                        MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.PrgBrStatusBar.Value = percent);

                        //Here we are blocking the thread every 300ms to give the user an oppurtunity to cancel the operation
                        Thread.Sleep(300);

                        //Checking if the cancellation token has had cancellation requested
                        if (data.Token.IsCancellationRequested)
                        {
                            string thdName = Thread.CurrentThread.Name;
                            MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.TxtBlkThreadInfo.Text = string.Concat("> ",thdName," Set Movie Thread has been cancelled."));

                            //Exit lock on Movies list object before exiting the thread
                            Monitor.Exit(Movies);
                            return;
                        }
                    }

                    //Setting of list to the WPF List box
                    MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.LstBxGhibliMovies.ItemsSource = Movies);

                    string thrdName = Thread.CurrentThread.Name;
                    MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.TxtBlkThreadInfo.Text = string.Concat("> ", thrdName, " has retrieved updated list."));

                    Monitor.Exit(Movies);
                }
            }
            catch (ThreadInterruptedException)
            {
                MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.TxtBlkThreadInfo.Text = string.Concat("> ",Thread.CurrentThread.Name," - Set Movies has been interrupted."));
            }
        }

        #endregion


        #region Thread Creation

        /// <summary>
        /// Creates and starts the retrieve films thread and sets the cancellation token source object.
        /// </summary>
        /// <param name="tokenSource"></param>
        public void RetrieveFilms(ref CancellationTokenSource tokenSource)
        {
            //Creation of thread and setting of thread variables
            Thread thread = new Thread(new ParameterizedThreadStart(SetMovies));
            thread.Name = "Create Retrieval Thread";
            thread.Priority = ThreadPriority.Highest;

            //Check if thread is null or if an instance is already created
            if (SetMoviesThread != null)
            {
                //Cancellation of previous token then thread
                if (tokenSource != null && !tokenSource.IsCancellationRequested)
                {
                    tokenSource.Cancel();
                }
                else if(tokenSource == null)
                {
                    MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.TxtBlkThreadInfo.Text = string.Concat(Thread.CurrentThread.Name, " - Retrieve Film has detected a rogue thread."));
                }
            }

            //Setting of new thread and starting of new thread
            SetMoviesThread = null;
            SetMoviesThread = thread;
            tokenSource = new CancellationTokenSource();
            SetMoviesThread.Start(new SetMovieRequest(GhibliHelper.GetFilms(),tokenSource.Token));
        }

        /// <summary>
        /// Creates and starts the get film thread.
        /// </summary>
        /// <param name="movieName"></param>
        public void CreateGetFilm(string movieName)
        {
            //Creation of thread and setting of thread variables
            Thread thread = new Thread(new ParameterizedThreadStart(GetMovie));
            thread.Name = "Get Movie Thread";
            thread.Priority = ThreadPriority.AboveNormal;
            thread.IsBackground = true;

            //Aborting thread if one already exists
            if (GetMoviesThread != null)
                GetMoviesThread.Abort();

            GetMoviesThread = null;
            GetMoviesThread = thread;
            GetMoviesThread.Start(movieName);
        }

        /// <summary>
        /// Creation of Discord saving thread and starts it.
        /// </summary>
        public void CreateSaveDiscordThread()
        {
            //Creation of thread and setting of thread variables
            Thread thread = new Thread(new ThreadStart(SaveDiscord));
            thread.Name = "Save Discord Thread";
            thread.Priority = ThreadPriority.Highest;

            //Aborting thread if one already exists
            if (DiscordSaveThread != null)
                DiscordSaveThread.Abort();

            DiscordSaveThread = null;
            DiscordSaveThread = thread;
            DiscordSaveThread.Start();
        }

        /// <summary>
        /// Creation of Discord loading thread and starts it.
        /// </summary>
        public void CreateLoadDiscordThread()
        {
            //Creation of thread and setting of thread variables
            Thread thread = new Thread(new ThreadStart(LoadDiscord));
            thread.Name = "Load Discord Thread";
            thread.Priority = ThreadPriority.AboveNormal;

            //Aborting thread if one already exists
            if (DiscordLoadThread != null)
                DiscordLoadThread.Abort();

            DiscordLoadThread = null;
            DiscordLoadThread = thread;
            DiscordLoadThread.Start();
        }

        /// <summary>
        /// Creation of event saving thread and starts it.
        /// </summary>
        public void CreateSaveEventThread()
        {
            //Creation of thread and setting of thread variables
            Thread thread = new Thread(new ThreadStart(SaveEvent));
            thread.Name = "Save Event Thread";
            thread.Priority = ThreadPriority.Highest;

            //Aborting thread if one already exists
            if (EventSaveThread != null)
                EventSaveThread.Abort();

            EventSaveThread = null;
            EventSaveThread = thread;
            EventSaveThread.Start();
        }

        /// <summary>
        /// Creation of event loading thread and starts it.
        /// </summary>
        public void CreateLoadEventThread()
        {
            //Creation of thread and setting of thread variables
            Thread thread = new Thread(new ThreadStart(LoadEvent));
            thread.Name = "Load Event Thread";
            thread.Priority = ThreadPriority.AboveNormal;

            //Aborting thread if one already exists
            if (EventLoadThread != null)
                EventLoadThread.Abort();

            EventLoadThread = null;
            EventLoadThread = thread;
            EventLoadThread.Start();
        }

        #endregion


        #region Record Persistance

        /// <summary>
        /// Utilises Isolated storage to save the discord record runtime list and saves it to the records.txt file in the userData directory.
        /// </summary>
        public void SaveDiscord()
        {
            try
            {
                //Accesses the Isolated Storage File records.txt and opens up a stream to be utilised
                using (IsolatedStorageFileStream isoStream = new IsolatedStorageFileStream(Path.Combine(userDataDir, discordFileName), FileMode.OpenOrCreate, FileAccess.ReadWrite, isoFile))
                {
                    //Accquires lock on Discord Records and serializes the data into the isolated storage file steam and closes the stream, thus saving the data
                    BinaryFormatter formatter = new BinaryFormatter();
                    if (Monitor.TryEnter(DiscordRecords,3000))
                    {
                        formatter.Serialize(isoStream, DiscordRecords);
                        Monitor.Exit(DiscordRecords);
                    }
                    isoStream.Close();

                    MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.TxtBlkThreadInfo.Text = string.Concat("> ", Thread.CurrentThread.Name, " - Save Discord has retrieved records."));


                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message,"Exception Occured");
                MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.TxtBlkThreadInfo.Text = string.Concat("> ", Thread.CurrentThread.Name, " - Save Discord has been interrupted."));
            }
        }

        /// <summary>
        /// Utilises Isolated storage to load the discord record to runtime list from the records.txt file in the userData directory.
        /// </summary>
        public void LoadDiscord()
        {
            try
            {
                //If the save thread is set and alive then the load thread is joined and waiting for it to complete saving
                if(DiscordSaveThread != null)
                    if (DiscordSaveThread.IsAlive)
                    { DiscordSaveThread.Join(); }

                //Accesses the Isolated Storage File records.txt and opens up a stream to be utilised
                using (IsolatedStorageFileStream isoStream = new IsolatedStorageFileStream(Path.Combine(userDataDir, discordFileName), FileMode.OpenOrCreate, FileAccess.ReadWrite, isoFile))
                {

                    //If the ISO stream is empty then just dont carry out the load logic
                    if (isoStream.Length > 0)
                    {
                        BinaryFormatter formatter = new BinaryFormatter();
                        if (Monitor.TryEnter(DiscordRecords,3000))
                        {
                            //Clear the discord record list
                            DiscordRecords.Clear();

                            //Adds discord records to list using AddRange as using "=" would release the lock on the object
                            DiscordRecords.AddRange((List<DiscordRecord>)formatter.Deserialize(isoStream));

                            //Populates the discord list box
                            MainWindow.Instance.Dispatcher.Invoke(() =>
                            {
                                MainWindow.Instance.LstBxDiscord.ItemsSource = DiscordRecords;
                                MainWindow.Instance.LstBxDiscord.Items.Refresh();
                            });

                            Monitor.Exit(DiscordRecords);
                        }
                        isoStream.Close();

                        MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.TxtBlkThreadInfo.Text = string.Concat("> ", Thread.CurrentThread.Name, " - Load Discord has retrieved records."));
                    }
                    else
                    {
                        MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.TxtBlkThreadInfo.Text = string.Concat("> ", Thread.CurrentThread.Name, " - Discord ISO stream is empty, cannot deserialize."));
                    }
                }

            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message,"Exception Occured");
                MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.TxtBlkThreadInfo.Text = string.Concat("> ", Thread.CurrentThread.Name, " - Load Discord has been interrupted."));
            }
        }

        /// <summary>
        /// Utilises Isolated storage to save the event record runtime list and saves it to the records.txt file in the userData directory.
        /// </summary>
        public void SaveEvent()
        {
            try
            {

                //Accesses the Isolated Storage File events.txt and opens up a stream to be utilised
                using (IsolatedStorageFileStream isoStream = new IsolatedStorageFileStream(Path.Combine(userDataDir, eventsFileName), FileMode.OpenOrCreate, FileAccess.ReadWrite, isoFile))
                {
                    //Accquires lock on events Records and serializes the data into the isolated storage file steam and closes the stream, thus saving the data
                    BinaryFormatter formatter = new BinaryFormatter();
                    if (Monitor.TryEnter(EventRecords,3000))
                    {
                        formatter.Serialize(isoStream, EventRecords);
                        Monitor.Exit(EventRecords);
                    }

                    isoStream.Close();
                    MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.TxtBlkThreadInfo.Text = string.Concat("> ", Thread.CurrentThread.Name, " - Save Event completed successfully."));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message,"Exception Occured");
                MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.TxtBlkThreadInfo.Text = string.Concat("> ", Thread.CurrentThread.Name, " - Save Event has been interrupted."));
            }
        }

        /// <summary>
        /// Utilises Isolated storage to load the event record to runtime list from the records.txt file in the userData directory.
        /// </summary>
        public void LoadEvent()
        {
            try
            {
                //If the save thread is set and alive then the load thread is joined and waiting for it to complete saving
                if (EventSaveThread != null)
                    if (EventSaveThread.IsAlive)
                        EventSaveThread.Join();

                //Accesses the Isolated Storage File events.txt and opens up a stream to be utilised
                using (IsolatedStorageFileStream isoStream = new IsolatedStorageFileStream(Path.Combine(userDataDir, eventsFileName), FileMode.OpenOrCreate, FileAccess.ReadWrite, isoFile))
                {
                    //If the ISO stream is empty then just dont carry out the load logic
                    if (isoStream.Length > 0)
                    {
                        BinaryFormatter formatter = new BinaryFormatter();
                        if (Monitor.TryEnter(EventRecords,3000))
                        {
                            //Clear the event record list
                            EventRecords.Clear();

                            //Adds event records to list using AddRange as using "=" would release the lock on the object
                            EventRecords.AddRange((List<EventRecord>)formatter.Deserialize(isoStream));

                            //Populates the event list box
                            MainWindow.Instance.Dispatcher.Invoke(() =>
                            {
                                MainWindow.Instance.LstBxEvents.ItemsSource = EventRecords;
                                MainWindow.Instance.LstBxEvents.Items.Refresh();
                            });

                            MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.UpdateEventStatus());
                            Monitor.Exit(EventRecords);
                        }

                        isoStream.Close();
                        MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.TxtBlkThreadInfo.Text = string.Concat("> ", Thread.CurrentThread.Name, " - Event loading has completed successfully."));
                    }
                    else
                    {
                        MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.TxtBlkThreadInfo.Text = string.Concat("> ", Thread.CurrentThread.Name, " - Event ISO stream is empty, cannot deserialize."));
                    }
                }


            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message,"Exception Occured");
                MainWindow.Instance.Dispatcher.Invoke(() => MainWindow.Instance.TxtBlkThreadInfo.Text = string.Concat("> ", Thread.CurrentThread.Name, " - Load Event has been interrupted."));
            }
        }


        #endregion

        /// <summary>
        /// Checks if get movie thread is alive
        /// </summary>
        public bool IsGetMovieActive()
        {
            if (GetMoviesThread != null)
                return GetMoviesThread.IsAlive;
            else
                return false;
        }
    }

    /// <summary>
    /// Ghibli API Data Transfer Object
    /// </summary>
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

        //Include method to set variables form moviefile object
    }

    /// <summary>
    /// A record object for a discord channel, this includes the server name and webhook URL for the desired discord channel.
    /// </summary>
    [Serializable]
    public class DiscordRecord
    {
        public string ServerName;
        public string WebhookURL;

        public DiscordRecord() { }

        public DiscordRecord(string serverName, string webhookURL)
        {
            ServerName = serverName;
            WebhookURL = webhookURL;
        }

        public override string ToString()
        {
            return ServerName;
        }

        public string DiscordInfo { get { return ServerName; } }
    }

    /// <summary>
    /// A record object for a movie event, this includes info such as the title and Sate the event will occur.
    /// </summary>
    [Serializable]
    public class EventRecord
    {
        public string MovieTitle { get; set; }
        public DateTime Date { get; set; }

        public EventRecord() { }

        public EventRecord(string movieTitle,DateTime date)
        {
            MovieTitle = movieTitle;
            Date = date;
        }

        public string MovieInfo { get { return string.Concat(MovieTitle, " - ", Date.Date.ToShortDateString()); } }
    }

    /// <summary>
    /// Input data for Set Movie Thread
    /// </summary>
    public class SetMovieRequest
    {
        public string JsonData;
        public CancellationToken Token;

        public SetMovieRequest () {}

        public SetMovieRequest(string json,CancellationToken ct)
        {
            JsonData = json;
            Token = ct;
        }
    }
}
