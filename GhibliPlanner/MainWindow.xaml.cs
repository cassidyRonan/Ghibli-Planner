using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace GhibliPlanner
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //Instance for singular design pattern (Utilised in dispatcher invoking in Ghibli Core)
        public static MainWindow Instance;

        //Backend Code & Thread Handling Class
        public Ghibli Core = new Ghibli();

        //Cancellation Token for cancelling of retrieve films thread
        CancellationTokenSource RetrieveFilmCancel;

        //Startup Logic
        public MainWindow()
        {
            InitializeComponent();
            Instance = this;

            //Initial retrieval of films with passing in of cancellation token
            Core.RetrieveFilms(ref RetrieveFilmCancel);

            //Creation and startup of Discord and Event loading threads
            Core.CreateLoadDiscordThread();
            Core.CreateLoadEventThread();
        }

        //GENERAL UI EVENTS
        
        /// <summary>
        /// Button event for status bar cancel button; invokes the cancel method on the CancellationTokenSource.
        /// </summary>
        private void BtnCancelProgOperation_Click(object sender, RoutedEventArgs e)
        {
            //Invokes the cancel method to notify the RetrieveFilms of the cancellation similar to how the BackgroundWorker is cancelled.
            RetrieveFilmCancel.Cancel();
        }

        /// <summary>
        /// Event fired off when tab control selection is changed.
        /// </summary>
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //Check to ensure that the source of the event is a TabControl
            if (e.Source.GetType() == typeof(TabControl))
            {
                switch ((e.Source as TabControl).SelectedIndex)
                {
                    case 0: //Movie Info Tab
                        break;

                    case 1: //Scheduled Events Tab
                        //ComboBox item setting
                        CmbBxDiscord.ItemsSource = Core.DiscordRecords;
                        CmbBxDiscord.Items.Refresh();
                        CmbBxDiscord.SelectedIndex = 0;

                        //Listbox item population
                        LstBxEvents.ItemsSource = Core.EventRecords;
                        LstBxEvents.Items.Refresh();
                        break;

                    case 2: //Discord Records Tab
                        //Listbox item population
                        LstBxDiscord.ItemsSource = Core.DiscordRecords;
                        LstBxDiscord.Items.Refresh();
                        break;

                    default:
                        break;
                }
            }
        }


        //MOVIE INFO UI EVENTS
        /// <summary>
        /// Event fired off when selection is changed on Ghibli Movie List Box
        /// </summary>
        private void LstBxGhibliMovies_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //Checks if get movie thread is alive
            if (!Core.IsGetMovieActive())
            {
                //Takes movie title and creates then starts thread to retrieve movie details
                Core.CreateGetFilm(LstBxGhibliMovies.SelectedItem.ToString());
            }
        }

        /// <summary>
        /// Button event that triggers the retrieval of films and repopulates the list
        /// </summary>
        private void BtnRefreshList_Click(object sender, RoutedEventArgs e)
        {
            Core.RetrieveFilms(ref RetrieveFilmCancel);
        }

        /// <summary>
        /// Button event for the create event on the Main UI Page, takes film choice and selected date to create an event object.
        /// </summary>
        private void BtnCreateEvent_Click(object sender, RoutedEventArgs e)
        {
            //Checks that a date and movie have been selected and the combination does not already exist
            if(DtPck.SelectedDate != null && Core.RetrievedMovie != null && !Core.EventRecords.Contains(new EventRecord(Core.RetrievedMovie.title, DtPck.SelectedDate.Value)))
            {
                EventRecord eventRecord = new EventRecord(Core.RetrievedMovie.title, DtPck.SelectedDate.Value);

                //Adds the created event to the runtime list in the Ghibli class
                Core.EventRecords.Add(eventRecord);

                //Refresh the event list to show the new item
                LstBxEvents.Items.Refresh();
            }
        }


        //SCHEDULED EVENTS UI EVENTS

        /// <summary>
        /// Button event that triggers the removal of the event record present in the listbox item.
        /// </summary>
        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            //Get sender object as button
            Button button = sender as Button;

            //Get datacontext from listbox item entry via the button attached in its item template and remove the entry
            Core.EventRecords.Remove(button.DataContext as EventRecord);

            //Refresh list to show up to date entries in runtime list in Ghibli Core
            LstBxEvents.Items.Refresh();
        }

        /// <summary>
        /// Button event that triggers the sending of a reminder message to the selected discord channel using the selected event record from the event listbox.
        /// </summary>
        private void BtnSendReminder_Click(object sender, RoutedEventArgs e)
        {
            if (LstBxEvents.SelectedItem != null && CmbBxDiscord.SelectedItem != null)
            {
                //Get event record from list box and cast as EventRecord
                EventRecord er = (LstBxEvents.SelectedItem as EventRecord);

                //Send event reminder with optional message from TxtBxDiscordMsg to Discord Webhook URL as POST request
                GhibliHelper.SendToWebHook((CmbBxDiscord.SelectedItem as DiscordRecord).WebhookURL, string.Concat("Movie: ", er.MovieTitle, "\nDate: ", er.Date.ToShortDateString(), "\n", TxtBxDiscordMsg.Text), "Ghibli Planner v0.1");
            }
        }

        /// <summary>
        /// Button event that triggers the creation and starting of the Save Events Thread.
        /// </summary>
        private void BtnSaveEventLists_Click(object sender, RoutedEventArgs e)
        {
            Core.CreateSaveEventThread();
        }

        /// <summary>
        /// Button event that triggers the creation and starting of the load event data thread.
        /// </summary>
        private void BtnLoadEventLists_Click(object sender, RoutedEventArgs e)
        {
            Core.CreateLoadEventThread();
            
        }


        //DISCORD RECORDS UI EVENTS

        /// <summary>
        /// Button event that triggers the creation of a discord record object and saves it to the runtime list in the Ghibli class.
        /// </summary>
        private void BtnSaveDiscord_Click(object sender, RoutedEventArgs e)
        {
            //Check to ensure that both fields have been populated
            if(!string.IsNullOrWhiteSpace(TxtBxServerName.Text) && !string.IsNullOrWhiteSpace(TxtBxWebhookURL.Text))
            {
                //Creation of discord record from field input
                DiscordRecord discRec = new DiscordRecord(TxtBxServerName.Text, TxtBxWebhookURL.Text);
                //Addition of record to runtime list
                Core.DiscordRecords.Add(discRec);

                //Refresh of record list
                LstBxDiscord.ItemsSource = Core.DiscordRecords;
                LstBxDiscord.Items.Refresh();
            }
        }

        /// <summary>
        /// Button event that clears the discord record input fields.
        /// </summary>
        private void BtnClearFields_Click(object sender, RoutedEventArgs e)
        {
            TxtBxServerName.Text = "";
            TxtBxWebhookURL.Text = "";
        }

        /// <summary>
        /// Button event that triggers the removal of the attached DiscordRecord entry from the runtime list.
        /// </summary>
        private void RemoveDiscordButton_Click(object sender, RoutedEventArgs e)
        {
            //Get sending object as button
            Button button = sender as Button;

            //Remove discord record from the runtime list via the datacontext of the button similar to remove button for events
            Core.DiscordRecords.Remove(button.DataContext as DiscordRecord);

            //Refreshing of runtime list
            LstBxDiscord.Items.Refresh();
        }

        /// <summary>
        /// Button event that triggers the creation and starting of the discord record saving thread.
        /// </summary>
        private void BtnSaveDiscordList_Click(object sender, RoutedEventArgs e)
        {
            Core.CreateSaveDiscordThread();
        }

        /// <summary>
        /// Button event that triggers the creation and starting of discord record load thread and updates the list box.
        /// </summary>
        private void BtnLoadDiscordList_Click(object sender, RoutedEventArgs e)
        {
            Core.CreateLoadDiscordThread();
            LstBxDiscord.Items.Refresh();
        }


        //EXTRA FUNCTIONALITY

        /// <summary>
        /// Checks if event records are present in the runtime list and if so, displays details on status bar. Otherwise leaves blank.
        /// </summary>
        public void UpdateEventStatus()
        {
            //Check if runtime list has any event records present
            if (Core.EventRecords.Count > 0)
            {
                //Order event records by date to get the next event due
                EventRecord eventRec = Core.EventRecords.OrderBy(e => e.Date).ToList()[0];
                TxtBlkEventStatus.Text = string.Concat("Next Event: ", eventRec.MovieTitle, " - ", eventRec.Date.ToShortDateString());
            }
            else
            {
                TxtBlkEventStatus.Text = "No events have been loaded or created. ";
            }
        }

    }
}
