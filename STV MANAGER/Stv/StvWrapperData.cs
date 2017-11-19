using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.ComponentModel;
using System.Xml;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using STVM.Stv.ApiV3;
//using STVM.Stv.Api;
using STVM.Data;

namespace STVM.Stv.Data

{
    [DataContract (Namespace="")]
    public class tTelecast
    {
        public TelecastUpdateEventHandler TelecastUpdateEvent;

        [DataMember]
        public int ID { get; set; }
        [DataMember]
        public string Title { get; set; }
        [DataMember]
        public string SubTitle { get; set; }
        [DataMember]
        public DateTime StartDate { get; set; }
        [DataMember]
        public DateTime EndDate { get; set; }
        [DataMember]
        public Categories Category  { get; set; }
        [DataMember]
        public string PublicText { get; set; }
        [DataMember]
        public string ImageURL;
        [DataMember]
        public string Subject { get; set; }
        [DataMember]
        public string TVStation { get; set; }
        [DataMember]
        public int Season;
        [DataMember]
        public int Episode;
        [DataMember]
        public int Year;
        [DataMember]
        public string Country;
        [DataMember]
        public int tmdbMovieID;
        [DataMember]
        public int tvdbShowID;
        [DataMember]
        public int tvdbEpisodeID;

        [DataMember(Name = "Status")]
        private TelecastStatus status;
        public TelecastStatus Status
        {
            get { return status; }
            set
            {
                if (value != status)
                {
                    status = value;
                    if (TelecastUpdateEvent != null) { TelecastUpdateEvent(new TelecastUpdateEventArgs(this, TelecastUpdateEvents.StatusChanged)); }
                }
            }
        }
        
        [DataMember]
        public bool Duplicate;
        [DataMember]
        public int FirstAiredID;
        [DataMember]
        public bool AdFree;
        [DataMember]
        public bool hasHD;
        [DataMember]
        public bool hasMobile;
        [DataMember]
        public bool suspectBroken;
        [DataMember]
        public bool recordManual;
        [DataMember]
        public bool recordChannel;

        public tTelecast()
        {
            ID = 0;
            Title = "";
            SubTitle = "";
            StartDate = DateTime.Now;
            EndDate = DateTime.Now;
            Category = Categories.Unknown;
            PublicText = "";
            ImageURL = "";
            Subject = "";
            TVStation = "";
            Season = 0;
            Episode = 0;
            tvdbShowID = 0;
            tvdbEpisodeID = 0;
            Status = TelecastStatus.Unknown;
            Duplicate = false;
            FirstAiredID = 0;
            AdFree = false;
            hasHD = false;
            hasMobile = false;
            suspectBroken = false;
            recordManual = false;
            recordChannel = false;
        }

        public tTelecast(Telecast item)
            : this()
        {
            ID = item.id;
            StartDate = item.startDateLocal;
            EndDate = item.endDateLocal;
            TVStation = item.tvStation.name; 
            switch(item.tvCategory.id)
            {
                case 1:
                    Category = Categories.Movie;
                    break;
                case 2:
                    Category = Categories.Series;
                    break;
                case 3:
                case 4:
                case 7:
                    Category = Categories.Other;
                    break;
                case 6: Category = Categories.Info;
                    break;
                default:
                    Category = Categories.Unknown;
                    break;
            }
            Title = item.title;
            SubTitle = item.subTitle;
            Subject = item.subject;
            ImageURL = item.imageUrl500;
            PublicText = item.description;

            tEpisodeCode episode;
            if (tEpisodeCode.TryParse(item.episode, out episode))
            {
                Season = episode.Season;
                Episode = episode.Episode;
            }
            Year = item.year;
            Country = item.country;
        }

        public override string ToString()
        {
            return ID.ToString() + "," + Title;
        }

        public override bool Equals(object obj)
        {
            tTelecast compareItem = obj as tTelecast;

            return compareItem.ID == this.ID;
        }

        public override int GetHashCode()
        {
            return this.ID;
        }

        public bool IsLocalAvailable()
        {
            return (this.Status == TelecastStatus.DownloadFinished | 
                this.Status == TelecastStatus.DownloadRenamed | 
                this.Status == TelecastStatus.InLocalArchive);
        }

        public ListViewItem ToListViewItem()
        {
            ListViewItem result = new ListViewItem(this.Title);
            string subTitle = (this.Subject == "") ? this.SubTitle : this.Subject;
            result.SubItems.Add(subTitle).Name = "SubTitle";
            string episode;
            if (this.Episode > 0)
            {
                tEpisode Episode = new tEpisode()
                {
                    Season = this.Season,
                    Episode = this.Episode
                };
                episode = Episode.EpisodeCode();
            }
            else episode = "";
            result.SubItems.Add(episode).Name = "Year/Episode";
            result.SubItems.Add(this.Category.ToDescription()).Name = "Category";
            string startDate = this.StartDate.ToString("dd.MM.yyyy HH:mm") + " (" + (this.EndDate - this.StartDate).TotalMinutes.ToString() + "')";
            result.SubItems.Add(startDate).Name = "Startdate";
            result.SubItems.Add(this.TVStation).Name = "TVStation";
            string Hd = this.hasHD ? "HD" : "";
            result.SubItems.Add(Hd).Name = "HD";
            result.SubItems.Add(this.Status.ToDescription()).Name = "Status";
            string adFree = this.AdFree ? "verfügbar" : "nein";
            result.SubItems.Add(adFree).Name = "AdFree";
            result.Tag = this;

            return result;
        }

        public tAiring Airing
        {
            get { return new tAiring { StartDate = this.StartDate, EndDate = this.EndDate }; }
        }

        public string Duration
        {
            get
            {
                double duration = (this.EndDate - this.StartDate).TotalMinutes;
                return String.Format("{0:0}'", duration);
            }
        }
    }

    public enum TelecastUpdateEvents
    {
        StatusChanged
    }

    public delegate void TelecastUpdateEventHandler(TelecastUpdateEventArgs e);
    public class TelecastUpdateEventArgs : EventArgs
    {
        public readonly tTelecast Telecast;
        public readonly TelecastUpdateEvents UpdateEvent;
        public TelecastUpdateEventArgs(tTelecast updateTelecast, TelecastUpdateEvents updateEvent)
        {
            Telecast = updateTelecast;
            UpdateEvent = updateEvent;
        }
    }

    [CollectionDataContract(Name = "Telecasts", ItemName = "Telecast", Namespace = "")]
    public class tTelecastCollection : List<tTelecast>
    {
        public tTelecastCollection()
        { }

        public tTelecastCollection(IEnumerable<tTelecast> items)
            : base(items)
        { }

        public IEnumerable<int> TelecastIDs
        {
            get { return this.Select(telecast => telecast.ID); }
        }

        public IEnumerable<string> Titles
        {
            get { return this.Select(telecast => telecast.Title).Distinct().OrderBy(title => title); }
        }

        public IEnumerable<string> TitlesByLastTelecast
        {
            get { return this.OrderBy(telecast => telecast.StartDate).Reverse().Select(telecast => telecast.Title).Distinct(); }
        }

        public IEnumerable<DateTime> Dates
        {
            get { return this.Select(telecast => telecast.StartDate.Date).Distinct().OrderBy(date => date).Reverse(); }
        }

        public tTelecast GetById(int TelecastID)
        {
            return this.Find(telecast => telecast.ID == TelecastID);
        }

        public IEnumerable<tTelecast> GetById(IEnumerable<int> TelecastIds)
        {
            List<tTelecast> result = new List<tTelecast>();
            foreach(int TelecastId in TelecastIds)
            {
                result.Add(this.GetById(TelecastId));
            }
            return result;
        }

        public tTelecastCollection GetByTitle(string Title)
        {
            return new tTelecastCollection(this.Where(telecast => telecast.Title == Title));
        }

        public tTelecastCollection GetByDate(DateTime Date)
        {
            return new tTelecastCollection(this.Where(telecast => telecast.StartDate.Date == Date.Date));
        }

        public tTelecastCollection GetByCategory(Categories Category)
        {
            return new tTelecastCollection(this.Where(telecast => telecast.Category == Category));
        }

        public bool Contains(Categories Category)
        {
            return this.Any(telecast => telecast.Category == Category);
        }

        public bool Contains(int TelecastID)
        {
            return this.Any(telecast => telecast.ID == TelecastID);
        }

        public void Remove(int TelecastID)
        {
            this.RemoveAll(telecast => telecast.ID == TelecastID);
        }

        public void RemoveRange(IEnumerable<tTelecast> items)
        {
            foreach(tTelecast item in items)
            {
                this.Remove(item.ID);
            }
        }

        public void RemoveBefore(DateTime StartDate)
        {
            this.RemoveAll(telecast => telecast.StartDate < StartDate);
        }

        //public new void Add(tTelecast item)
        //{
        //    this.Remove(item.ID);
        //    base.Add(item);
        //}

        public void AddOrRefreshRange(IEnumerable<tTelecast> items)
        {
            foreach (tTelecast item in items)
            {
                this.Remove(item.ID);
            }
            base.AddRange(items);
        }

        /// <summary>
        /// Suche alle Wiederholungen einer Sendung
        /// </summary>
        /// <param name="item">Erste Ausstrahlung</param>
        /// <returns>Liste aller Wiederholungen inklusive der ersten Ausstrahlung</returns>
        public tTelecastCollection Duplicates(tTelecast item)
        {
            tTelecastCollection result = new tTelecastCollection();
            result.AddRange(this.Where(tc => tc.FirstAiredID == item.ID));
            return result;
        }
    }

    public class tTVStation
    {
        private TvStation _station;
        public int ID { get { return _station.id; } }
        public string Name { get { return _station.name; } }
        public string LogoUrlSmall { get { return _station.smallLogoUrl; } }
        public string LogoUrlLarge { get { return _station.largeLogoUrl; } }
        public bool IsRecordable { get { return _station.isRecordable; } }
        public Image LogoSmall;

        public tTVStation()
        {
            LogoSmall = null;
        }

        public tTVStation(TvStation Station)
            : this()
        {
            _station = Station;
            LoadImageAsync();
        }

        private async Task LoadImageAsync()
        {
            System.Net.WebClient client = new System.Net.WebClient();
            byte[] bitmapData = await client.DownloadDataTaskAsync(new Uri(LogoUrlSmall));
            MemoryStream memoryStream = new MemoryStream(bitmapData);
            LogoSmall = new Bitmap(memoryStream);
        }

        public override string ToString()
        {
            return this.Name;
        }
    }

    public class tTVStationList: List<tTVStation>
    {
        public Image GetLogo(string Name)
        {
            return this.First(station => station.Name == Name).LogoSmall;
        }

        public bool Exists(string TvStationName)
        {
            return this.Any(station => station.Name == TvStationName);
        }
    }

    public enum StvStatusOptions
    {
        [DescriptionAttribute("Offline")]
        Offline,
        [DescriptionAttribute("Warte auf Session ...")]
        SessionWaiting,
        [DescriptionAttribute("Session eröffnet ...")]
        SessionCompleted,
        [DescriptionAttribute("Warte auf Login ...")]
        LoginWaiting,
        [DescriptionAttribute("Login erfolgreich ...")]
        LoginCompleted,
        [DescriptionAttribute("Warte auf Konfiguration ...")]
        ConfigWaiting,
        [DescriptionAttribute("Bereit")]
        Ready,
        [DescriptionAttribute("Server Fehler")]
        ServerError,
        [DescriptionAttribute("Username oder Passwort falsch")]
        LoginError
    }

    public enum RecordingStates
    {
        InVideoArchive = 0,
        WaitingForCutList = 1,
        Programmed = 2
    }

    public enum RecordFormats
    {
        Undefined = 0,
        DivX = 1,
        Mobile = 4,
        SD = 5,
        HD = 6
    }


}
