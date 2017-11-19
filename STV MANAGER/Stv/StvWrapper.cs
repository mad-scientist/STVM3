using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Xml;
using System.IO;
using System.ComponentModel;
using System.Windows.Forms;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using STVM.Data;
using STVM.Stv.Data;
using STVM.Stv.Http;
using STVM.Stv.Favorites;
using STVM.Stv.ApiV3;
using STVM.Download;

namespace STVM.Stv
{

    [DataContract(Name = "STV_Wrapper", Namespace = "")]
    class stvWrapper
    {
        const string stvTimeZoneId = "UTC";
        // const string stvTimeZoneId = "W. Europe Standard Time";
        const int TelecastRequestSize = 500;

        private StvApiV3 stv;

        private stvHTTP stvWeb;
        public tTVStationList TVStations;
        private stvSearchQueue SearchQueue;
        public tFavoriteCollection Favorites;

        private User stvUser;

        public bool CancelAction = false;

        [DataMember]
        public tTelecastCollection Telecasts;
        [DataMember]
        public DateTimeOffset LastUpdate;

        public tTelecastCollection VideoArchive
        {
            get { return new tTelecastCollection(this.Telecasts.Where(telecast => telecast.Status > TelecastStatus.Programmed)); }
            set { this.Telecasts = value; }
        }

        public tTelecastCollection Programmed
        {
            get { return new tTelecastCollection(this.Telecasts.Where(telecast => telecast.Status == TelecastStatus.Programmed)); }
            set { this.Telecasts = value; }
        }

        // convert local time to STV server time (Germany)
        private DateTime stvLastUpdate
        {
            get { return TimeZoneInfo.ConvertTime(LastUpdate, TimeZoneInfo.FindSystemTimeZoneById(stvTimeZoneId)).DateTime; }
        }

        public string Username
        {
            get { return stv.Username; }
            set { stv.Username = value; }
        }
        public string Password
        {
            get { return stv.Password; }
            set { stv.Password = value; }
        }

        private StvStatusOptions stvStatus;
        public StvStatusOptions Status
        {
            get { return stvStatus; }
            set
            {
                if (value != stvStatus)
                {
                    stvStatus = value;
                    ServerEvent(new ServerEventArgs(value));
                }
            }
        }

        public event LogEventHandler LogEvent
        {
            add { stv.LogEvent += value; }
            remove { stv.LogEvent -= value; }
        }

        public event LoginEventHandler LoginEvent;
        public event TaskUpdateEventHandler TaskUpdateEvent;
        public event ServerEventHandler ServerEvent;
        public event VideoArchiveChangedEventHandler VideoArchiveRefreshEvent;
        public event SearchUpdateEventHandler SearchUpdateEvent;
        public event RecordCreateEventHandler RecordCreateEvent;

        private stvWrapper()
        {
            Telecasts = new tTelecastCollection();
            LastUpdate = new DateTimeOffset(DateTime.Now.AddMonths(-2));
        }

        private void Initialize()
        {
            stv = new StvApiV3();
            stvWeb = new stvHTTP();
            stvUser = new User();
            TVStations = new tTVStationList();
            SearchQueue = new stvSearchQueue();
            Username = "";
            Password = "";
        }

        #region XML
        private const string xmlFilename = "StvArchive.xml";
        private string xmlFile;

        private string SettingsPath
        { get { return Path.GetDirectoryName(xmlFile); } }

        private static stvWrapper Deserialize(string xmlFile)
        {
            if (File.Exists(xmlFile))
            {
                try
                {
                    DataContractSerializer serializer = new DataContractSerializer(typeof(stvWrapper));
                    FileStream readFileStream = new FileStream(xmlFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                    stvWrapper result = (stvWrapper)serializer.ReadObject(readFileStream);
                    readFileStream.Close();

                    return result;
                }
                catch (Exception e)
                {
                    MessageBox.Show("Fehler beim Einlesen von " + xmlFile + "\r\nDaten werden zurückgesetzt.",
                        "STV MANAGER", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    stvWrapper result = new stvWrapper();
                    return result;
                }
            }
            else return new stvWrapper();
        }

        public static stvWrapper ReadFromXML(string xmlPath)
        {
            string xmlFile = Path.Combine(xmlPath, xmlFilename);
            stvWrapper result = Deserialize(xmlFile);
            result.Initialize();
            result.Favorites = tFavoriteCollection.ReadFromXML(xmlPath);
            result.xmlFile = xmlFile;
            return result;
        }

        public void SaveToXML()
        {
            DirectoryInfo directory = Directory.CreateDirectory(Path.GetDirectoryName(xmlFile));
            if (directory.Exists)
            {
                DataContractSerializer serializer = new DataContractSerializer(typeof(stvWrapper));
                XmlTextWriter writeFileStream = new XmlTextWriter(xmlFile, null);
                writeFileStream.Formatting = Formatting.Indented;
                serializer.WriteObject(writeFileStream, this);
                writeFileStream.Flush();
                writeFileStream.Close();
            }

            Favorites.SaveToXML();
        }
        #endregion XML

        #region Initialize

        public async Task<bool> Login()
        {
            if (String.IsNullOrEmpty(Username) | String.IsNullOrEmpty(Password))
            {
                Status = StvStatusOptions.LoginError;
                LoginEvent(new LoginEventArgs("Username oder Passwort nicht angegeben"));
                return false;
            }
            else
            {
                Response<User> response = await stv.GetUser();
                if (response.Success)
                {
                    stvUser = response.Data;
                    await GetTvStations();
                    Status = StvStatusOptions.Ready;
                    return true;
                }
                else
                {
                    stvUser = new User();
                    Status = StvStatusOptions.LoginError;
                    LoginEvent(new LoginEventArgs(response.Errors[0].userMessage));
                    return false;
                }
            }
        }

        public async Task<bool> IsPremium()
        {
            if (stvUser.id != "")
            {
                return (stvUser.contract.hasXLPackage | stvUser.contract.hasXXLPackage);
            }
            else return false;
        }

        public async Task<string> RealName()
        {
            if (stvUser.id != "")
            {
                return (stvUser.fullName);
            }
            else return String.Empty;
        }

        private async Task GetTvStations()
        {
            TVStations.Clear();
            Response<List<TvStation>> response = await stv.GetTvStations();
            if (response.Success)
            {
                foreach (TvStation station in response.Data)
                {
                    tTVStation tv = new tTVStation(station);
                    TVStations.Add(tv);
                }
            }
        }

        #endregion Initialize

        #region Downloading

        public async Task GetDownloadFilename(tDownload Download)
        {
            Download.stvDownloadFilename = await stvWeb.GetDownloadFilename(Download.stvDownloadURL);
        }

        public async Task GetDownloadAsync(tDownload Download, SemaphoreSlim semaphore)
        {
            if (!await IsPremium())
            {
                Download.AdFree = false;
            }
            Response<RecordDownloadUrl> response = await stv.GetRecordDownloadUrl(Download.TelecastID, Download.Format, Download.AdFree);
            if (response.Success)
            {
                Download.stvDownloadURL = response.Data.downloadUrl;
                Download.stvStreamingURL = response.Data.streamingUrl;
                Download.stvDownloadFilename = response.Data.fileName;
                Download.Size = response.Data.estimatedFileSize;
                Download.Status = DownloadStatus.UrlReceived;
            }
            else
            {
                Download.Status = DownloadStatus.Error;
            }

            semaphore.Release();
        }

        public async Task<IEnumerable<tDownload>> GetDownloadLinksParallel(IEnumerable<tDownload> Downloads)
        {
            CancelAction = false;
            int Started = 0;
            TaskUpdateEvent(new TaskUpdateEventArgs(TaskUpdateOptions.Download, Started, Downloads.Count()));

            // now let's send HTTP requests to each of these URLs in parallel
            List<Task> allTasks = new List<Task>();
            SemaphoreSlim throttler = new SemaphoreSlim(initialCount: 20);
            foreach (tDownload download in Downloads)
            {
                if (!CancelAction)
                {
                    // do an async wait until we can schedule again
                    await throttler.WaitAsync();

                    allTasks.Add(GetDownloadAsync(download, throttler));
                    Started++;
                    TaskUpdateEvent(new TaskUpdateEventArgs(TaskUpdateOptions.Download, Started, Downloads.Count()));
                }
            }

            // won't get here until all urls have been put into tasks
            await Task.WhenAll(allTasks);

            // won't get here until all tasks have completed in some way
            // (either success or exception)
            TaskUpdateEvent(new TaskUpdateEventArgs(TaskUpdateOptions.Download, -1, 0));
            return Downloads;
        }

        #endregion Downloading

        #region Records

        public async Task DeleteRecords(tTelecastCollection removeTelecasts)
        {
            Response<List<RecordResponse>> response = await stv.DeleteRecord(removeTelecasts.TelecastIDs);
            if (response.Success)
            {
                foreach (int ID in response.Data.Where(record => record.success).Select(record => record.telecastId))
                {
                    this.Telecasts.Remove(ID);
                }
                VideoArchiveRefreshEvent(new VideoArchiveRefreshEventArgs(TelecastsRefreshOptions.TelecastsDeleted, null));
            }
            TaskUpdateEvent(new TaskUpdateEventArgs(TaskUpdateOptions.Delete, -1, 0));
        }

        public async Task CreateRecords(IEnumerable<tTelecast> telecasts)
        {
            int RecordErrorCount = telecasts.Where(telecast => telecast.ID == 0).Count();

            // bereits programmierte Sendungen ausschließen
            IEnumerable<tTelecast> newTelecasts = telecasts.Where(telecast => telecast.ID != 0).Except(this.Programmed);

            int RecordOrderCount = telecasts.Count();
            int RecordNewCount = newTelecasts.Count();

            int RecordExistingCount = RecordOrderCount - RecordNewCount - RecordErrorCount;
            int RecordSuccessCount = 0;

            TaskUpdateEvent(new TaskUpdateEventArgs(TaskUpdateOptions.Recording, 0, RecordOrderCount));

            if (newTelecasts.Any())
            {
                Response<List<RecordResponse>> response = await stv.PostRecord(newTelecasts.Select(telecast => telecast.ID), stvUser.recordBuffer.leadTime, stvUser.recordBuffer.followUpTime);
                if (response.Success)
                {
                    TaskUpdateEvent(new TaskUpdateEventArgs(TaskUpdateOptions.Recording, RecordOrderCount, RecordOrderCount));
                    IEnumerable<int> SuccessIds = response.Data.Where(record => record.success).Select(record => record.telecastId);
                    RecordSuccessCount = SuccessIds.Count();
                    RecordErrorCount += RecordNewCount - RecordSuccessCount;
                    IEnumerable<tTelecast> newProgramming = newTelecasts.Where(telecast => SuccessIds.Contains(telecast.ID));
                    foreach (tTelecast telecast in newProgramming) { telecast.Status = TelecastStatus.Programmed; }
                    this.Telecasts.AddOrRefreshRange(newProgramming);
                    VideoArchiveRefreshEvent(new VideoArchiveRefreshEventArgs(TelecastsRefreshOptions.TelecastsAdded, newProgramming));
                }
                else
                {
                    RecordCreateEvent(new RecordCreateEventArgs(0, RecordExistingCount, RecordNewCount));
                }
            }
            //LogEvent(new LogEventArgs("CreateRecord Success: " + RecordSuccessCount.ToString() + " Existing: " + RecordExistingCount.ToString() + " Error: " + RecordErrorCount.ToString()));
            RecordCreateEvent(new RecordCreateEventArgs(RecordSuccessCount, RecordExistingCount, RecordErrorCount));
            TaskUpdateEvent(new TaskUpdateEventArgs(TaskUpdateOptions.Recording, -1, 0));
        }


        #endregion Records

        #region Updating

        public void Refresh(bool videoArchive, bool programming)
        {
            Telecasts.Clear();
            LastUpdate = new DateTimeOffset(DateTime.Now.AddMonths(-1));
            VideoArchiveRefreshEvent(new VideoArchiveRefreshEventArgs(TelecastsRefreshOptions.TelecastsDeleted, null));
            Update(videoArchive, programming);
        }

        public async Task Update(bool videoArchive, bool programming)
        {
            List<RecordStates> recordStates = new List<RecordStates>();

            if (programming) { recordStates.Add(RecordStates.Programmed); }
            if (videoArchive) { recordStates.Add(RecordStates.InArchive); }
            IEnumerable<tTelecast> archiveUpdate = await GetAllTelecasts(recordStates);
            DateTime Oldest = archiveUpdate.Select(telecast => telecast.StartDate).Min();
            Telecasts.RemoveBefore(Oldest);
            Telecasts.AddOrRefreshRange(archiveUpdate);

            LastUpdate = DateTimeOffset.Now;
            SaveToXML();
            VideoArchiveRefreshEvent(new VideoArchiveRefreshEventArgs(TelecastsRefreshOptions.Finished, null));
        }

        private async Task<IEnumerable<tTelecast>> GetAllTelecasts(IEnumerable<RecordStates> recordStates)
        {
            List<tTelecast> result = new List<tTelecast>();
            int count = await GetTelecastCount(recordStates);
            TaskUpdateEvent(new TaskUpdateEventArgs(TaskUpdateOptions.VideoArchive, 0, count));

            for (int i = 0; i < count; i += TelecastRequestSize)
            {
                result.AddRange(await GetTelecasts(recordStates, i, TelecastRequestSize));
                TaskUpdateEvent(new TaskUpdateEventArgs(TaskUpdateOptions.VideoArchive, result.Count, count));
            }
            TaskUpdateEvent(new TaskUpdateEventArgs(TaskUpdateOptions.VideoArchive, -1, 0));
            return result;
        }

        private async Task<IEnumerable<tTelecast>> GetTelecasts(IEnumerable<RecordStates> recordStates, int from, int count)
        {
            List<tTelecast> result = new List<tTelecast>();

            Response<List<Record>> response = await stv.GetRecords(recordStates, from, count);
            if (response.Success)
            {
                foreach (Record record in response.Data)
                {
                    tTelecast telecast = new tTelecast(record.telecast);
                    telecast.AdFree = record.adFreeAvailable;
                    telecast.hasHD = record.formats.Exists(format => format.recordFormat.id == (int)RecordFormats.HD);
                    telecast.hasMobile = record.formats.Exists(format => format.recordFormat.id == (int)RecordFormats.Mobile);
                    telecast.suspectBroken =
                        record.telecast.title.Contains("- Programmänderung!") |
                        record.telecast.title.Contains("- kurzfristige Programmänderung") |
                        record.telecast.title.Contains("- Sendung entfällt");
                    telecast.recordChannel = record.channels.Any();
                    telecast.recordManual = record.tags.Any(tag => tag.key == "record:manual");
                    switch ((RecordStateIds)record.formats.First().recordState.id)
                    {
                        case RecordStateIds.Programmed:
                            telecast.Status = TelecastStatus.Programmed;
                            break;
                        case RecordStateIds.InArchive:
                            telecast.Status = TelecastStatus.OnStvServer;
                            break;
                    }

                    result.Add(telecast);
                }
            }
            return result;
        }

        public async Task<int> GetTelecastCount(IEnumerable<RecordStates> recordStates)
        {
            Response<RecordCount> response = await stv.GetRecordCount(recordStates);
            if (response.Success)
            {
                return response.Data.count;
            }
            else
            {
                return 0;
            }
        }

        public async Task<tTelecast> GetDetails(tTelecast Telecast)
        {
            Response<Telecast> response = await stv.GetTelecastDetail(Telecast.ID);
            if (response.Success)
            {
                return new tTelecast(response.Data);
            }
            else return new tTelecast();
        }

        #endregion

        #region Search

        public enum QueryStatus
        {
            Queued,
            Active,
            Finished
        }

        private class tSearchQuery
        {
            public tFilter Filter;
            public tTelecastCollection Results;
            public QueryStatus Status;

            public tSearchQuery()
            {
                Filter = new tFilter();
                Results = new tTelecastCollection();
                Status = QueryStatus.Queued;
            }
        }

        private class stvSearchQueue
        {
            private tTelecastCollection Telecasts;
            private List<tSearchQuery> Queries;

            private const int MaximumConnections = 10;

            public stvSearchQueue()
            {
                Telecasts = new tTelecastCollection();
                Queries = new List<tSearchQuery>();
            }

            public void Clear()
            {
                Telecasts.Clear();
                Queries.Clear();
            }

            public int ActiveCount
            {
                get { return Queries.Count(q => q.Status == QueryStatus.Active); }
            }

            public int FinishedCount
            {
                get { return Queries.Count(q => q.Status == QueryStatus.Finished); }
            }

            public int TotalCount
            {
                get { return (this.Queries.Count); }
            }

            public bool IsEmpty
            {
                get { return this.Queries.Count(q => q.Status == QueryStatus.Queued) == 0; }
            }

            public bool IsFinished
            {
                get { return (this.FinishedCount == this.TotalCount); }
            }

            public bool IsWaiting
            {
                get { return (!IsEmpty & (ActiveCount < MaximumConnections)); }
            }

            public tSearchQuery GetNext()
            {
                tSearchQuery nextQuery = Queries.First(q => q.Status == QueryStatus.Queued);
                nextQuery.Status = QueryStatus.Active;
                return nextQuery;
            }

            private void AddQuery(tFilter newFilter)
            {
                Queries.Add(new tSearchQuery { Filter = newFilter });
            }

            public void AddSearch(tFilter Filter)
            {
                // Suche nach Volltext
                if (Filter.SearchText != "")
                {
                    AddQuery(Filter);
                }

                // Suche nach Sender&Datum ohne Text
                else if (Filter.SearchByTVStation & Filter.SearchByDate)
                {
                    int days = 30 - (Filter.Date - DateTime.Now).Days;
                    int index = 0;
                    DateTime date = Filter.Date;

                    switch (Filter.DateOption)
                    {
                        case SearchDateOptions.SingleDay:
                            AddQuery(Filter);
                            break;

                        case SearchDateOptions.RepeatDaily:
                            AddQuery(Filter.DayBefore());
                            do
                            {
                                tFilter newFilter = Filter.Clone();
                                newFilter.Date = date.AddDays(index);
                                AddQuery(newFilter);
                                index++;
                            } while (index < days);
                            break;

                        case SearchDateOptions.RepeatWeekly:
                            do
                            {
                                tFilter newFilter = Filter.Clone();
                                newFilter.Date = date.AddDays(index);
                                AddQuery(newFilter.DayBefore());
                                AddQuery(newFilter);
                                index += 7;
                            } while (index < days);
                            break;
                    }
                }
            }

            public tTelecastCollection FilterResults(tSearchQuery Query)
            {
                tTelecastCollection result = new tTelecastCollection();
                tFilter filter = Query.Filter;

                foreach (tTelecast item in Query.Results)
                {
                    SearchFulltextOptions option = filter.FulltextOption;
                    string text = filter.SearchText;

                    bool isFulltext = (option == SearchFulltextOptions.Fulltext);
                    bool isInTitle = (
                        option == SearchFulltextOptions.InTitle &&
                        item.Title.Contains(text, StringComparison.CurrentCultureIgnoreCase)
                        );
                    bool isExactTitle = (
                        option == SearchFulltextOptions.ExactTitle &&
                        item.Title.Equals(text, StringComparison.CurrentCultureIgnoreCase)
                        );
                    bool isInTitleSubTitle = (
                        option == SearchFulltextOptions.InTitleSubtitle && (
                            item.Title.Contains(text, StringComparison.CurrentCultureIgnoreCase) |
                            item.SubTitle.Contains(text, StringComparison.CurrentCultureIgnoreCase)
                            )
                        );

                    if (isFulltext | isInTitle | isExactTitle | isInTitleSubTitle)
                    {
                        bool isTVStation = (
                            !filter.SearchByTVStation ||
                            (item.TVStation == filter.TVStation)
                            );

                        bool isSingleDay = (
                            !filter.SearchByDate || (
                                (filter.DateOption == SearchDateOptions.SingleDay) &
                                (item.StartDate.Date == filter.Date)
                                )
                            );

                        bool isWeekly = (
                            !filter.SearchByDate || (
                                (filter.DateOption == SearchDateOptions.RepeatWeekly) &
                                (item.StartDate.DayOfWeek == filter.Date.DayOfWeek)
                                )
                            );

                        bool isDaily = (
                            !filter.SearchByDate || (
                                (filter.DateOption == SearchDateOptions.RepeatDaily) &
                                (item.StartDate.Date >= filter.Date)
                                )
                            );

                        bool isStartTime = (
                            !filter.SearchByDate ||
                            !filter.SearchByStartTime || (
                                (item.StartDate.TimeOfDay >= filter.StartTime1.TimeOfDay &
                                item.StartDate.TimeOfDay <= filter.StartTime2.TimeOfDay)
                                )
                            );

                        if (isTVStation & (isSingleDay | isWeekly | isDaily) & isStartTime)
                        {
                            result.Add(item);
                        }
                    }
                }
                return result;
            }

            public void AddResults(tSearchQuery Query)
            {
                this.Telecasts.AddOrRefreshRange(FilterResults(Query));
            }

            public tTelecastCollection Results
            {
                get { return this.Telecasts; }
            }
        }

        private void QueryNextSearch()
        {
            while (SearchQueue.IsWaiting)
            {
                SubmitSearchQuery(SearchQueue.GetNext());
            }
        }

        private async Task SubmitSearchQuery(tSearchQuery Query)
        {
            if (Query.Filter.SearchText != "")
            {
                Query.Results = new tTelecastCollection(await GetTextSearch(Query.Filter.SearchText, Query.Filter.FulltextOption == SearchFulltextOptions.Fulltext));
            }
            else
            {
                tTVStation tvStation = this.TVStations.Find(tv => tv.Name == Query.Filter.TVStation);
                if (tvStation != null)
                {
                    int dayBefore = Query.Filter.dayBefore ? -1 : 0;
                    Query.Results = new tTelecastCollection(await GetTvStationDaySearch(tvStation.ID, Query.Filter.Date.AddDays(dayBefore).CleanDate(), Query.Filter.useEpgDate));
                }
            }
            SearchQueue.AddResults(Query);
            QueryFinished(Query);
        }

        private async Task<IEnumerable<tTelecast>> GetTextSearch(string SearchText, bool FullText)
        {
            List<tTelecast> result = new List<tTelecast>();

            TelecastQuery telecastQuery = new TelecastQuery()
            {
                searchText = SearchText,
                searchFullText = FullText,
                minStartDate = DateTime.Now,
            };
            Response<List<Telecast>> response = await stv.GetTelecasts(telecastQuery);
            if (response.Success)
            {
                foreach (Telecast item in response.Data)
                {
                    tTelecast telecast = new tTelecast(item);
                    telecast.Status = TelecastStatus.InEPG;
                    result.Add(telecast);
                }
            }
            return result;
        }

        public async Task<IEnumerable<tTelecast>> GetTvStationDaySearch(string TvStation, DateTime date, bool useEpgDate)
        {
            List<tTelecast> result = new List<tTelecast>();

            tTVStation tvStation = this.TVStations.Find(tv => tv.Name == TvStation);
            if (tvStation != null)
            {
                result.AddRange(await GetTvStationDaySearch(tvStation.ID, date, useEpgDate));
            }
            return result;
        }

        public async Task<IEnumerable<tTelecast>> GetTvStationDaySearch(int TvStation, DateTime date, bool useEpgDate)
        {
            List<tTelecast> result = new List<tTelecast>();

            int minAddStartHour = useEpgDate ? 5 : 0;
            int maxAddStartHour = useEpgDate ? 6 : 0;

            TelecastQuery telecastQuery = new TelecastQuery() {
                tvStation = TvStation,
                minStartDate = date.AddHours(minAddStartHour),
                maxStartDate = date.AddDays(1).AddHours(maxAddStartHour)
            };
            Response<List<Telecast>> response = await stv.GetTelecasts(telecastQuery);
            if (response.Success)
            {
                foreach (Telecast item in response.Data)
                {
                    tTelecast telecast = new tTelecast(item);
                    telecast.Status = TelecastStatus.InEPG;
                    if (telecast.EndDate > date.AddHours(maxAddStartHour))
                    {
                        result.Add(telecast);
                    }
                }
            }
            return result;
        }

        private void QueryFinished(tSearchQuery query)
        {
            query.Status = QueryStatus.Finished;
            TaskUpdateEvent(new TaskUpdateEventArgs(TaskUpdateOptions.Search, SearchQueue.FinishedCount, SearchQueue.TotalCount));

            if (SearchQueue.IsFinished | CancelAction)
            {
                if (SearchUpdateEvent != null) { SearchUpdateEvent(new SearchUpdateEventArgs(SearchQueue.Results)); }
                SearchCompleted();
            }
            else
            {
                QueryNextSearch();
            }
        }

        private void SearchCompleted()
        {
            if (TaskUpdateEvent != null) { TaskUpdateEvent(new TaskUpdateEventArgs(TaskUpdateOptions.Search, -1, 0)); }
            SearchQueue.Clear();
        }

        public void AddSearch(tFilterCollection Filters)
        {
            CancelAction = false;
            if (SearchQueue.IsFinished)
            Filters.ForEach(filter => SearchQueue.AddSearch(filter));
            TaskUpdateEvent(new TaskUpdateEventArgs(TaskUpdateOptions.Search, SearchQueue.FinishedCount, SearchQueue.TotalCount));
            QueryNextSearch();
        }

        public void NewSearch(tFilterCollection Filters)
        {
            if (!SearchQueue.IsFinished) SearchCompleted();
            if (Filters.Any()) AddSearch(Filters);
        }

        public void NewSearch(tFilter Filter)
        {
            NewSearch(new tFilterCollection { Filter });
        }

        public async Task<IEnumerable<tTelecast>> GetEPG(DateTime date, int TVStation)
        {
            return await GetTvStationDaySearch(TVStation, date, true);
        }

        #endregion Search

    }

    public enum TaskUpdateOptions
    {
        [DescriptionAttribute("Sendung im Archiv")]
        VideoArchive,
        [DescriptionAttribute("Programmierte Sendung")]
        Programmed,
        [DescriptionAttribute("Aufnahme")]
        Recording,
        [DescriptionAttribute("EPG Suche")]
        Search,
        [DescriptionAttribute("Löschen")]
        Delete,
        [DescriptionAttribute("Download")]
        Download,
        [DescriptionAttribute("Fernsehserien.de Import")]
        Wunschliste
    }

    // event fired when telecast data is downloaded
    public delegate void TaskUpdateEventHandler(TaskUpdateEventArgs e);
    public class TaskUpdateEventArgs : EventArgs
    {
        public readonly TaskUpdateOptions Option;
        public readonly int Current;
        public readonly int Total;
        public TaskUpdateEventArgs(TaskUpdateOptions option, int current, int total)
        {
            Option = option;
            Current = current;
            Total = total;
        }
    }

    public enum TelecastsRefreshOptions
    {
        [DescriptionAttribute("Telecasts hinzugefügt")]
        TelecastsAdded,
        [DescriptionAttribute("Telecasts gelöscht")]
        TelecastsDeleted,
        [DescriptionAttribute("Änderungen abgeschlossen")]
        Finished
    }

    // event fired when telecast data is changed
    public delegate void VideoArchiveChangedEventHandler(VideoArchiveRefreshEventArgs e);
    public class VideoArchiveRefreshEventArgs : EventArgs
    {
        public readonly TelecastsRefreshOptions Change;
        public readonly IEnumerable<tTelecast> Telecasts;

        public VideoArchiveRefreshEventArgs(TelecastsRefreshOptions change, IEnumerable<tTelecast> telecasts)
        {
            Change = change;
            Telecasts = telecasts;
        }
    }

    // event fired when search result is received
    public delegate void SearchUpdateEventHandler(SearchUpdateEventArgs e);
    public class SearchUpdateEventArgs : EventArgs
    {
        public readonly tTelecastCollection Telecasts;

        public SearchUpdateEventArgs(tTelecastCollection telecasts)
        {
            Telecasts = telecasts;
        }
    }

    // event fired when Login credentials are missing
    public delegate void LoginEventHandler(LoginEventArgs e);
    public class LoginEventArgs: EventArgs
    {
        public readonly string Message;
        public LoginEventArgs(string message)
        {
            Message = message;
        }
    }

    // event fired when server status changes
    public delegate void ServerEventHandler(ServerEventArgs e);
    public class ServerEventArgs : EventArgs
    {
        public readonly StvStatusOptions ServerStatus;
        public ServerEventArgs(StvStatusOptions newStatus)
        {
            ServerStatus = newStatus;
        }
    }

    // event fired when records are created
    public delegate void RecordCreateEventHandler(RecordCreateEventArgs e);
    public class RecordCreateEventArgs : EventArgs
    {
        public readonly int SuccessCount;
        public readonly int ExistingCount;
        public readonly int ErrorCount;
        public RecordCreateEventArgs(int successCount, int existingCount, int errorCount)
        {
            SuccessCount = successCount;
            ExistingCount = existingCount;
            ErrorCount = errorCount;
        }
    }


}
