using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using STVM.Dialogs;
using STVM.Data;
using STVM.Helper;
using STVM.Stv;
using STVM.Stv.Data;
using STVM.Stv.Favorites;
using STVM.Wrapper.Local;
using STVM.Wrapper.Tvdb;
using STVM.Wrapper.Tmdb;
using STVM.Wrapper.Xbmc;
using STVM.Wrapper.JDL;
using STVM.Wrapper.Synology;
using STVM.Wrapper.Media;
using STVM.DataProvider.Fernsehserien;
using STVM.SeriesManager;
using STVM.Download;
using STVM.Stv.ApiV3;
using BrightIdeasSoftware;

namespace STVM
{
    public partial class fmMain : Form
    {
        private stvWrapper stv;
        private SettingsWrapper settings;
        private string AppUserFolder;
        private tDownloader downloader;
        private localWrapper local;
        public tTxdbLinkCollection<string> TxdbTitleLinks;
        public tTxdbLinkCollection<int> TxdbTelecastIdLinks;
        public SmManager SeriesManager;
        public List<FsSeries> Fernsehserien;

        public fmMain()
        {
            InitializeComponent();
            lbVersion.Text += Application.ProductVersion;
            AppUserFolder = ApplicationEx.AppDataPath();
            Directory.CreateDirectory(AppUserFolder);

            fmSplash splashScreen = new fmSplash();
            splashScreen.Show();
            Application.DoEvents();

            splashScreen.AddLine("Lade Archiv-Daten ...");
            Application.DoEvents();

            stv = stvWrapper.ReadFromXML(AppUserFolder);
            stv.LogEvent += new LogEventHandler(OnLogEvent);
            stv.LoginEvent += new LoginEventHandler(OnLoginEvent);
            stv.TaskUpdateEvent += new TaskUpdateEventHandler(OnTaskUpdateEvent);
            stv.ServerEvent += new ServerEventHandler(OnServerEvent);
            stv.VideoArchiveRefreshEvent += new VideoArchiveChangedEventHandler(OnVideoArchiveRefreshEvent);
            stv.RecordCreateEvent += new RecordCreateEventHandler(OnRecordCreateEvent);
            foreach (tTelecast telecast in stv.Telecasts)
            {
                telecast.TelecastUpdateEvent += OnTelecastUpdateEvent;
            }

            splashScreen.AddLine("Lade Download-Daten ...");
            Application.DoEvents();

            downloader = tDownloader.ReadFromXML(AppUserFolder);
            downloader.LogEvent += new LogEventHandler(OnLogEvent);
            downloader.DownloadUpdateEvent += OnDownloadUpdateEvent;
            if (downloader.Telecasts.Count == 0) { downloader.Telecasts.AddRange(stv.VideoArchive); }

            splashScreen.AddLine("Lade Lokales Archiv ...");
            Application.DoEvents();
            local = localWrapper.ReadFromXML(AppUserFolder);

            splashScreen.AddLine("Lade Datenbank-Verknüpfungen ...");
            Application.DoEvents();
            TxdbTitleLinks = tTxdbLinkCollection<string>.ReadFromXML(AppUserFolder, "TxdbTitleLinks.xml");
            TxdbTelecastIdLinks = tTxdbLinkCollection<int>.ReadFromXML(AppUserFolder, "TxdbTelecastIdLinks.xml");

            splashScreen.AddLine("Lade Einstellungen ...");
            Application.DoEvents();
            settings = SettingsWrapper.ReadFromXML(AppUserFolder);

            SeriesManager = SmManager.ReadFromFile();

            splashScreen.Hide();
        }

        private void fmMain_Load(object sender, EventArgs e)
        {
            this.Font = SystemFonts.MessageBoxFont;

            Rectangle screenSize = Screen.FromControl(this).Bounds;
            if (!screenSize.Contains(settings.Position))
            { settings.Position = new Point(0, 0); }
            if (!screenSize.Contains(new System.Drawing.Point(settings.Size)))
            { settings.Size = screenSize.Size; }
            if (settings.Size.Width < 400) { settings.Size.Width = 400; }
            if (settings.Size.Height < 300) { settings.Size.Height = 300; }

            this.Location = settings.Position;
            this.Size = settings.Size;
            if (settings.Maximized) { this.WindowState = FormWindowState.Maximized; }

            ApplySettings();
            InitializeGUI();

            tabControl1.SelectedTab = tabSTV;

            // Listen füllen
            StvTreeRefresh();
            localTreeRefresh();
            DownloadListRefresh();
            SearchFavoritesUpdate();
            SmSeriesRefresh();
        }

        private async void fmMain_Shown(object sender, EventArgs e)
        {
            downloader.Connect();
            stv.Login();
        }

        private void fmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            downloader.PrepareClose();
            if (tabControl1.SelectedTab == tabSettings)
            {
                SaveSettingsFromTab();
            }
            settings.Size = this.Size;
            settings.Position = this.Location;
            settings.Maximized = this.WindowState == FormWindowState.Maximized;
        }

        private void fmMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            while (!downloader.CanClose()) { Application.DoEvents(); }
            settings.SaveToXML();
            stv.SaveToXML();
            downloader.SaveToXML();
            local.SaveToXML();
            TxdbTitleLinks.SaveToXML();
            TxdbTelecastIdLinks.SaveToXML();
            SeriesManager.SaveToFile();
        }

        private async Task HourlyTask()
        {
            calEPG.MinDate = DateTime.Today.AddDays(-28);
            calEPG.MaxDate = DateTime.Today.AddDays(28);
            if (settings.StvAutoServerUpdate) await stv.Update(true, !settings.StvDoNotManageProgramming);
            if (settings.AutoDownload == AutoDownloadOptions.Scheduled && (DateTime.Now - settings.AutoDownloadSchedule).Hours < 1) { StvAutoDownload(); }
        }

        private void timerLong_Tick(object sender, EventArgs e)
        {
            HourlyTask();
        }

        /// <summary>
        /// Prüft die Anwendungseinstellungen auf Konsistenz und öffnet bei Problemen die entsprechende Konfigurationsseite
        /// </summary>
        /// <returns></returns>
        private bool CheckAppSettings()
        {
            if (settings.ManageDownloads & string.IsNullOrEmpty(tbDownloadPath.Text))
            {
                MessageBox.Show("Verzeichnis für Downloads von Save.TV nicht angegeben", "Einstellungen", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                tvSettings.SelectedNode = tvSettings.Nodes["nodeDownloadManager"];
                tbDownloadPath.Focus();
                return false;
            }
            if (settings.UseLocalArchive)
            {
                if (string.IsNullOrEmpty(tbLocalPathMovies.Text))
                {
                    MessageBox.Show("Verzeichnis für Filme nicht angegeben", "Einstellungen", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    tvSettings.SelectedNode = tvSettings.Nodes["nodeLocal"];
                    tbLocalPathMovies.Focus();
                    return false;
                }
                if (string.IsNullOrEmpty(tbLocalPathSeries.Text))
                {
                    MessageBox.Show("Verzeichnis für Serien nicht angegeben", "Einstellungen", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    tvSettings.SelectedNode = tvSettings.Nodes["nodeLocal"];
                    tbLocalPathSeries.Focus();
                    return false;
                }
                if (string.IsNullOrEmpty(tbLocalPathInfos.Text))
                {
                    MessageBox.Show("Verzeichnis für Info-Sendungen nicht angegeben", "Einstellungen", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    tvSettings.SelectedNode = tvSettings.Nodes["nodeLocal"];
                    tbLocalPathInfos.Focus();
                    return false;
                }
                if (string.IsNullOrEmpty(tbLocalPathOther.Text))
                {
                    MessageBox.Show("Verzeichnis für andere Sendungen nicht angegeben", "Einstellungen", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    tvSettings.SelectedNode = tvSettings.Nodes["nodeLocal"];
                    tbLocalPathOther.Focus();
                    return false;
                }
            }

            return true;
        }

        private void UnderConstruction()
        {
            MessageBox.Show("Noch nicht implementiert ... sorry!", "STV MANAGER", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
        }

        private void LoadSettingsToTab()
        {
            tvSettings.ExpandAll();

            // STV Settings
            tbStvUsername.Text = settings.StvUsername;
            tbStvPassword.Text = settings.StvPassword;
            cbStvSavePassword.Checked = settings.StvSavePassword;

            rbHdQuality.Checked = (settings.StvDefaultVideoFormat == RecordFormats.HD);
            rbSDQuality.Checked = (settings.StvDefaultVideoFormat == RecordFormats.SD);
            rbMobileQuality.Checked = (settings.StvDefaultVideoFormat == RecordFormats.Mobile);
            rbDivxQuality.Checked = (settings.StvDefaultVideoFormat == RecordFormats.DivX);

            cbStvPreferAdFree.Checked = settings.StvPreferAdFree;
            cbStvManageProgramming.Checked = !settings.StvDoNotManageProgramming;
            cbStvAutoServerUpdate.Checked = settings.StvAutoServerUpdate;

            // Grundeinstellungen
            cbManageDownloads.Checked = settings.ManageDownloads;
            cbUseTxDB.Checked = settings.UseTxdb;
            boxUseLocalArchive.Enabled = settings.UseTxdb;
            cbUseLocalArchive.Checked = settings.UseLocalArchive;

            boxStreamingMediaPlayerPath.Text = settings.StreamingPlayerPath;

            // Download-Einstellungen
            switch (settings.StvDownloadMethod)
            {
                case DownloadMethods.JDownloader:
                    rbDownloadJDL.Checked = true;
                    break;
                case DownloadMethods.ExternalDlm:
                    rbDownloadExternal.Checked = true;
                    break;
                case DownloadMethods.Synology:
                    rbDownloadSynology.Checked = true;
                    break;
                case DownloadMethods.InternalDlm:
                    rbDownloadInternal.Checked = true;
                    break;
            }

            UseJDL(settings.StvDownloadMethod == DownloadMethods.JDownloader);
            UseDiskStation(settings.StvDownloadMethod == DownloadMethods.Synology);
            UseExternalDlm(settings.StvDownloadMethod == DownloadMethods.ExternalDlm);

            cbUseAutoDownloads.Checked = settings.AutoDownload != AutoDownloadOptions.Never;
            rbAutoDownloadScheduled.Checked = settings.AutoDownload == AutoDownloadOptions.Scheduled;
            rbAutoDownloadImmediate.Checked = settings.AutoDownload == AutoDownloadOptions.Immediately;
            cbAutoDownloadAwaitAdFree.Checked = settings.AutoDownloadAwaitAdFree;
            cbAutoDownloadIgnoreDuplicates.Checked = settings.AutoDownloadSkipDuplicates;
            dtAutoDownloadTime.Value = settings.AutoDownloadSchedule;

            // Lokale Verzeichnisse
            tbDownloadPath.Text = settings.LocalPathDownloads;
            tbLocalPathMovies.Text = settings.LocalPathMovies;
            tbLocalPathSeries.Text = settings.LocalPathSeries;
            tbLocalPathOther.Text = settings.LocalPathOther;
            tbLocalPathInfos.Text = settings.LocalPathInfos;
            rbEpisodeCodeS.Checked = settings.LocalUseSxxExxEpisodeCode;

            tbSettingSeriesName.Text = settings.LocalNameSeries;

            //XBMC
            cbUseXbmc.Checked = settings.UseXbmc;
            tbXbmcUrl.Text = settings.XbmcUrl;
            numXbmcPort.Value = settings.XbmcPort;
            tbXbmcUser.Text = settings.XbmcUser;
            tbXbmcPass.Text = settings.XbmcPass;
        }

        private void SaveSettingsFromTab()
        {
            // STV Settings
            settings.StvUsername = tbStvUsername.Text;
            settings.StvSavePassword = cbStvSavePassword.Checked;
            settings.StvPassword = tbStvPassword.Text;
            settings.StvDoNotManageProgramming = !cbStvManageProgramming.Checked;
            settings.StvAutoServerUpdate = cbStvAutoServerUpdate.Checked;

            if (rbHdQuality.Checked) settings.StvDefaultVideoFormat = RecordFormats.HD;
            if (rbSDQuality.Checked) settings.StvDefaultVideoFormat = RecordFormats.SD;
            if (rbMobileQuality.Checked) settings.StvDefaultVideoFormat = RecordFormats.Mobile;
            if (rbDivxQuality.Checked) settings.StvDefaultVideoFormat = RecordFormats.DivX;
            settings.StvPreferAdFree = cbStvPreferAdFree.Checked;

            //Grundeinstellungen
            if (cbUseTxDB.Checked != settings.UseTxdb)
            {
                settings.UseTxdb = cbUseTxDB.Checked;
                if (settings.UseTxdb) { TxdbLinkKnown(stv.Telecasts); }
                else
                {
                    if (MessageBox.Show("Sollen alle bereits vorhandenen Verknüpfungen entfernt werden?", "Internet-Datenbanken", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question)
                        == DialogResult.Yes)
                    {
                        TxdbClear(stv.Telecasts);
                        TxdbTitleLinks.ForEach(link => link.Status = TxdbLinkStatusOptions.Ignored);
                    }
                }
            }

            settings.ManageDownloads = cbManageDownloads.Checked;

            settings.StreamingPlayerPath = boxStreamingMediaPlayerPath.Text;

            // Lokale Verzeichnisse
            settings.LocalPathDownloads = tbDownloadPath.Text;
            settings.LocalPathMovies = tbLocalPathMovies.Text;
            settings.LocalPathSeries = tbLocalPathSeries.Text;
            settings.LocalPathInfos = tbLocalPathInfos.Text;
            settings.LocalPathOther = tbLocalPathOther.Text;
            settings.LocalUseSxxExxEpisodeCode = rbEpisodeCodeS.Checked;

            settings.LocalNameSeries = tbSettingSeriesName.Text;

            if (cbUseLocalArchive.Checked != settings.UseLocalArchive)
            {
                settings.UseLocalArchive = cbUseLocalArchive.Checked;
                if (settings.UseLocalArchive)
                {
                    if (MessageBox.Show("Sollen alle Verzeichnisse jetzt eingelesen werden?", "Lokales Archiv", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                        == DialogResult.Yes)
                    {
                        local.UpdateArchive();
                    }
                }
                else
                {
                    if (MessageBox.Show("Sollen alle bereits vorhandenen Verknüpfungen entfernt werden?", "Lokales TV-Archiv", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                        == DialogResult.Yes)
                    {
                        local.Shows.ForEach(show => show.Foldername = "");
                        local.Episodes.ForEach(ep => ep.Filename = "");
                        local.Movies.ForEach(movie => movie.Filename = "");
                    }
                }
            }

            if (rbDownloadInternal.Checked)
            {
                settings.StvDownloadMethod = DownloadMethods.InternalDlm;
                settings.InternalDlmMaximumConnections = (int)numInternalDlmMaximumConnections.Value;
            }
            else if (rbDownloadJDL.Checked)
            {
                settings.StvDownloadMethod = DownloadMethods.JDownloader;
                settings.JDLPluginMode = rbJDLPluginMode.Checked;
                settings.JDLFullService = cbJDLFullService.Checked;
            }
            else if (rbDownloadExternal.Checked)
            {
                settings.StvDownloadMethod = DownloadMethods.ExternalDlm;
                settings.ExternalDlmTelecastLinks = rbExternalDlmTc.Checked;
            }
            else if (rbDownloadSynology.Checked)
            {
                settings.StvDownloadMethod = DownloadMethods.Synology;
                settings.SynoServerURL = tbSynoServerURL.Text;
                settings.SynoServerPort = (int)numSynoServerPort.Value;
                settings.SynoUseHttps = cbSynoUseHttps.Checked;
                settings.SynoUsername = tbSynoUsername.Text;
                settings.SynoSavePassword = cbSynoSavePassword.Checked;
                settings.SynoPassword = tbSynoPassword.Text;
                settings.SynoUseSSH = cbSynoUseSSH.Checked;
            }

            if (cbUseAutoDownloads.Checked)
            {
                settings.AutoDownload = rbAutoDownloadImmediate.Checked ? AutoDownloadOptions.Immediately : AutoDownloadOptions.Scheduled;
            }
            else { settings.AutoDownload = AutoDownloadOptions.Never; }
            settings.AutoDownloadSkipDuplicates = cbAutoDownloadIgnoreDuplicates.Checked;
            settings.AutoDownloadAwaitAdFree = cbAutoDownloadAwaitAdFree.Checked;
            settings.AutoDownloadSchedule = dtAutoDownloadTime.Value;

            //XBMC
            settings.UseXbmc = cbUseXbmc.Checked;
            settings.XbmcUrl = tbXbmcUrl.Text;
            settings.XbmcPort = (int)numXbmcPort.Value;
            settings.XbmcUser = tbXbmcUser.Text;
            settings.XbmcPass = tbXbmcPass.Text;

            settings.SaveToXML();
        }

        private void btStreamingMediaPlayerSelect_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlgPlayerSelect = new OpenFileDialog();
            dlgPlayerSelect.CheckFileExists = true;
            dlgPlayerSelect.Filter = "Ausführbare Dateien (*.exe)|*.exe|Alle Dateien (*.*)|*.*";
            dlgPlayerSelect.FilterIndex = 0;
            dlgPlayerSelect.Multiselect = false;
            dlgPlayerSelect.Title = "Mediaplayer auswählen";
            if (dlgPlayerSelect.ShowDialog() == DialogResult.OK) boxStreamingMediaPlayerPath.Text = dlgPlayerSelect.FileName;
        }

        private void UseDiskStation(bool Checked)
        {
            pnSettingSynology.Visible = Checked;
            if (Checked)
            {
                tbSynoServerURL.Text = settings.SynoServerURL;
                cbSynoUseHttps.Checked = settings.SynoUseHttps;
                tbSynoUsername.Text = settings.SynoUsername;
                tbSynoPassword.Text = settings.SynoPassword;
                cbSynoSavePassword.Checked = settings.SynoSavePassword;
                cbSynoUseSSH.Checked = settings.SynoUseSSH;
                numSynoServerPort.Value = settings.SynoServerPort;
            }
        }

        private void UseJDL(bool Checked)
        {
            pnSettingJDL.Visible = Checked;
            if (Checked)
            {
                rbJDLNoHassle.Checked = !settings.JDLPluginMode;
                rbJDLPluginMode.Checked = settings.JDLPluginMode;
                cbJDLFullService.Enabled = settings.JDLPluginMode;
                cbJDLFullService.Checked = settings.JDLFullService;
            }
        }

        private void UseExternalDlm(bool Checked)
        {
            boxSettingExternalDlm.Visible = Checked;
            if (Checked)
            {
                rbExternalDlmDlUrl.Checked = !settings.ExternalDlmTelecastLinks;
                rbExternalDlmTc.Checked = settings.ExternalDlmTelecastLinks;
            }
        }

        private void UseInternalDlm(bool Checked)
        {
            pnSettingInternalDlm.Visible = Checked;
            if (Checked)
            {
                numInternalDlmMaximumConnections.Value = settings.InternalDlmMaximumConnections;
            }
        }

        private void ShowTab(TabPage Tab, TabPage ShowAfter)
        {
            if (!tabControl1.TabPages.Contains(Tab))
            {
                tabControl1.TabPages.Insert(tabControl1.TabPages.IndexOf(ShowAfter) + 1, Tab);
            }
        }

        private void HideTab(TabPage Tab)
        {
            if (tabControl1.TabPages.Contains(Tab))
            {
                tabControl1.TabPages.Remove(Tab);
            }
        }

        private void ApplySettings()
        {
            stv.Username = settings.StvUsername;
            stv.Password = settings.StvPassword;

            local.ShowsBasePath = settings.LocalPathSeries;
            local.MoviesBasePath = settings.LocalPathMovies;
            local.UseSxxExxEpisodeCode = settings.LocalUseSxxExxEpisodeCode;
            toolStvShowDuplicates.Checked = settings.StvShowDuplicates;
            toolStvShowBroken.Checked = settings.StvShowBroken;
            toolStvShowHasAdfree.Checked = settings.StvShowHasAdfree;
            toolStvShowHasNoAdfree.Checked = settings.StvShowHasNoAdfree;
            toolStvShowLocalAvailable.Checked = !settings.StvShowLocalAvailable;
            toolDownloadDefault.Enabled = settings.ManageDownloads;
            toolStream.Enabled = settings.ManageDownloads;
            toolDownloadAdFree.Checked = settings.StvPreferAdFree;
            LocalEpisodeShowOption(settings.LocalShowAll ? "all" : "local");
            StvChangeSort(settings.StvSortOption);
            toolTvdbSettings.Enabled = settings.UseTxdb;
            if (settings.UseTxdb)
            {
                //ShowTab(tabShows, tabSettings);
                LinkUndefinedTxdbLinks();
                CheckDuplicates(stv.Telecasts);
                StvTreeRefresh();
            }
            else
            {
                HideTab(tabShows);
            }
            if (settings.UseLocalArchive)
            {
                ShowTab(tabLocal, tabSearch);
                ShowTab(tabLocalArchive, tabLocal);
                olvColumnLocal.IsVisible = true;
            }
            else
            {
                HideTab(tabLocal);
                HideTab(tabLocalArchive);
                olvColumnLocal.IsVisible = false;
            }
            if (settings.ManageDownloads)
            {
                ShowTab(tabDownloads, tabAssistant);
                olvColumnDownload.IsVisible = true;
            }
            else
            {
                HideTab(tabDownloads);
                olvColumnDownload.IsVisible = false;
            }
            olvStvList.RebuildColumns();
            toolDownloadToLocal.Enabled = settings.UseLocalArchive;
            toolStvShowLocalAvailable.Enabled = settings.UseLocalArchive;
            toolAssistantShowDuplicates.Checked = settings.AssistantShowDuplicates;
            toolAssistantShowNonProgrammable.Checked = settings.AssistantShowNonProgrammable;

            downloader.SetDownloadMethod(settings.StvDownloadMethod);

            if (settings.StvDownloadMethod == DownloadMethods.Synology)
            {
                if (settings.SynoPassword == "")
                {
                    fmLogin synoLogin = new fmLogin("DiskStation Login") { Username = settings.SynoUsername };
                    if (synoLogin.ShowDialog() == DialogResult.OK)
                    {
                        settings.SynoUsername = synoLogin.Username;
                        settings.SynoPassword = synoLogin.Password;
                        settings.SynoSavePassword = synoLogin.SavePassword;
                        settings.SaveToXML();
                    }
                }
                downloader.SetCredentials(settings.SynoUsername, settings.SynoPassword, settings.SynoServerURL, settings.SynoServerPort, settings.SynoUseHttps);
            }

            downloader.DestinationPath = settings.LocalPathDownloads;
            downloader.MaximumConnections = settings.InternalDlmMaximumConnections;
            downloader.SendTelecastLink = (
                settings.StvDownloadMethod == DownloadMethods.JDownloader & settings.JDLPluginMode |
                settings.StvDownloadMethod == DownloadMethods.ExternalDlm & settings.ExternalDlmTelecastLinks
                );
            toolDownloadCancel.Enabled = downloader.CanCancel;

            cbExecuteSearch.Checked = settings.SearchFavoritesExecuteSearch;

            if (settings.StvDoNotManageProgramming)
            {
                toolStvShowProgrammed.Enabled = false;
                stv.Programmed.Clear();
                if (toolStvShowProgrammed.Checked)
                {
                    toolStvShowProgrammed.Checked = false;
                    StvTreeRefresh();
                }
            }
            else
            {
                toolStvShowProgrammed.Enabled = true;
                if (!stv.Programmed.Any()) stv.Update(false, true);
            }
        }

        private void InitializeStvList()
        {
            olvStvList.CellPadding = new Rectangle { X = 3, Y = 0 };

            olvColumnTitle.AspectGetter = delegate (object row)
            {
                if (row == null) { return null; }
                else
                {
                    tTelecast item = row as tTelecast;
                    if (item.tvdbShowID > 0) { return local.Show(item.tvdbShowID).Title; }
                    else if (item.tmdbMovieID > 0) { return local.Movie(item.tmdbMovieID).Title; }
                    else { return item.Title; }
                }
            };

            olvColumnSubTitle.AspectGetter = delegate (object row)
            {
                if (row == null) { return null; }
                else
                {
                    tTelecast item = row as tTelecast;
                    string duplicate = item.Duplicate ? "   (Wdh.)" : "";
                    if (item.tvdbEpisodeID > 0) { return local.Episode(item.tvdbEpisodeID).Title + duplicate; }
                    else { return item.SubTitle + duplicate; }
                }
            };

            olvColumnYear.AspectGetter = delegate (object row)
            {
                if (row == null) { return null; }
                else
                {
                    tTelecast item = row as tTelecast;
                    if (item.tvdbEpisodeID > 0)
                    {
                        tEpisode Episode = local.Episode(item.tvdbEpisodeID);
                        return Episode.EpisodeCode(settings.LocalUseSxxExxEpisodeCode);
                    }
                    else if (item.tmdbMovieID > 0)
                    {
                        tMovie Movie = local.Movie(item.tmdbMovieID);
                        return Movie.Year > 1 ? Movie.Year.ToString() : "unbekannt";
                    }
                    else if (item.Episode > 0)
                    {
                        tEpisode Episode = new tEpisode()
                        {
                            Season = item.Season,
                            Episode = item.Episode
                        };
                        return Episode.EpisodeCode(settings.LocalUseSxxExxEpisodeCode);
                    }
                    else if (item.Year > 0)
                    {
                        return item.Year.ToString();
                    }
                    else return String.Empty;
                }
            };

            olvColumnCategory.AspectGetter = new AspectGetterDelegate(olvCategoryAspectGetter);

            olvColumnHD.CellPadding = new Rectangle { X = 4, Y = 0 };
            olvColumnHD.Renderer = new ImageRenderer();
            olvColumnHD.AspectGetter = delegate (object row)
            {
                if (row == null) { return null; }
                else
                {
                    tTelecast item = row as tTelecast;
                    return new[] { imgStvList.Images[item.hasHD ? "HD_on" : "HD_off"] };
                }
            };

            olvColumnAdFree.CellPadding = new Rectangle { X = 4, Y = 0 };
            olvColumnAdFree.Renderer = new ImageRenderer();
            olvColumnAdFree.AspectGetter = delegate (object row)
            {
                if (row == null) { return null; }
                else
                {
                    tTelecast item = row as tTelecast;
                    return new[] { imgStvList.Images[item.AdFree ? "AdFree_on" : "AdFree_off"] };
                }
            };

            olvColumnDownload.CellPadding = new Rectangle { X = 4, Y = 0 };
            olvColumnDownload.Renderer = new ImageRenderer();
            olvColumnDownload.AspectGetter = delegate (object row)
            {
                if (row == null) { return null; }
                else
                {
                    string key;
                    switch (downloader.Telecasts.GetStatusById((row as tTelecast).ID))
                    {
                        case DownloadStatus.Submitting:
                        case DownloadStatus.Waiting:
                        case DownloadStatus.Progressing:
                            key = "Download_running";
                            break;

                        case DownloadStatus.Finished:
                        case DownloadStatus.Renamed:
                        case DownloadStatus.MovedToArchive:
                            key = "Download_ready";
                            break;

                        default:
                            key = "Download_off";
                            break;
                    }

                    return new[] { imgStvList.Images[key] };
                }
            };

            olvColumnLocal.CellPadding = new Rectangle { X = 4, Y = 0 };
            olvColumnLocal.Renderer = new ImageRenderer();
            olvColumnLocal.AspectGetter = delegate (object row)
            {
                if (row == null) { return null; }
                else
                {
                    tTelecast item = row as tTelecast;
                    bool localAvailable = false;
                    bool localHd = false;
                    switch (item.Category)
                    {
                        case Categories.Series:
                            localAvailable = local.Episode(item.tvdbEpisodeID).Status == TelecastStatus.InLocalArchive;
                            localHd = local.Episode(item.tvdbEpisodeID).isHd;
                            break;

                        case Categories.Movie:
                            localAvailable = local.Movie(item.tmdbMovieID).Status == TelecastStatus.InLocalArchive;
                            break;
                    }

                    if (localAvailable)
                    {
                        if (localHd) { return new[] { imgStvList.Images["Local_on_HD"] }; }
                        else { return new[] { imgStvList.Images["Local_on"] }; }
                    }
                    else { return new[] { imgStvList.Images["Local_off"] }; }
                }
            };

            olvStvList.CellToolTipGetter = delegate (OLVColumn col, Object row)
            {
                if (row == null) { return null; }
                else
                {
                    tTelecast item = row as tTelecast;
                    if (col == olvColumnHD)
                    {
                        return item.hasHD ? "Sendung in HD verfügbar" : "Sendung nicht in HD verfügbar";
                    }

                    else if (col == olvColumnAdFree) { return item.AdFree ? "Schnittliste verfügbar" : "Schnittliste nicht verfügbar"; }

                    else if (col == olvColumnDownload)
                    {
                        switch (downloader.Telecasts.GetStatusById(item.ID))
                        {
                            case DownloadStatus.Submitting:
                            case DownloadStatus.Waiting:
                            case DownloadStatus.Progressing:
                                return "Download läuft ...";

                            case DownloadStatus.Finished:
                            case DownloadStatus.Renamed:
                            case DownloadStatus.MovedToArchive:
                                return "Sendung ist heruntergeladen";

                            default:
                                return "Sendung noch nicht heruntergeladen";
                        }
                    }

                    else if (col == olvColumnLocal)
                    {
                        bool localAvailable =
                            local.Episode(item.tvdbEpisodeID).Status == TelecastStatus.InLocalArchive |
                            local.Movie(item.tmdbMovieID).Status == TelecastStatus.InLocalArchive;
                        return localAvailable ? "Sendung im Lokalen TV-Archiv" : "Sendung nicht im Lokalen TV-Archiv";
                    }

                    else return null;
                }
            };

            olvStvList.PrimarySortColumn = olvColumnAiring;
            olvStvList.PrimarySortOrder = SortOrder.Descending;
        }

        private void InitializeEpgList()
        {
            olvColumnEpgAirTime.AspectGetter = delegate (object row)
            {
                if (row == null) { return null; }
                else { return (row as tTelecast).StartDate.ToShortTimeString(); }
            };

            olvColumnEpgAirTime.GroupKeyGetter = delegate (object row)
            {
                if (row == null) { return null; }
                else
                {
                    tTelecast telecast = (tTelecast)row;
                    int StartHour = (telecast.StartDate - calEPG.SelectionStart).Hours + (telecast.StartDate - calEPG.SelectionStart).Days * 24;
                    if (StartHour < 12) { return 6; }
                    else if (StartHour < 20) { return 12; }
                    else if (StartHour < 24) { return 20; }
                    else { return 24; }
                }
            };

            olvColumnEpgAirTime.GroupKeyToTitleConverter = delegate (object groupKey)
            {
                switch ((int)groupKey)
                {
                    case 6: return "Vormittag";
                    case 12: return "Nachmittag";
                    case 20: return "Abend";
                    case 24: return "Nacht";
                    default: return "";
                }
            };

            olvColumnEpgCategory.AspectGetter = new AspectGetterDelegate(olvCategoryAspectGetter);

            olvColumnEpgTvStationName.CellPadding = new Rectangle { X = 3, Y = 0 };
            olvColumnEpgTvStation.CellPadding = new Rectangle { X = 8, Y = 0 };
            olvColumnEpgTvStation.Renderer = new ImageRenderer();
        }

        private void InitializeSmEpisodes()
        {
            colSmEpisodesEpisode.AspectGetter = delegate (object row)
            {
                if (row == null) { return null; }
                else
                {
                    SmEpisode episode = row as SmEpisode;
                    return episode.Episode.Code(settings.LocalUseSxxExxEpisodeCode ? EpisodeCodeTypes.sCode : EpisodeCodeTypes.xCode);
                }
            };

            colSmEpisodesSaveTv.AspectGetter = delegate (object row)
            {
                if (row == null) { return null; }
                else
                {
                    int TelecastId = (row as SmEpisode).SaveTv.ID;
                    if (toolSmViewArchive.Checked & stv.VideoArchive.Contains(TelecastId)) return "im Archiv";
                    else if (toolSmViewProgrammed.Checked & stv.Programmed.Contains(TelecastId)) return "Programmiert";
                    else return null;
                }
            };

            lvSmEpisodes.PrimarySortColumn = colSmEpisodesAiring;
            lvSmEpisodes.PrimarySortOrder = SortOrder.Descending;
        }

        public object olvCategoryAspectGetter(object row)
        {
            if (row == null) { return null; }
            else { return (row as tTelecast).Category.ToDescription(); }
        }

        private void InitializeLocalArchiveList()
        {
            olvColumnLocalArchiveName.AspectGetter = delegate (object row)
            {
                if (row == null) { return null; }
                else { return Path.GetFileName((row as string)); }
            };

            olvColumnLocalArchiveFileSize.AspectGetter = delegate (object row)
            {
                if (row == null) { return null; }
                else
                {
                    if (File.Exists(row as string))
                    {
                        return ((new FileInfo(row as string).Length / 1024 / 1024).ToString() + " MB");
                    }
                    else return String.Empty;
                }
            };

            olvColumnLocalArchiveTxdb.AspectGetter = delegate (object row)
            {
                if (row == null) { return null; }
                else
                {
                    string result = String.Empty;
                    string Filename = row as string;
                    string Type = tvLocalArchiveTree.SelectedNode.Tag as string;

                    if (Type == Categories.Movie.ToDescription())
                    {
                        tMovie movie = local.Movies.FindByFileName(Filename);
                        if (movie != null) { result = movie.Year.ToString(); }
                    }
                    else if (Type == Categories.Series.ToDescription())
                    {
                        tEpisode episode = local.Episodes.FindByFileName(Filename);
                        if (episode != null) { result = episode.EpisodeCode(settings.LocalUseSxxExxEpisodeCode); }
                    }
                    return result;
                }
            };

            olvColumnLocalArchiveTxdbTitle.AspectGetter = delegate (object row)
            {
                if (row == null) { return null; }
                else
                {
                    string result = String.Empty;
                    string Filename = row as string;
                    string Type = tvLocalArchiveTree.SelectedNode.Tag as string;

                    if (Type == Categories.Movie.ToDescription())
                    {
                        tMovie movie = local.Movies.FindByFileName(Filename);
                        if (movie != null) { result = movie.Title; }
                    }
                    else if (Type == Categories.Series.ToDescription())
                    {
                        tEpisode episode = local.Episodes.FindByFileName(Filename);
                        if (episode != null) { result = episode.Title; }
                    }
                    return result;
                }
            };

            olvLocalArchiveList.PrimarySortColumn = olvColumnLocalArchiveName;
            olvLocalArchiveList.PrimarySortOrder = SortOrder.Ascending;

        }

        private void InitializeGUI()
        {
            InitializeStvList();
            InitializeEpgList();
            InitializeSmEpisodes();
            InitializeLocalArchiveList();

            cbActivateDebugLogging.Checked = settings.EnableDebugLogging;

            tvSettings.SelectedNode = tvSettings.Nodes["nodeBasics"];

            lvSearchDetails.ListViewItemSorter = new ListViewColumnSorter
            {
                Order = SortOrder.Ascending,
                SortColumn = 4,
                Structure = "XXEXDXXX"
            };

            lvDownloads.ListViewItemSorter = new ListViewColumnSorter
            {
                Order = SortOrder.Ascending,
                SortColumn = 0
            };

            lvLocalListShow.ListViewItemSorter = new ListViewColumnSorter
            {
                Order = SortOrder.Ascending,
                SortColumn = 0,
                Structure = "EXX"
            };

            lvLocalListMovie.ListViewItemSorter = new ListViewColumnSorter
            {
                Order = SortOrder.Ascending,
                SortColumn = 0
            };

            lvShows.ListViewItemSorter = new ListViewColumnSorter
            {
                Order = SortOrder.Ascending,
                SortColumn = 0
            };

            lvAssistant.ListViewItemSorter = new ListViewColumnSorter
            {
                Order = SortOrder.Ascending,
                SortColumn = 2,
                Structure = "XEDXX"
            };

            calEPG.SelectionStart = DateTime.Today.Date;

            // Defaultwerte in die Suchfelder laden
            dtSearchDate.MinDate = DateTime.Now.Date;
            dtSearchDate.MaxDate = DateTime.Now.Date.AddDays(30);

            foreach (SearchDateOptions option in Enum.GetValues(typeof(SearchDateOptions)))
            {
                boxSearchDateRepeat.Items.Add(option.ToDescription());
            }
            boxSearchDateRepeat.SelectedItem = SearchDateOptions.SingleDay.ToDescription();

            foreach (SearchFulltextOptions option in Enum.GetValues(typeof(SearchFulltextOptions)))
            {
                boxSearchFulltextOptions.Items.Add(option.ToDescription());
            }
            boxSearchFulltextOptions.SelectedItem = SearchFulltextOptions.InTitle.ToDescription();
        }

        private async Task OnLoginStatusChange(bool Connect)
        {
            if (Connect) { HourlyTask(); }
            timerHourly.Enabled = Connect;

            // GUI Elemente aktivieren
            toolSearchStart.Enabled = Connect;
            toolShowEdit.Enabled = Connect;
            toolShowIgnore.Enabled = Connect;

            if (Connect)
            {
                rbDivxQuality.Enabled = false;
                rbSDQuality.Enabled = false;
                rbHdQuality.Enabled = false;
                rbMobileQuality.Enabled = false;
                boxAdFree.Enabled = false;

                // an Save.TV Paket Status anpassen
                if (await stv.IsPremium())
                {
                    rbSDQuality.Enabled = true;
                    rbMobileQuality.Enabled = true;
                    rbHdQuality.Enabled = true;
                    if (settings.StvDefaultVideoFormat == RecordFormats.DivX)
                    {
                        settings.StvDefaultVideoFormat = RecordFormats.SD;
                        rbSDQuality.Checked = true;
                    }
                    boxAdFree.Enabled = true;
                }
                else
                {
                    rbSDQuality.Enabled = true;
                    if (settings.StvDefaultVideoFormat != RecordFormats.SD)
                    {
                        settings.StvDefaultVideoFormat = RecordFormats.SD;
                        rbDivxQuality.Checked = true;
                    }
                    settings.StvPreferAdFree = false;
                    settings.AutoDownloadAwaitAdFree = false;
                }
            }

            if (Connect)
            {
                // verfügbare Sender in Suchfeld eintragen
                if (boxSearchTVStation.Items.Count == 0)
                {
                    foreach (tTVStation tvStation in stv.TVStations) //.OrderBy(tv => tv.ID))
                    {
                        boxSearchTVStation.Items.Add(tvStation.Name);
                    }
                    boxSearchTVStation.SelectedItem = stv.TVStations.First().Name;
                }
            }
            boxSearch.Enabled = Connect;
            boxFavorites.Enabled = Connect;

            if (Connect)
            {
                // verfügbare Sender in EPG-Seite eintragen

                boxEpgTvStations.Enabled = true;

                System.Net.WebClient client = new System.Net.WebClient();

                olvEpgTvStations.SetObjects(stv.TVStations); //.OrderBy(tv => tv.ID));
            }

            SmUpdateToolbar();
        }

        private void tabControl1_Deselecting(object sender, TabControlCancelEventArgs e)
        {
            if (e.TabPage == tabSettings)
            {
                SaveSettingsFromTab();
                ApplySettings();
                if (!CheckAppSettings()) { e.Cancel = true; }
            }
        }

        private void tabControl1_Selecting(object sender, TabControlCancelEventArgs e)
        {
            if (e.TabPageIndex >= tabControl1.TabCount)
            {
                e.Cancel = true;
            }

            if (e.TabPage == tabSettings)
            {
                LoadSettingsToTab();
            }
            else if (e.TabPage == tabShows)
            {
                showsListUpdate();
            }
            else if (e.TabPage == tabEPG)
            {
                calEPG.MinDate = DateTime.Today.AddDays(-28);
                calEPG.MaxDate = DateTime.Today.AddDays(28);
                if (olvEpgTvStations.SelectedIndex == -1) olvEpgTvStations.SelectedIndex = 0;
                olvEpgTvStations.BuildList();
            }
            else if (e.TabPage == tabLocalArchive)
            {
                LocalArchiveTreeRefresh();
            }
        }

        private void SelectFolder(string Foldertitle, TextBox FolderTextBox)
        {
            FolderBrowserDialog dlgSelectFolder = new FolderBrowserDialog();
            dlgSelectFolder.Description = Foldertitle;
            dlgSelectFolder.SelectedPath = FolderTextBox.Text;
            if (dlgSelectFolder.ShowDialog() == DialogResult.OK)
            {
                FolderTextBox.Text = dlgSelectFolder.SelectedPath;
            }
        }

        private void btDownloadSelect_Click(object sender, EventArgs e)
        {
            SelectFolder("Verzeichnis festlegen, in dem neue Downloads aus STV landen:", tbDownloadPath);
        }

        private void btLocaSelectSeries_Click(object sender, EventArgs e)
        {
            SelectFolder("Verzeichnis festlegen, in dem TV-Serien archiviert werden. Jede Serie muss hier in einem eigenen Unterverzeichnis liegen:", tbLocalPathSeries);
        }

        private void btLocalSelectMovie_Click(object sender, EventArgs e)
        {
            SelectFolder("Verzeichnis festlegen, in dem Filme archiviert werden:", tbLocalPathMovies);
        }

        private void btLocalSelectInfos_Click(object sender, EventArgs e)
        {
            SelectFolder("Verzeichnis festlegen, in dem Info-Sendungen archiviert werden:", tbLocalPathInfos);
        }

        private void btLocalSelectOther_Click(object sender, EventArgs e)
        {
            SelectFolder("Verzeichnis festlegen, in dem andere Sendungen archiviert werden:", tbLocalPathOther);
        }

        private void OnLoginEvent(LoginEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<LoginEventArgs>(OnLoginEvent), e);
                return;
            }

            MessageBox.Show(e.Message, "Save.TV Login", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

            fmLogin stvLogin = new fmLogin("Save.TV Server Login");

            stvLogin.Username = settings.StvUsername;
            if (stvLogin.ShowDialog() == DialogResult.OK)
            {
                stv.Username = stvLogin.Username;
                stv.Password = stvLogin.Password;

                // Logindaten in den Settings speichern
                settings.StvUsername = stvLogin.Username;
                settings.StvSavePassword = stvLogin.SavePassword;
                if (stvLogin.SavePassword)
                {
                    settings.StvPassword = stvLogin.Password;
                }
                settings.SaveToXML();
                stv.Login();
            }
        }

        private void OnLogEvent(LogEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<LogEventArgs>(OnLogEvent), e);
                return;
            }

            if (settings.EnableDebugLogging)
            {
                boxDebugLogging.Text += e.Time.ToString() + "  " + e.Event + "\r\n";
            }
        }

        private void OnServerEvent(ServerEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<ServerEventArgs>(OnServerEvent), e);
                return;
            }

            statusLogin.Text = e.ServerStatus.ToDescription();

            switch (e.ServerStatus)
            {
                case StvStatusOptions.Offline:
                    statusLoginState.ForeColor = Color.Gray;
                    OnLoginStatusChange(false);
                    break;

                case StvStatusOptions.ServerError:
                    statusLoginState.ForeColor = Color.Red;
                    OnLoginStatusChange(false);
                    break;

                case StvStatusOptions.LoginError:
                case StvStatusOptions.SessionWaiting:
                    statusLoginState.ForeColor = Color.Red;
                    break;

                case StvStatusOptions.SessionCompleted:
                case StvStatusOptions.LoginWaiting:
                case StvStatusOptions.LoginCompleted:
                case StvStatusOptions.ConfigWaiting:
                    statusLoginState.ForeColor = Color.Gold;
                    break;

                case StvStatusOptions.Ready:
                    statusLoginState.ForeColor = Color.Green;
                    OnLoginStatusChange(true);
                    break;
            }
        }

        private void OnTaskUpdateEvent(TaskUpdateEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<TaskUpdateEventArgs>(OnTaskUpdateEvent), e);
                return;
            }

            switch (e.Current)
            {
                case 0:
                    switch (e.Option)
                    {
                        case TaskUpdateOptions.VideoArchive:
                        case TaskUpdateOptions.Programmed:
                            toolStvRefresh.Text = "Neu laden ...";
                            toolStvRefresh.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
                            break;
                    }
                    statusInfo.Visible = false;
                    statusProgress.Visible = true;
                    toolProgressLabel.Text = "Verarbeite " + e.Option.ToDescription() + " 0/" + e.Total.ToString();
                    toolProgress.Value = 0;
                    break;

                case -1:
                    statusProgress.Visible = false;
                    statusInfo.Visible = true;
                    switch (e.Option)
                    {
                        case TaskUpdateOptions.Search:
                            stv.SearchUpdateEvent -= OnSearchUpdate;
                            stv.SearchUpdateEvent -= OnSearchUpdateAndProgram;
                            stv.SearchUpdateEvent -= OnAssistantSearchUpdateEvent;
                            break;
                        case TaskUpdateOptions.VideoArchive:
                        case TaskUpdateOptions.Programmed:
                            toolStvRefresh.DisplayStyle = ToolStripItemDisplayStyle.Text;
                            toolStvRefresh.Text = "Aktualisieren";
                            break;
                        case TaskUpdateOptions.Wunschliste:
                            toolAssistantSearch.Text = "Suchen";
                            toolAssistantSearch.DisplayStyle = ToolStripItemDisplayStyle.Text;
                            break;
                    }
                    StatusStripUpdate();
                    break;

                default:
                    statusInfo.Visible = false;
                    statusProgress.Visible = true;
                    toolProgressLabel.Text = "Verarbeite " + e.Option.ToDescription() + " " + e.Current.ToString() + "/" + e.Total.ToString();
                    toolProgress.Value = Math.Min(e.Current * 100 / e.Total, 100);
                    break;
            }
            Application.DoEvents();
        }

        private void OnVideoArchiveRefreshEvent(VideoArchiveRefreshEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<VideoArchiveRefreshEventArgs>(OnVideoArchiveRefreshEvent), e);
                return;
            }

            switch (e.Change)
            {
                case TelecastsRefreshOptions.TelecastsDeleted:
                    StvTreeRefresh();
                    break;

                case TelecastsRefreshOptions.TelecastsAdded:

                    // verknüpfe bekannte Serien und Filme, setze Rest auf undefined
                    TxdbLinkKnown(e.Telecasts);
                    if (settings.UseTxdb) LinkUndefinedTxdbLinks();

                    // prüfe die Sendungen auf Wiederholungen
                    CheckDuplicates(stv.Telecasts);
                    StvTreeRefresh();
                    break;

                case TelecastsRefreshOptions.Finished:

                    // prüfen, ob bereits Downloads gestartet wurden
                    foreach (tTelecast Telecast in stv.VideoArchive)
                    {
                        tTelecastDownload TelecastDownload = downloader.Telecasts.FindOrAdd(Telecast.ID);
                        switch (TelecastDownload.Status)
                        {
                            case DownloadStatus.Waiting:
                            case DownloadStatus.Progressing:
                                Telecast.Status = TelecastStatus.Downloading;
                                break;

                            case DownloadStatus.Finished:
                                Telecast.Status = TelecastStatus.DownloadFinished;
                                break;
                        }
                    }

                    // verknüpfe bekannte Serien und Filme, setze Rest auf undefined
                    TxdbLinkKnown(stv.Telecasts);
                    if (settings.UseTxdb) LinkUndefinedTxdbLinks();

                    // prüfe die Sendungen auf Wiederholungen
                    CheckDuplicates(stv.Telecasts);
                    StvTreeRefresh();
                    if (settings.AutoDownload == AutoDownloadOptions.Immediately) { StvAutoDownload(); }
                    break;
            }
        }

        private async Task LinkUndefinedTxdbLinks()
        {
            tTxdbLinkCollection<string> newLinks = TxdbTitleLinks.GetUndefined();
            if (newLinks.Count() > 0)
            {
                string Message = newLinks.Count().ToString() +
                    " neue Serien oder Filme gefunden. Mit Internet-Datenbanken verknüpfen?\r\nVerknüpfungen können auch später manuell angelegt werden.";
                if (settings.UseTxdb &&
                    MessageBox.Show(Message, "Neue Serien oder Filme im Archiv", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes
                    )
                {
                    foreach (tTxdbLink<string> Link in newLinks)
                    {
                        bool Cancel = !await TxdbLinkDefine(Link);
                        if (Cancel) break;
                    }
                }
                newLinks.GetUndefined().ForEach(link => link.Status = TxdbLinkStatusOptions.Ignored);
                localTreeRefresh();
            }
        }

        private void OnTelecastUpdateEvent(TelecastUpdateEventArgs e)
        {
            switch (e.UpdateEvent)
            {
                case TelecastUpdateEvents.StatusChanged:
                    olvStvList.RefreshObject(e.Telecast);
                    break;
            }
        }

        private void toolStvRefresh_ButtonClick(object sender, EventArgs e)
        {
            stv.Update(true, !settings.StvDoNotManageProgramming);
        }

        private void NeuladenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            stv.Refresh(true, !settings.StvDoNotManageProgramming);
        }

        private void StvChangeSort(string Option)
        {
            // Uncheck all items
            nachDatumSortierenToolStripMenuItem.Checked = false;
            nachTitelneuesteSendungToolStripMenuItem.Checked = false;
            nachTitelSortierenToolStripMenuItem.Checked = false;
            nachTypSerieToolStripMenuItem.Checked = false;

            // sort by clicked item
            nachTitelSortierenToolStripMenuItem.Checked = (Option == "title");
            nachTitelneuesteSendungToolStripMenuItem.Checked = (Option == "lasttelecast");
            nachTypSerieToolStripMenuItem.Checked = (Option == "type");
            nachDatumSortierenToolStripMenuItem.Checked = (Option == "date");

            settings.StvSortOption = Option;
            toolStvListSort.Tag = Option;
        }

        private void StvSortierenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem menu = (ToolStripMenuItem)sender;
            string tag = (string)menu.Tag;
            StvChangeSort(tag);
            StvTreeRefresh();
        }

        /// <summary>
        /// Prüfe Serien und Filme auf Wiederholungen
        /// </summary>
        /// <param name="Telecasts">Liste der zu prüfenden Sendungen</param>
        private void CheckDuplicates(tTelecastCollection Telecasts)
        {
            foreach (tTelecast telecast in Telecasts.OrderBy(tc => tc.ID))
            {
                // als Default wird jede Sendung als einzeln definiert
                telecast.FirstAiredID = telecast.ID;

                // erste Episoden-Ausstrahlung mit gleicher Tvdb ID suchen
                if (telecast.tvdbEpisodeID > 0)
                { telecast.FirstAiredID = stv.Telecasts.OrderBy(tc => tc.ID).First(tc => tc.tvdbEpisodeID == telecast.tvdbEpisodeID).ID; }

                // erste Film-Ausstrahlung mit gleicher Tmdb ID suchen
                else if (telecast.tmdbMovieID > 0)
                { telecast.FirstAiredID = stv.Telecasts.OrderBy(tc => tc.ID).First(tc => tc.tmdbMovieID == telecast.tmdbMovieID).ID; }

                // handelt es sich um eine Wiederholung?
                telecast.Duplicate = telecast.ID != telecast.FirstAiredID;
            }
        }

        private void TxdbClear(tTelecastCollection clearTelecasts)
        {
            foreach (tTelecast item in clearTelecasts)
            {
                item.tvdbShowID = 0;
                item.tvdbEpisodeID = 0;
                item.tmdbMovieID = 0;
                item.Duplicate = false;
                item.Status = TelecastStatus.OnStvServer;
            }
        }

        /// <summary>
        /// Sendungen mit bekannten Serien und Filmen verknüpfen
        /// </summary>
        /// <param name="Telecasts">Zu verknüpfende Sendungen</param>
        private void TxdbLinkKnown(IEnumerable<tTelecast> Telecasts)
        {
            foreach (tTelecast telecast in Telecasts)
            {
                switch (telecast.Category)
                {
                    case Categories.Series:
                    case Categories.Info:
                        TxdbTelecastIdLinks.FindOrAddNew(telecast.ID, TxdbLinkTypes.Show);
                        telecast.tvdbShowID = TxdbTitleLinks.FindOrAddNew(telecast.Title, TxdbLinkTypes.Show).ID;
                        if (telecast.tvdbShowID > 0 & telecast.tvdbEpisodeID == 0)
                        {
                            telecast.Category = Categories.Series;
                            TxbdLinkEpisode(telecast, true);
                        }
                        break;

                    case Categories.Movie:
                        TxdbTelecastIdLinks.FindOrAddNew(telecast.ID, TxdbLinkTypes.Movie);
                        telecast.tmdbMovieID = TxdbTitleLinks.FindOrAddNew(telecast.Title, TxdbLinkTypes.Movie).ID;
                        break;
                }
            }
        }

        /// <summary>
        /// Neuen Link für Sendungen erzeugen
        /// </summary>
        /// <param name="StvTitle">Titel der Sendung</param>
        /// <param name="Link">Txdb Daten</param>
        /// <returns>false, wenn der User den Vorgang abbrechen will</returns>
        private async Task<bool> TxdbLinkDefine(tTxdbLink<string> Link)
        {
            tTelecastCollection Telecasts = stv.Telecasts.GetByTitle(Link.Key);
            bool result = true;
            switch (Link.Type)
            {
                case TxdbLinkTypes.Movie:
                    result = TxdbLinkMovie(Link.Key, Telecasts);
                    break;

                case TxdbLinkTypes.Show:
                    result = await TxdbLinkShow(Link.Key, Telecasts);
                    break;
            }
            return result;
        }

        /// <summary>
        /// Telecast mit Film aus TheMovieDB verknüpfen
        /// </summary>
        /// <param name="stvTitle">Titel der Sendung im Save.TV Archiv</param>
        /// <param name="Telecasts">Sendungen zu diesem Titel</param>
        /// <returns>false, wenn der User den Vorgang abbrechen will</returns>
        private bool TxdbLinkMovie(string stvTitle, tTelecastCollection Telecasts)
        {
            tTxdbLink<string> Link = TxdbTitleLinks.Find(stvTitle);
            // wenn Link bereits definiert, nicht mehr automatisch akzeptieren
            bool autoResult = (Link.Status != TxdbLinkStatusOptions.Defined);

            // Tmdb abfragen
            fmAddMovie addMovie = new fmAddMovie(stvTitle);
            addMovie.Search(autoResult);

            switch (addMovie.DialogResult)
            {
                case DialogResult.Cancel:
                    if (Link.Status == TxdbLinkStatusOptions.Undefined) Link.Status = TxdbLinkStatusOptions.Ignored;
                    break;

                case DialogResult.Ignore:
                    if (Link.IsDefined)
                    {
                        local.Remove(local.Movies.Find(Link.ID), false);
                    }
                    TxdbClear(Telecasts);
                    Link.Status = TxdbLinkStatusOptions.Ignored;
                    break;

                case DialogResult.OK:
                    tMovie newMovie = addMovie.Movie();
                    if (newMovie != local.Movies.Find(Link.ID))
                    {
                        if (Link.IsDefined)
                        {
                            local.Remove(local.Movies.Find(Link.ID), false);
                        }
                        local.Movies.Add(newMovie);
                    }
                    Telecasts.ForEach(telecast => telecast.tmdbMovieID = newMovie.ID);
                    Link.ID = newMovie.ID;
                    break;
            }

            return (addMovie.DialogResult != DialogResult.Cancel);
        }

        /// <summary>
        /// Telecast mit Serie aus TheTVDB verknüpfen
        /// </summary>
        /// <param name="stvTitle">Titel der Sendung im Save.TV Archiv</param>
        /// <param name="Telecasts">Sendungen zu diesem Titel</param>
        /// <returns>false, wenn der User den Vorgang abbrechen will</returns>
        private async Task<bool> TxdbLinkShow(string stvTitle, tTelecastCollection Telecasts)
        {
            tTxdbLink<string> Link = TxdbTitleLinks.Find(stvTitle);
            // wenn Link bereits definiert, nicht mehr automatisch akzeptieren
            bool autoResult = (Link.Status != TxdbLinkStatusOptions.Defined);

            TvdbShow addShow = new TvdbShow
            {
                BasePath = local.ShowsBasePath,
                KnownShows = local.Shows,
                HideIgnoreAll = false,
                HideFolderName = !settings.UseLocalArchive
            };
            await addShow.SearchFromStvTitle(stvTitle, autoResult);

            switch (addShow.DialogResult)
            {
                case DialogResult.Cancel:
                    if (Link.Status == TxdbLinkStatusOptions.Undefined) Link.Status = TxdbLinkStatusOptions.Ignored;
                    break;

                case DialogResult.Ignore:
                    if (Link.IsDefined)
                    {
                        local.Remove(local.Shows.Find(Link.ID), false);
                    }
                    TxdbClear(Telecasts);
                    Link.Status = TxdbLinkStatusOptions.Ignored;
                    break;

                case DialogResult.OK:
                    tShow Show = addShow.Show();

                    if (Show != local.Shows.Find(Link.ID))
                    {
                        if (Link.IsDefined)
                        {
                            local.Remove(local.Shows.Find(Link.ID), false);
                        }
                        local.Shows.Add(Show);

                        // Episoden in lokalem Archiv aktualisieren
                        local.Episodes.RemoveAll(episode => episode.ShowID == Show.ID);
                        local.Episodes.AddRange(addShow.Episodes());
                        if (Show.Foldername != "")
                        {
                            local.ReadEpisodeFolder(Show, Show.Foldername);
                        }
                    }

                    bool Cancel = false;
                    foreach (tTelecast Telecast in Telecasts)
                    {
                        Telecast.Category = Categories.Series; // Save.TV Info-Serien ebenfalls als Serien definieren
                        Telecast.tvdbShowID = Show.ID;
                        Cancel = !TxbdLinkEpisode(Telecast, false);
                        if (Cancel) break;
                    }
                    Link.ID = Show.ID;
                    break;
            }

            return (addShow.DialogResult != DialogResult.Cancel);
        }

        /// <summary>
        /// Telecast mit Serienepisode aus TheTVDB verknüpfen
        /// </summary>
        /// <param name="Telecast">Telecast</param>
        /// <param name="IgnoreIfUnsure">bei True werden unsichere Episoden auf Ignorieren gesetzt, bei False wird ein Auswahldialog gezeigt</param>
        /// <returns>false, wenn der Benutzer den Vorgang abbrechen will</returns>
        /// 
        private bool TxbdLinkEpisode(tTelecast Telecast, bool IgnoreIfUnsure)
        {
            tTxdbLink<int> link = TxdbTelecastIdLinks.Find(Telecast.ID);
            if (link.EpisodeID != 0)
            {
                Telecast.tvdbEpisodeID = link.EpisodeID;
                return true;
            }
            else
            {
                tShow localShow = local.Show(Telecast.tvdbShowID);
                tEpisodeCollection localEpisodes = local.Episodes.Show(localShow);

                tEpisode localEpisode = new tEpisode();
                bool found = false;
                bool cancel = IgnoreIfUnsure;

                // Season und Episode bei STV vorhanden
                if (Telecast.Season != 0)
                {
                    localEpisode = localEpisodes.Find(Telecast.Season, Telecast.Episode);
                    found = (localEpisode != null);
                }

                // Subject bei STV vorhanden
                if (!found && Telecast.Subject != "")
                {
                    localEpisode = localEpisodes.FuzzyFind(Telecast.Subject, 0.70F);
                    found = (localEpisode != null);
                }

                // Subtitle sollten immer vorhanden sein
                if (!found && Telecast.SubTitle != "")
                {
                    localEpisode = localEpisodes.FuzzyFind(Telecast.SubTitle, 0.70F);
                    found = (localEpisode != null);

                    // immer noch kein Match -> Subtitle u.U. nur Teilstring des TVDB-Eintrags (zB Tatort)
                    if (!found)
                    {
                        localEpisode = localEpisodes.Find(episode => episode.Title.Contains(Telecast.SubTitle));
                        found = (localEpisode != null);
                    }

                    // Letzte Rettung, setze Schwelle auf Null und zeige Auswahldialog zur Bestätigung
                    if (!found)
                    {
                        if (!IgnoreIfUnsure)
                        {
                            localEpisode = localEpisodes.FuzzyFind(Telecast.SubTitle, 0);

                            fmTvdbEpisodes tvdbChangeEpisode = new fmTvdbEpisodes(Telecast, localShow, localEpisode);
                            switch (tvdbChangeEpisode.ShowDialog())
                            {
                                case DialogResult.OK:
                                    if (tvdbChangeEpisode.ReloadFlag)
                                    {
                                        local.Episodes.RemoveAll(episode => episode.ShowID == localShow.ID);
                                        local.Episodes.AddRange(tvdbChangeEpisode.Episodes());
                                    }
                                    localEpisode = tvdbChangeEpisode.Episode;
                                    found = true;
                                    break;

                                case DialogResult.Ignore:
                                    break;

                                case DialogResult.Cancel:
                                    cancel = true;
                                    break;
                            }
                        }
                    }
                }

                Telecast.tvdbEpisodeID = found ? localEpisode.ID : 0;
                link.EpisodeID = Telecast.tvdbEpisodeID;
                return (!cancel);
            }
        }

        private void TxdbDelete(tTxdbLinkCollection<string> Links)
        {
            foreach (tTxdbLink<string> Link in Links)
            {
                if (Link.IsDefined)
                {
                    switch (Link.Type)
                    {
                        case TxdbLinkTypes.Movie:
                            local.Movies.RemoveAll(movie => movie.ID == Link.ID);
                            break;

                        case TxdbLinkTypes.Show:
                            local.Shows.RemoveAll(show => show.ID == Link.ID);
                            local.Episodes.RemoveAll(episode => episode.ShowID == Link.ID);
                            break;
                    }
                    TxdbClear(stv.Telecasts.GetByTitle(Link.Key));
                }
                TxdbTitleLinks.Remove(Link);
            }
        }

        private void TxdbIgnore(tTxdbLinkCollection<string> Links)
        {
            foreach (tTxdbLink<string> Link in Links)
            {
                Link.Status = TxdbLinkStatusOptions.Ignored;
                TxdbClear(stv.Telecasts.GetByTitle(Link.Key));
            }
        }

        private void TvdbChangeEpisode(IEnumerable<tTelecast> Telecasts)
        {
            foreach (tTelecast Telecast in Telecasts)
            {
                tShow localShow = local.Show(Telecast.tvdbShowID);
                tEpisode Episode = local.Episode(Telecast.tvdbEpisodeID);
                tTxdbLink<int> link = TxdbTelecastIdLinks.Find(Telecast.ID);

                bool Cancel = false;

                fmTvdbEpisodes tvdbChangeEpisode = new fmTvdbEpisodes(Telecast, localShow, Episode);
                switch (tvdbChangeEpisode.ShowDialog())
                {
                    case DialogResult.OK:
                        if (tvdbChangeEpisode.ReloadFlag)
                        {
                            local.Episodes.RemoveAll(episode => episode.ShowID == localShow.ID);
                            local.Episodes.AddRange(tvdbChangeEpisode.Episodes());
                        }
                        Telecast.tvdbEpisodeID = tvdbChangeEpisode.Episode.ID;
                        link.SetEpisode(Telecast.tvdbShowID, tvdbChangeEpisode.Episode.ID);
                        break;

                    case DialogResult.Ignore:
                        Telecast.tvdbEpisodeID = 0;
                        link.EpisodeID = 0;
                        break;

                    case DialogResult.Cancel:
                        Cancel = true;
                        break;
                }
                if (Cancel) break;
                CheckDuplicates(new tTelecastCollection(stv.Telecasts.Where(tc => tc.tvdbShowID == localShow.ID)));
            }
            StvTreeItemSelected();
        }

        private void StvTreeRefresh()
        {
            tvStvTree.Nodes.Clear();
            StvDetailUpdate(new tTelecast());

            tvStvTree.BeginUpdate();
            TreeNode baseNode = tvStvTree.Nodes.Add("Alle");

            tTelecastCollection Telecasts = new tTelecastCollection();
            if (toolStvShowVideoArchive.Checked) { Telecasts.AddRange(stv.VideoArchive.Where(telecast => !telecast.suspectBroken | settings.StvShowBroken)); }
            if (toolStvShowProgrammed.Checked) { Telecasts.AddRange(stv.Programmed.Where(telecast => !telecast.suspectBroken | settings.StvShowBroken)); }

            //if (toolStvShowVideoArchive.Checked & toolStvShowProgrammed.Checked) { Telecasts = stv.Telecasts; }
            //else if (toolStvShowVideoArchive.Checked) { Telecasts = stv.VideoArchive; }
            //else if (toolStvShowProgrammed.Checked) { Telecasts = stv.Programmed; }

            switch ((string)toolStvListSort.Tag)
            {
                case "title":
                    baseNode.Text = "Alle";
                    foreach (string title in Telecasts.Titles)
                    {
                        baseNode.Nodes.Add(title);
                    }
                    break;

                case "lasttelecast":
                    baseNode.Text = "Alle";
                    foreach (string title in Telecasts.TitlesByLastTelecast)
                    {
                        baseNode.Nodes.Add(title);
                    }
                    break;

                case "type":
                    baseNode.Text = "Alle";

                    if (Telecasts.Contains(Categories.Movie))
                    {
                        baseNode.Nodes.Add(Categories.Movie.ToDescription());
                    }

                    if (Telecasts.Contains(Categories.Series))
                    {
                        TreeNode showNode = new TreeNode(Categories.Series.ToDescription());
                        foreach (string title in Telecasts.GetByCategory(Categories.Series).Titles)
                        {
                            showNode.Nodes.Add(title);
                        }
                        baseNode.Nodes.Add(showNode);
                    }

                    if (Telecasts.Contains(Categories.Info))
                    {
                        TreeNode infoNode = new TreeNode(Categories.Info.ToDescription());
                        foreach (string title in Telecasts.GetByCategory(Categories.Info).Titles)
                        {
                            infoNode.Nodes.Add(title);
                        }
                        baseNode.Nodes.Add(infoNode);
                    }

                    if (Telecasts.Contains(Categories.Other))
                    {
                        baseNode.Nodes.Add(Categories.Other.ToDescription());
                    }

                    break;

                case "date":
                    baseNode.Text = "Alle Daten";
                    foreach (DateTime date in Telecasts.Dates)
                    {
                        TreeNode dateNode = new TreeNode(date.ToShortDateString());
                        dateNode.Tag = date;
                        baseNode.Nodes.Add(dateNode);
                    }
                    break;
            }

            baseNode.ExpandAll();
            tvStvTree.EndUpdate();
            Application.DoEvents();
            tvStvTree.SelectedNode = baseNode;
            StatusStripUpdate();
        }

        private void StvTreeItemSelected()
        {
            TreeNode selectedNode = tvStvTree.SelectedNode;

            tTelecastCollection stvList = new tTelecastCollection();
            if (toolStvShowVideoArchive.Checked & toolStvShowProgrammed.Checked) { stvList = stv.Telecasts; }
            else if (toolStvShowVideoArchive.Checked) { stvList = stv.VideoArchive; }
            else if (toolStvShowProgrammed.Checked) { stvList = stv.Programmed; }

            if (selectedNode.Level == 0)
            {
                StvListRefresh(stvList);
            }
            else
            {
                switch ((string)toolStvListSort.Tag)
                {
                    case "type":
                        {
                            if (selectedNode.Text == Categories.Movie.ToDescription())
                            {
                                StvListRefresh(stvList.GetByCategory(Categories.Movie));
                            }

                            else if (selectedNode.Text == Categories.Series.ToDescription())
                            {
                                StvListRefresh(stvList.GetByCategory(Categories.Series));
                            }
                            else if (selectedNode.Text == Categories.Info.ToDescription())
                            {
                                StvListRefresh(stvList.GetByCategory(Categories.Info));
                            }
                            else if (selectedNode.Text == Categories.Other.ToDescription())
                            {
                                StvListRefresh(stvList.GetByCategory(Categories.Other));
                            }
                            else
                            {
                                StvListRefresh(stvList.GetByTitle(selectedNode.Text));
                            }
                            break;
                        }

                    case "date":
                        StvListRefresh(stvList.GetByDate((DateTime)selectedNode.Tag));
                        break;

                    case "title":
                    case "lasttelecast":
                        StvListRefresh(stvList.GetByTitle(selectedNode.Text));
                        break;
                }

            }
        }

        private void tvStvTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            StvTreeItemSelected();
        }

        private void StvListRefresh(List<tTelecast> Telecasts)
        {
            List<tTelecast> displayTelecasts = new List<tTelecast>();

            foreach (tTelecast Telecast in Telecasts)
            {
                bool isLocalAvailable =
                    local.Episode(Telecast.tvdbEpisodeID).Status == TelecastStatus.InLocalArchive |
                    local.Movie(Telecast.tmdbMovieID).Status == TelecastStatus.InLocalArchive;

                if (
                    (settings.StvShowDuplicates | !Telecast.Duplicate) &&
                    ((settings.StvShowHasAdfree & Telecast.AdFree) | (settings.StvShowHasNoAdfree & !Telecast.AdFree)) &&
                    (settings.StvShowBroken | !Telecast.suspectBroken) &&
                    (settings.StvShowLocalAvailable | !isLocalAvailable) &&
                    (toolStvShowAllRecords.Checked | (toolStvShowManualRecords.Checked & Telecast.recordManual) | (toolStvShowChannelRecords.Checked & Telecast.recordChannel))
                    )
                { displayTelecasts.Add(Telecast); }
            }

            olvStvList.ClearObjects();
            olvStvList.SetObjects(displayTelecasts);
            if (olvStvList.GetItemCount() > 0) { olvStvList.SelectedIndex = olvStvList.TopItemIndex; }
        }

        private void olvStvList_SelectionChanged(object sender, EventArgs e)
        {
            StvDetailUpdate(olvStvList.SelectedObject as tTelecast);
        }

        private void StvDetailUpdate(tTelecast item)
        {
            if (item == null) { item = new tTelecast(); }
            if (item.ID.ToString() != boxStvDetail.Tag)
            {
                boxStvDetail.Tag = item.ID.ToString();
                string imageURL = "";
                switch (item.Category)
                {
                    case Categories.Series:
                        if (item.tvdbEpisodeID > 0)
                        {
                            tbStvPublicText.Text = local.Episode(item.tvdbEpisodeID).Summary;
                        }
                        else
                        {
                            tbStvPublicText.Text = item.PublicText;
                        }

                        picStvImage.Image = null;
                        imageURL = local.Episode(item.tvdbEpisodeID).ImageURL;
                        if (string.IsNullOrEmpty(imageURL))
                        {
                            imageURL = item.ImageURL;
                        }

                        if (imageURL != "")
                        {
                            picStvImage.LoadAsync(imageURL);
                        }
                        toolTmdbChangeMovie.Enabled = false;
                        toolTvdbChangeShow.Enabled = true;
                        toolTvdbChangeEpisode.Enabled = item.tvdbShowID != 0;
                        toolTvdbIgnore.Enabled = item.tvdbShowID > 0;
                        stvShowUpdate(item.tvdbShowID);
                        break;

                    case Categories.Movie:
                        if (item.tmdbMovieID > 0)
                        {
                            tbStvPublicText.Text = local.Movie(item.tmdbMovieID).Summary;
                        }
                        else
                        {
                            tbStvPublicText.Text = item.PublicText;
                        }

                        picStvImage.Image = null;
                        imageURL = local.Movie(item.tmdbMovieID).ImageURL;
                        if (string.IsNullOrEmpty(imageURL))
                        {
                            imageURL = item.ImageURL;
                        }

                        if (imageURL != "")
                        {
                            picStvImage.LoadAsync(imageURL);
                        }
                        toolTmdbChangeMovie.Enabled = true;
                        toolTvdbChangeShow.Enabled = false;
                        toolTvdbChangeEpisode.Enabled = false;
                        toolTvdbIgnore.Enabled = item.tmdbMovieID > 0;
                        stvShowUpdate(-1);
                        break;

                    case Categories.Unknown:
                        tbStvPublicText.Text = "";
                        picStvImage.Image = null;
                        stvShowUpdate(-1);
                        break;

                    default:
                        stvShowUpdate(-1);
                        tbStvPublicText.Text = item.PublicText;
                        if (item.ImageURL != "") picStvImage.LoadAsync(item.ImageURL);

                        toolTmdbChangeMovie.Enabled = true;
                        toolTvdbChangeShow.Enabled = true;
                        toolTvdbChangeEpisode.Enabled = false;
                        toolTvdbIgnore.Enabled = false;
                        break;
                }
            }
        }

        private void stvShowUpdate(int TvdbShowID)
        {

            if (TvdbShowID < 0)
            {
                boxTvdbLink.Enabled = false;
                tbStvTvdbLinkShow.Text = "";
                tbStvTvdbLinkLocalFolder.Text = "";
                lbStvTvdbLinkLocalEpisodes.Text = "0 / 0 Episoden im lokalen Archiv vorhanden";
            }
            else
            {
                boxTvdbLink.Enabled = true;
                tShow Show = local.Show(TvdbShowID);
                tEpisodeCollection Episodes = local.Episodes.Show(Show);
                tbStvTvdbLinkShow.Text = Show.Title;
                tbStvTvdbLinkLocalFolder.Text = Show.Foldername;
                lbStvTvdbLinkLocalEpisodes.Text = Episodes.Local().Count().ToString() + "/" + Episodes.Count().ToString() + " Episoden im lokalen Archiv vorhanden";
            }
        }

        private void StvTelecastsDelete(IEnumerable<tTelecast> Telecasts)
        {
            if (Telecasts.Any())
            {
                tTelecastCollection telecasts = new tTelecastCollection(Telecasts);
                tTelecastCollection telecastsWithDuplicates = new tTelecastCollection();
                foreach (tTelecast telecast in telecasts)
                {
                    telecastsWithDuplicates.AddOrRefreshRange(stv.Telecasts.Duplicates(telecast));
                }

                fmDelete Delete = new fmDelete(telecasts, telecastsWithDuplicates);
                if (Delete.ShowDialog() == DialogResult.OK)
                {
                    if (Delete.DeleteDuplicates) { stv.DeleteRecords(telecastsWithDuplicates); }
                    else { stv.DeleteRecords(telecasts); }
                }
            }
        }

        private async Task StvTelecastsDownload(IEnumerable<tTelecast> Telecasts, RecordFormats Format)
        {
            if (settings.StvDownloadMethod == DownloadMethods.JDownloader && settings.JDLPluginMode & settings.JDLFullService)
            {
                JdlWrapper jdl = new JdlWrapper { Referer = "STV MANAGER" };
                jdl.taskCreate(
                    Telecasts.Select(telecast => "https://www.save.tv/STV/M/obj/archive/VideoArchiveDetails.cfm?TelecastId=" + telecast.ID.ToString()),
                    Telecasts.Select(telecast => telecast.Title + " - " + telecast.SubTitle),
                    "");
            }
            else if (!Directory.Exists(downloader.DestinationPath))
            {
                MessageBox.Show("Downloadverzeichnis " + downloader.DestinationPath + " ist nicht erreichbar.\r\nBitte Einstellungen überprüfen.", "Download Starten", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                tabControl1.SelectedTab = tabSettings;
                tvSettings.SelectedNode = tvSettings.Nodes["nodeDownloads"];
                tbDownloadPath.Focus();
            }
            else
            {
                tDownloadCollection Downloads = new tDownloadCollection();
                foreach (tTelecast Telecast in Telecasts)
                {
                    tDownload download = new tDownload(Telecast);

                    // detaillierten Dateinamen setzen
                    switch (download.Category)
                    {
                        case Categories.Series:
                            if (download.tvdbEpisodeID > 0)
                            {
                                download.localFilename = local.GetDefaultFilename(download.tvdbShowID, download.tvdbEpisodeID) + ".MP4";
                            }
                            break;

                        case Categories.Movie:
                            if (download.tmdbMovieID > 0)
                            {
                                download.localFilename = local.GetDefaultFilename(download.tmdbMovieID) + ".MP4";
                            }
                            break;
                    }

                    download.AdFree = toolDownloadAdFree.Checked;
                    download.Format = Format;
                    if (Format == RecordFormats.HD & Telecast.hasHD == false) download.Format = RecordFormats.SD;
                    download.Status = DownloadStatus.Queued;
                    Downloads.Add(download);
                }

                downloader.AddRange(Downloads);
                Application.DoEvents(); //sicherstellen, dass alle UI-Vorgänge abgeschlossen sind, bevor die asynchrone Abfrage gestartet wird?

                IEnumerable<tDownload> allDownloads = await stv.GetDownloadLinksParallel(Downloads);
                IEnumerable<tDownload> runDownloads = allDownloads.Where(download => download.Status > DownloadStatus.Error);

                List<Task> allTasks = new List<Task>();
                foreach (tDownload download in runDownloads)
                {
                    if (string.IsNullOrEmpty(download.stvDownloadFilename))
                    {
                        allTasks.Add(stv.GetDownloadFilename(download));
                    }
                    download.Status = DownloadStatus.Submitting;
                }
                await Task.WhenAll(allTasks);
                downloader.RunRange(runDownloads);
            }
        }

        private async Task StvTelecastsStream(IEnumerable<tTelecast> Telecasts, RecordFormats Format)
        {
            tDownloadCollection Downloads = new tDownloadCollection();
            foreach (tTelecast Telecast in Telecasts.Take(1))
            {
                tDownload download = new tDownload(Telecast);
                download.AdFree = toolDownloadAdFree.Checked;
                download.Format = Format;
                if (Format == RecordFormats.HD & Telecast.hasHD == false) download.Format = RecordFormats.SD;
                download.Status = DownloadStatus.Queued;
                Downloads.Add(download);
            }

            IEnumerable<tDownload> Streams = await stv.GetDownloadLinksParallel(Downloads);

            String Player = settings.StreamingPlayerPath;
            String Stream = Streams.Where(download => download.Status > DownloadStatus.Error).First().stvStreamingURL;

            System.Diagnostics.Process process = new System.Diagnostics.Process();
            if (Player == "")
            {
                process.StartInfo.FileName = Stream;
            }
            else
            {
                process.StartInfo.FileName = Player;
                process.StartInfo.Arguments = Stream;
            }
            process.Start();
        }

        private void StvAutoDownload()
        {
            IEnumerable<tTelecast> newTelecasts = stv.VideoArchive.GetById(downloader.Telecasts.GetNotDownloadedTelecastIds());
            OnLogEvent(new LogEventArgs(newTelecasts.ToString() + " neue Sendungen zum automatischen Download gefunden."));

            if (newTelecasts.Count() > 0)
            {
                if (settings.AutoDownloadAwaitAdFree)
                {
                    newTelecasts = newTelecasts.Where(tc => tc.AdFree == true);
                }
                if (settings.AutoDownloadSkipDuplicates)
                {
                    newTelecasts = newTelecasts.Where(tc => tc.Duplicate == false);
                }
                OnLogEvent(new LogEventArgs("Automatischer Download " + String.Join(",", newTelecasts.Select(tc => tc.ID))));
                StvTelecastsDownload(newTelecasts, settings.StvDefaultVideoFormat);
            }
        }

        private IEnumerable<tTelecast> StvListGetSelectedItems(string Message)
        {
            if (olvStvList.SelectedObjects.Count == 0)
            {
                MessageBox.Show("Keine Sendung ausgewählt", Message, MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
            return olvStvList.SelectedObjects.Cast<tTelecast>();
        }

        private void toolStvDelete_Click(object sender, EventArgs e)
        {
            StvTelecastsDelete(StvListGetSelectedItems("Sendungen löschen"));
        }

        private void toolDownloadDefault_ButtonClick(object sender, EventArgs e)
        {
            StvTelecastsDownload(StvListGetSelectedItems("Sendungen herunterladen"), settings.StvDefaultVideoFormat);
        }

        private void toolDownloadMobile_Click(object sender, EventArgs e)
        {
            StvTelecastsDownload(StvListGetSelectedItems("Sendungen herunterladen"), RecordFormats.Mobile);
        }

        private void toolDownloadHQ_Click(object sender, EventArgs e)
        {
            StvTelecastsDownload(StvListGetSelectedItems("Sendungen herunterladen"), RecordFormats.SD);
        }

        private void toolDownloadHD_Click(object sender, EventArgs e)
        {
            StvTelecastsDownload(StvListGetSelectedItems("Sendungen herunterladen"), RecordFormats.HD);
        }

        private void toolDownloadAll_Click(object sender, EventArgs e)
        {
            StvAutoDownload();
        }

        private void toolStream_Click(object sender, EventArgs e)
        {
            StvTelecastsStream(StvListGetSelectedItems("Sendungen streamen"), settings.StvDefaultVideoFormat);
        }

        private void cbStvSavePassword_CheckedChanged(object sender, EventArgs e)
        {
            if (!cbStvSavePassword.Checked)
            {
                tbStvPassword.Text = "";
            }
            tbStvPassword.Enabled = cbStvSavePassword.Checked;
        }

        private void cbSynoSavePassword_CheckedChanged(object sender, EventArgs e)
        {
            if (!cbSynoSavePassword.Checked)
            {
                tbSynoPassword.Text = "";
            }
            tbSynoPassword.Enabled = (cbSynoSavePassword.Checked & rbDownloadSynology.Checked);
        }

        private void rbDownloadMethod_CheckedChanged(object sender, EventArgs e)
        {
            UseDiskStation(rbDownloadSynology.Checked);
            UseJDL(rbDownloadJDL.Checked);
            UseInternalDlm(rbDownloadInternal.Checked);
            UseExternalDlm(rbDownloadExternal.Checked);
        }

        private void ListView_SortByColumn(object sender, ColumnClickEventArgs e)
        {
            ListViewColumnSorter sort = (sender as ListView).ListViewItemSorter as ListViewColumnSorter;

            // Determine if clicked column is already the column that is being sorted.
            if (e.Column == sort.SortColumn)
            {
                // Reverse the current sort direction for this column.
                if (sort.Order == SortOrder.Ascending)
                {
                    sort.Order = SortOrder.Descending;
                }
                else
                {
                    sort.Order = SortOrder.Ascending;
                }
            }
            else
            {
                sort.SortColumn = e.Column;
                sort.Order = SortOrder.Ascending;
            }

            (sender as ListView).Sort();
        }

        private void OnDownloadUpdateEvent(DownloadUpdateEventArgs e)
        {
            switch (e.UpdateEvent)
            {
                case DownloadUpdateEvents.Removed:
                    DownloadListRemove(e.Download);
                    break;

                case DownloadUpdateEvents.ProgressChanged:
                case DownloadUpdateEvents.FilenameChanged:
                    DownloadListItemUpdate(e.Download);
                    break;

                case DownloadUpdateEvents.StatusChanged:
                    DownloadListItemUpdate(e.Download);
                    tTelecast Telecast = stv.Telecasts.GetById(e.Download.TelecastID);
                    if (Telecast != null)
                    {
                        switch (e.Download.Status)
                        {
                            case DownloadStatus.Error:
                            case DownloadStatus.Cancelled:
                                if (Telecast.Status < TelecastStatus.DownloadFinished)
                                {
                                    Telecast.Status = TelecastStatus.OnStvServer;
                                }
                                break;

                            case DownloadStatus.Waiting:
                            case DownloadStatus.Progressing:
                                if (Telecast.Status < TelecastStatus.Downloading)
                                {
                                    Telecast.Status = TelecastStatus.Downloading;
                                }
                                break;

                            case DownloadStatus.Finished:
                                Telecast.Status = TelecastStatus.DownloadFinished;
                                break;

                            case DownloadStatus.Renamed:
                                Telecast.Status = TelecastStatus.DownloadRenamed;
                                break;

                            case DownloadStatus.MovedToArchive:
                                Telecast.Status = TelecastStatus.InLocalArchive;
                                break;
                        }
                    }
                    break;
            }
        }

        private void DownloadListRemove(tDownload Download)
        {
            ListViewItem found = lvDownloads.Items.Cast<ListViewItem>().FirstOrDefault(item => item.Tag == Download);
            lvDownloads.Items.Remove(found);
        }

        private void DownloadListItemUpdate(tDownload Download)
        {
            ListViewItem found = lvDownloads.Items.Cast<ListViewItem>().FirstOrDefault(item => item.Tag == Download);

            if (found == null) // not found
            {
                found = new ListViewItem(Download.TelecastID.ToString());
                found.SubItems.Add(Download.stvDownloadFilename).Name = "stvFilename";
                found.SubItems.Add(Download.localFilename).Name = "localFilename";
                found.SubItems.Add("").Name = "status"; // empty status field
                found.Tag = Download;
                lvDownloads.Items.Add(found);
            }
            string progress = Download.Received.ToString() + " / " + Download.Size.ToString() + " MB (" + Download.ProgressPercent.ToString() + " %)";
            string status = (Download.Status == DownloadStatus.Progressing) ? progress : "";
            status += Download.Status.ToDescription();
            found.SubItems["stvFilename"].Text = Download.stvDownloadFilename;
            found.SubItems["localFilename"].Text = Download.localFilename;
            found.SubItems["status"].Text = status;
        }

        private void DownloadListRefresh()
        {
            lvDownloads.Items.Clear();
            lvDownloads.BeginUpdate();
            foreach (tDownload download in downloader.ActiveDownloads)
            {
                string progress = download.Received.ToString() + " / " + download.Size.ToString() + " MB (" + download.ProgressPercent.ToString() + " %)";
                string status = (download.Status == DownloadStatus.Progressing) ? progress : "";
                status += download.Status.ToDescription();

                ListViewItem item = new ListViewItem(download.TelecastID.ToString());
                item.SubItems.Add(download.stvDownloadFilename).Name = "stvFilename";
                item.SubItems.Add(download.localFilename).Name = "localFilename";
                item.SubItems.Add(status).Name = "status";
                item.Tag = download;
                lvDownloads.Items.Add(item);
            }
            lvDownloads.EndUpdate();
        }

        private void DownloadsRemove(IEnumerable<tDownload> Downloads)
        {
            List<tDownload> activeDownloads = Downloads.Where(download => download.Status == DownloadStatus.Submitting | download.Status == DownloadStatus.Waiting | download.Status == DownloadStatus.Progressing).ToList();
            string cancelMessage = downloader.CanCancel ? "werden abgebrochen" : "in externen Downloadmanagern";
            cancelMessage = activeDownloads.Count.ToString() + " aktive Downloads " + cancelMessage + ". Einträge trotzdem entfernen?";

            if (!activeDownloads.Any() ||
                MessageBox.Show(cancelMessage, "Downloads entfernen", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
            {
                downloader.RemoveRange(Downloads);
            }
        }

        private void DownloadsCancel(IEnumerable<tDownload> Downloads)
        {
            downloader.CancelRange(Downloads);
        }

        private void DownloadsRestart(IEnumerable<tDownload> Downloads)
        {
            downloader.CancelRange(Downloads);
            stv.GetDownloadLinksParallel(Downloads);
        }

        private void DownloadsRename(IEnumerable<tDownload> Downloads)
        {
            foreach (tDownload Download in Downloads)
            {
                if (Download.Status == DownloadStatus.Finished &&
                    !string.IsNullOrEmpty(Download.localFilename))
                {
                    string stvFilePath = Path.Combine(settings.LocalPathDownloads, Download.stvDownloadFilename);
                    string downloadFilePath = Path.Combine(settings.LocalPathDownloads, Download.localFilename);
                    if (File.Exists(stvFilePath) && FileEx.Move(stvFilePath, downloadFilePath, "Download umbenennen"))
                    {
                        Download.Status = DownloadStatus.Renamed;
                    }
                }
            }
        }

        private void DownloadsToLocal(IEnumerable<tDownload> Downloads)
        {
            DownloadsRename(Downloads.Where(download => download.Status == DownloadStatus.Finished));

            bool moved = false;
            string sshPassword = "";
            bool doSSH = settings.StvDownloadMethod == DownloadMethods.Synology && settings.SynoUseSSH;

            foreach (tDownload dl in Downloads)
            {
                if (dl.localFilename != "")
                {
                    string dlPath = doSSH ? DirectoryHelper.GetRealPath(settings.LocalPathDownloads) : settings.LocalPathDownloads;
                    string stvFilePath = Path.Combine(dlPath, dl.stvDownloadFilename);
                    string downloadFilePath = Path.Combine(dlPath, dl.localFilename);

                    string DestinationPath = "";
                    switch (dl.Category)
                    {
                        case Categories.Series:
                            DestinationPath = dl.tvdbShowID != 0 ?
                                local.Show(dl.tvdbShowID).Foldername : Path.Combine(settings.LocalPathSeries, stv.Telecasts.GetById(dl.TelecastID).Title);
                            break;

                        case Categories.Movie:
                            DestinationPath = settings.LocalPathMovies;
                            break;

                        case Categories.Info:
                            DestinationPath = Path.Combine(settings.LocalPathInfos, stv.Telecasts.GetById(dl.TelecastID).Title);
                            break;

                        case Categories.Other:
                            DestinationPath = settings.LocalPathOther;
                            break;
                    }
                    string Destination = Path.Combine(DestinationPath, dl.localFilename);

                    if (File.Exists(downloadFilePath) && DestinationPath != "")
                    {
                        if (doSSH)
                        {
                            DestinationPath = DirectoryHelper.GetRealPath(DestinationPath);
                            if (sshPassword == "")
                            {
                                fmLogin synoSshLogin = new fmLogin("DiskStation SSH Login")
                                {
                                    Username = "admin",
                                    UsernameReadonly = true,
                                    SavePassword = false,
                                    OfferSavePassword = false
                                };
                                sshPassword = (synoSshLogin.ShowDialog() == System.Windows.Forms.DialogResult.OK) ? synoSshLogin.Password : "";
                            }
                            if (sshPassword != "")
                            {
                                apiDownloadStation syno = new apiDownloadStation() { Address = settings.SynoServerURL };
                                moved = syno.sshMove(downloadFilePath, DestinationPath, sshPassword);
                            }
                        }
                        else if ((Path.GetPathRoot(Destination) == Path.GetPathRoot(downloadFilePath)) ||
                            (MessageBox.Show("Datei wird auf ein anderes Laufwerk verschoben. Fortfahren?",
                            "STV MANAGER", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK))
                        {
                            if (!Directory.Exists(DestinationPath)) Directory.CreateDirectory(DestinationPath);
                            moved = FileEx.Move(downloadFilePath, Destination, "Download in lokales TV-Archiv verschieben");
                        }

                        if (moved)
                        {
                            if (dl.tvdbEpisodeID > 0)
                            {
                                tEpisode localEpisode = local.Episode(dl.tvdbEpisodeID);
                                localEpisode.Filename = Destination;
                                localEpisode.isHd = dl.Format == RecordFormats.HD;
                            }
                            else if (dl.tmdbMovieID > 0) { local.Movie(dl.tmdbMovieID).Filename = Destination; }
                            dl.Status = DownloadStatus.MovedToArchive;
                        }
                    }
                }
            }

            if (moved)
            {
                downloader.RemoveRange(Downloads.Where(download => download.Status == DownloadStatus.MovedToArchive));

                if (settings.UseXbmc)
                {
                    apiXbmc xbmc = new apiXbmc
                    {
                        Address = settings.XbmcUrl,
                        Port = settings.XbmcPort,
                        Username = settings.XbmcUser,
                        Password = settings.XbmcPass
                    };
                    if (!xbmc.VideoLibraryScan())
                    {
                        MessageBox.Show("Aktualisierung der Kodi Datenbank nicht erfolgreich. Läuft Kodi und stimmen die Zugangsdaten?");
                    }
                }
            }
        }

        private IEnumerable<tDownload> DownloadListGetSelectedItems()
        {
            return lvDownloads.SelectedItems.Cast<ListViewItem>().Select(item => item.Tag as tDownload);
        }

        private void toolDownloadListRemove_Click(object sender, EventArgs e)
        {
            DownloadsRemove(DownloadListGetSelectedItems());
        }

        private void toolDownloadCancel_Click(object sender, EventArgs e)
        {
            DownloadsCancel(DownloadListGetSelectedItems());
        }

        private void toolDownloadRestart_Click(object sender, EventArgs e)
        {
            DownloadsRestart(DownloadListGetSelectedItems());
        }

        private void toolRenameDownload_Click(object sender, EventArgs e)
        {
            DownloadsRename(DownloadListGetSelectedItems());
        }

        private void toolDownloadToLocal_Click(object sender, EventArgs e)
        {
            if (settings.UseLocalArchive)
            {
                DownloadsToLocal(DownloadListGetSelectedItems());
            }
        }

        private void toolDownloadDeleteTelecast_Click(object sender, EventArgs e)
        {
            tTelecastCollection Telecasts = new tTelecastCollection();
            foreach (tDownload Download in DownloadListGetSelectedItems())
            {
                Telecasts.Add(stv.Telecasts.GetById(Download.TelecastID));
            }
            StvTelecastsDelete(Telecasts);
        }

        private void lvDownloads_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lvDownloads.SelectedItems.Count > 0)
            {
                tDownload dl = (tDownload)lvDownloads.SelectedItems[0].Tag;
                if (dl.tvdbShowID != 0)
                {
                    tbDownloadInfoSeries.Text = local.Show(dl.tvdbShowID).Title;
                    tbDownloadInfoSeriesFolder.Text = local.Show(dl.tvdbShowID).Foldername;
                }
                else
                {
                    tbDownloadInfoSeries.Text = (stv.Telecasts.GetById(dl.TelecastID) != null) ? stv.Telecasts.GetById(dl.TelecastID).Title : "";
                    tbDownloadInfoSeriesFolder.Text = "";
                }
                if (dl.tvdbEpisodeID != 0)
                {
                    tbDownloadInfoEpisode.Text = local.Episode(dl.tvdbEpisodeID).Title;
                    tbDownloadInfoEpisodeCode.Text = local.Episode(dl.tvdbEpisodeID).EpisodeCode(settings.LocalUseSxxExxEpisodeCode);
                }
                else
                {
                    tbDownloadInfoEpisode.Text = "";
                    tbDownloadInfoEpisodeCode.Text = "";
                }
            }
            else
            {
                tbDownloadInfoSeries.Text = "";
                tbDownloadInfoEpisode.Text = "";
                tbDownloadInfoEpisodeCode.Text = "";
                tbDownloadInfoSeriesFolder.Text = "";
            }
        }



        private void toolLocalUpdate_ButtonClick(object sender, EventArgs e)
        {
            if (settings.UseLocalArchive && Directory.Exists(settings.LocalPathSeries))
            {
                local.UpdateArchive();
                localTreeRefresh();
            }
        }

        private void toolLocalFullRefresh_Click(object sender, EventArgs e)
        {
            if (settings.UseLocalArchive && Directory.Exists(settings.LocalPathSeries))
            {
                local.Refresh();
                localTreeRefresh();
            }
        }

        private void localTreeRefresh()
        {
            if (settings.UseLocalArchive)
            {
                tvLocalTree.Nodes.Clear();
                tvLocalTree.BeginUpdate();
                TreeNode seriesNode = tvLocalTree.Nodes.Add(Categories.Series.ToDescription());

                foreach (tShow show in local.Shows)
                {
                    TreeNode titleNode = new TreeNode(show.Title);
                    titleNode.Tag = show;
                    seriesNode.Nodes.Add(titleNode);
                }

                seriesNode.Expand();

                TreeNode moviesNode = tvLocalTree.Nodes.Add(Categories.Movie.ToDescription());

                tvLocalTree.Sort();
                tvLocalTree.EndUpdate();
                tvLocalTree.SelectedNode = seriesNode;
                StatusStripUpdate();
            }
        }

        private void localListShowRefresh(tEpisodeCollection Episodes)
        {
            lvLocalListShow.Visible = true;
            lvLocalListMovie.Visible = false;

            lvLocalListShow.Items.Clear();
            lvLocalListShow.BeginUpdate();
            object keepSorter = lvLocalListShow.ListViewItemSorter;
            lvLocalListShow.ListViewItemSorter = null;
            foreach (tEpisode episode in Episodes)
            {
                if (settings.LocalShowAll || (episode.Status == TelecastStatus.InLocalArchive))
                {
                    string EpisodeCode = episode.EpisodeCode(settings.LocalUseSxxExxEpisodeCode);
                    ListViewItem item = new ListViewItem(EpisodeCode);
                    item.SubItems.Add(episode.Title);
                    item.SubItems.Add(episode.Filename);
                    item.SubItems.Add(episode.isHd ? "HD" : "SD");
                    item.Tag = episode;
                    lvLocalListShow.Items.Add(item);
                    if (episode.Filename == "")
                    {
                        item.ForeColor = Color.Gray;
                    }
                }
            }
            lvLocalListShow.ListViewItemSorter = (ListViewColumnSorter)keepSorter;
            lvLocalListShow.EndUpdate();
            picLocal.Image = null;
            tbLocalInfo.Text = "";
        }

        private void localListMovieRefresh(tMovieCollection Movies)
        {
            lvLocalListShow.Visible = false;
            lvLocalListMovie.Visible = true;

            lvLocalListMovie.Items.Clear();
            lvLocalListMovie.BeginUpdate();
            object keepSorter = lvLocalListMovie.ListViewItemSorter;
            lvLocalListMovie.ListViewItemSorter = null;
            foreach (tMovie movie in Movies)
            {
                if (settings.LocalShowAll || (movie.Status == TelecastStatus.InLocalArchive))
                {
                    ListViewItem item = new ListViewItem(movie.Title);
                    item.SubItems.Add(movie.Year.ToString());
                    item.SubItems.Add(movie.Filename);
                    item.Tag = movie;
                    lvLocalListMovie.Items.Add(item);
                    if (movie.Filename == "")
                    {
                        item.ForeColor = Color.Gray;
                    }
                }
            }
            lvLocalListMovie.ListViewItemSorter = (ListViewColumnSorter)keepSorter;
            lvLocalListMovie.EndUpdate();
            picLocal.Image = null;
            tbLocalInfo.Text = "";
        }

        private void lvLocalListShow_SelectedIndexChanged(object sender, EventArgs e)
        {
            picLocal.Image = null;
            tbLocalInfo.Text = "";

            if (lvLocalListShow.SelectedItems.Count > 0)
            {
                tEpisode Episode = lvLocalListShow.SelectedItems[0].Tag as tEpisode;
                tbLocalInfo.Text = Episode.Summary;
                if (!string.IsNullOrEmpty(Episode.ImageURL)) picLocal.LoadAsync(Episode.ImageURL);
            }
        }

        private void lvLocalListMovie_SelectedIndexChanged(object sender, EventArgs e)
        {
            picLocal.Image = null;
            tbLocalInfo.Text = "";

            if (lvLocalListMovie.SelectedItems.Count > 0)
            {
                tMovie Movie = lvLocalListMovie.SelectedItems[0].Tag as tMovie;
                tbLocalInfo.Text = Movie.Summary;
                if (!string.IsNullOrEmpty(Movie.ImageURL)) picLocal.LoadAsync(Movie.ImageURL);
            }
        }

        private void localTreeSelected()
        {
            TreeNode selectedNode = tvLocalTree.SelectedNode;

            if (selectedNode == null)
            {
                boxLocalSeriesInfo.Enabled = false;
                tbLocalSeriesInfoStv.Text = "";
                tbLocalSeriesInfoPath.Text = "";
                lbLocalSeriesCount.Text = "0 / 0 Episoden im lokalen Archiv";
                toolLocalDeleteMovie.Enabled = false;
                toolLocalDeleteShow.Enabled = false;
            }

            else if (selectedNode.Text == Categories.Movie.ToDescription())
            {
                boxLocalSeriesInfo.Enabled = false;
                tbLocalSeriesInfoStv.Text = "";
                tbLocalSeriesInfoPath.Text = "";
                lbLocalSeriesCount.Text = "0 / 0 Episoden im lokalen Archiv";
                toolLocalDeleteMovie.Enabled = true;
                toolLocalDeleteShow.Enabled = false;
                localListMovieRefresh(local.Movies);
            }

            else if (selectedNode.Text == Categories.Series.ToDescription())
            {
                boxLocalSeriesInfo.Enabled = true;
                tbLocalSeriesInfoStv.Text = "";
                tbLocalSeriesInfoPath.Text = "";
                lbLocalSeriesCount.Text = "0 / 0 Episoden im lokalen Archiv";
                toolLocalDeleteMovie.Enabled = false;
                toolLocalDeleteShow.Enabled = false;
                localListShowRefresh(local.Episodes);
            }

            else
            {
                tShow selected = (tShow)selectedNode.Tag;
                tEpisodeCollection Episodes = local.Episodes.Show(selected);
                localListShowRefresh(Episodes);
                tbLocalSeriesInfoStv.Text = string.Join(", ", TxdbTitleLinks.Where(link => link.ID == selected.ID).Select(link => link.Key));
                tbLocalSeriesInfoPath.Text = selected.Foldername;
                lbLocalSeriesCount.Text = Episodes.Local().Count().ToString() + "/" + Episodes.Count().ToString() + " Episoden im lokalen Archiv";
                toolLocalDeleteMovie.Enabled = false;
                toolLocalDeleteShow.Enabled = true;
                boxLocalSeriesInfo.Enabled = true;
            }
        }

        private void tvLocalTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            localTreeSelected();
        }

        private void toolLocalShowAll_Click(object sender, EventArgs e)
        {
            localTreeSelected();
        }

        private void toolLocalDeleteShow_Click(object sender, EventArgs e)
        {
            if (tvLocalTree.SelectedNode != null)
            {
                if (MessageBox.Show("Serie aus Liste löschen (Dateien werden nicht gelöscht)?", "STV MANAGER", MessageBoxButtons.YesNo, MessageBoxIcon.Question) ==
                    DialogResult.Yes)
                {
                    local.Remove((tShow)tvLocalTree.SelectedNode.Tag);
                    localTreeRefresh();
                }
            }
        }

        private void toolLocalDeleteMovie_Click(object sender, EventArgs e)
        {
            if (lvLocalListMovie.SelectedItems.Count > 0 &&
                MessageBox.Show("Filme aus Liste löschen (Dateien werden nicht gelöscht)?", "STV MANAGER", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                foreach (ListViewItem item in lvLocalListMovie.SelectedItems)
                {
                    local.Remove((tMovie)item.Tag);
                }
                localTreeRefresh();
            }
        }

        private void LocalEpisodeShowOption(string Option)
        {
            toolLocalShowSelect.Tag = Option;
            alleEpisodenAnzeigenToolStripMenuItem.Checked = (Option == "all");
            nurLokalVorhandeneEpisodenAnzeigenToolStripMenuItem.Checked = (Option == "local");
            settings.LocalShowAll = (Option == "all");
        }

        private void alleEpisodenAnzeigenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LocalEpisodeShowOption("all");
            localTreeSelected();
        }

        private void nurLokalVorhandeneEpisodenAnzeigenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LocalEpisodeShowOption("local");
            localTreeSelected();
        }

        private void StatusStripUpdate()
        {
            statusTelecastCount.Text = stv.VideoArchive.Count().ToString() + " Sendungen im Videoarchiv";
            statusProgrammedCount.Text = stv.Programmed.Count().ToString() + " Sendungen programmiert";
            statusLastUpdate.Text = "Letztes Update: " + stv.LastUpdate.ToString("dd.MM.yyyy HH:mm zzz");
            if (settings.UseLocalArchive)
            {
                statusLocalCount.Text = local.Episodes.Count(episode => episode.Filename != "").ToString() + " Sendungen im lokalen Archiv";
            }
            else { statusLocalCount.Text = "          "; }
        }

        private void toolStvShowVideoArchive_Click(object sender, EventArgs e)
        {
            StvTreeRefresh();
        }

        private void toolStvShowProgrammed_Click(object sender, EventArgs e)
        {
            StvTreeRefresh();
        }

        private void toolStvShowHasNoAdfree_Click(object sender, EventArgs e)
        {
            settings.StvShowHasNoAdfree = toolStvShowHasNoAdfree.Checked;
            StvTreeItemSelected();
        }

        private void toolStvShowHasAdfree_Click(object sender, EventArgs e)
        {
            settings.StvShowHasAdfree = toolStvShowHasAdfree.Checked;
            StvTreeItemSelected();
        }

        private void toolStvShowDuplicates_Click(object sender, EventArgs e)
        {
            settings.StvShowDuplicates = toolStvShowDuplicates.Checked;
            StvTreeItemSelected();
        }

        private void toolStvShowLocalAvailable_Click(object sender, EventArgs e)
        {
            settings.StvShowLocalAvailable = toolStvShowLocalAvailable.Checked;
            StvTreeItemSelected();
        }

        private void toolStvShowBroken_Click(object sender, EventArgs e)
        {
            settings.StvShowBroken = toolStvShowBroken.Checked;
            StvTreeRefresh();
        }

        //private void toolManualAdd_Click(object sender, EventArgs e)
        //{
        //    tShow newShow = local.AddNew(true);
        //    string StvTitle = stv.TvdbLink.Where(telecast => telecast.Value == newShow.ID).Select(kvp => kvp.Key);
        //    if (newShow != null)
        //    {
        //        LinkStvTxdb(stv.Telecasts.GetByTitle(StvTitle));
        //        localListUpdate();
        //    }
        //}

        private void linkForum_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(linkForum.Text);
        }

        private void linkEmail_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("mailto:" + linkEmail.Text);
        }


        private void rbDownloadTelecastLink_CheckedChanged(object sender, EventArgs e)
        {
        }

        private void cbSynoUseSSH_CheckedChanged(object sender, EventArgs e)
        {
            if (tbStvUsername.Text != "thomasfl")
            {
                MessageBox.Show("Private Funktion. Bei Interesse bitte im Forum posten, da mir sonst der Aufwand einer allgemeinen Implementierung zu groß ist.",
                    "STV MANAGER", MessageBoxButtons.OK, MessageBoxIcon.Information);
                cbSynoUseSSH.Checked = false;
            }
        }

        private void toolCancelAction_Click(object sender, EventArgs e)
        {
            stv.CancelAction = true;
        }

        private void toolSearchStart_Click(object sender, EventArgs e)
        {
            Search();
        }

        private void tbSearchText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                Search();
            }
        }

        private tFilter GetFilter()
        {
            return new tFilter
            {
                SearchText = tbSearchText.Text,
                FulltextOption = (SearchFulltextOptions)boxSearchFulltextOptions.SelectedIndex,
                SearchByTVStation = cbSearchUseTVStation.Checked,
                TVStation = boxSearchTVStation.SelectedItem.ToString(),
                SearchByDate = cbSearchUseDate.Checked,
                useEpgDate = false,
                DateOption = (SearchDateOptions)boxSearchDateRepeat.SelectedIndex,
                Date = dtSearchDate.Value,
                SearchByStartTime = cbSearchUseTime.Checked,
                StartTime1 = dtSearchTime1.Value,
                StartTime2 = dtSearchTime2.Value
            };
        }

        private void Search()
        {
            if (tbSearchText.Text == "" && (!cbSearchUseTVStation.Checked | !cbSearchUseDate.Checked))
            {
                MessageBox.Show("Zuwenig Suchparameter. Mögliche Suchen sind:\r\nSuchtext (ohne/mit Einschränkungen)\r\nSender & Datum (ohne/mit Uhrzeit)",
                    "Suche im TV-Programm", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                lvSearchDetails.Items.Clear();
                toolSearchCreateRecord.Enabled = false;
                picSearchDetail.Image = null;
                tbSearchPublicText.Text = "";
                boxSearchResult.Text = "Suchergebnis";

                toolSearchStart.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
                toolSearchStart.Text = "Suche ...";

                // first unsubscribe both events to avoid double event subscriptions
                stv.SearchUpdateEvent -= OnSearchUpdate;
                stv.SearchUpdateEvent -= OnSearchUpdateAndProgram;
                if (cbAutoProgram.Checked) { stv.SearchUpdateEvent += OnSearchUpdateAndProgram; }
                else { stv.SearchUpdateEvent += OnSearchUpdate; }

                stv.NewSearch(GetFilter());
            }
        }

        private void OnSearchUpdate(SearchUpdateEventArgs e)
        {
            tTelecastCollection result = e.Telecasts;

            toolSearchStart.Text = "Suchen";
            toolSearchStart.DisplayStyle = ToolStripItemDisplayStyle.Text;

            SearchListRefresh(result);
        }

        private async void OnSearchUpdateAndProgram(SearchUpdateEventArgs e)
        {
            tTelecastCollection result = e.Telecasts;

            toolSearchStart.Text = "Suchen";
            toolSearchStart.DisplayStyle = ToolStripItemDisplayStyle.Text;

            SearchListRefresh(result);
            await stv.CreateRecords(result);
        }

        private void SearchListRefresh(tTelecastCollection telecasts)
        {
            lvSearchDetails.Items.Clear();

            lvSearchDetails.BeginUpdate();
            object rememberComparer = lvSearchDetails.ListViewItemSorter;
            lvSearchDetails.ListViewItemSorter = null;
            foreach (tTelecast item in telecasts)
            {
                lvSearchDetails.Items.Add(item.ToListViewItem());
            }
            lvSearchDetails.ListViewItemSorter = (ListViewColumnSorter)rememberComparer;
            lvSearchDetails.EndUpdate();
            boxSearchResult.Text = "Suchergebnis: " + lvSearchDetails.Items.Count.ToString() + " Sendungen";
        }

        private void lvSearchDetails_SelectedIndexChanged(object sender, EventArgs e)
        {
            SearchDetailsSelected();
        }

        private void SearchDetailsSelected()
        {
            if (lvSearchDetails.SelectedItems.Count > 0)
            {
                tTelecast item = (tTelecast)lvSearchDetails.SelectedItems[0].Tag;
                tbSearchPublicText.Text = item.PublicText;
                picSearchDetail.Image = null;
                picSearchDetail.LoadAsync(item.ImageURL);
                toolSearchCreateRecord.Enabled = true;
            }
            else
            {
                picSearchDetail.Image = null;
                tbSearchPublicText.Text = "";
                toolSearchCreateRecord.Enabled = false;
            }
        }

        private void toolSearchClear_Click(object sender, EventArgs e)
        {
            SearchFavoritesLoad(new tFavorite());
            lbSearchFavorites.SelectedItem = null;
            SearchListRefresh(new tTelecastCollection());
            //tbSearchFavoriteName.Text = "";
        }

        private void cbSearchUseTime_CheckedChanged(object sender, EventArgs e)
        {
            dtSearchTime1.Enabled = cbSearchUseTime.Checked;
            dtSearchTime2.Enabled = cbSearchUseTime.Checked;
        }

        private void cbSearchUseTVStation_CheckedChanged(object sender, EventArgs e)
        {
            boxSearchTVStation.Enabled = cbSearchUseTVStation.Checked;
        }

        private void cbSearchUseDate_CheckedChanged(object sender, EventArgs e)
        {
            dtSearchDate.Enabled = cbSearchUseDate.Checked;
            cbSearchUseTime.Enabled = cbSearchUseDate.Checked;
            dtSearchTime1.Enabled = (cbSearchUseDate.Checked & cbSearchUseTime.Checked);
            boxSearchDateRepeat.Enabled = cbSearchUseDate.Checked;
        }

        private async void toolCreateRecord_Click(object sender, EventArgs e)
        {
            if (lvSearchDetails.SelectedItems.Count > 0)
            {
                tTelecastCollection telecasts = new tTelecastCollection();
                foreach (ListViewItem item in lvSearchDetails.SelectedItems)
                {
                    telecasts.Add((tTelecast)item.Tag);
                }

                await stv.CreateRecords(telecasts);
            }
        }

        private void OnRecordCreateEvent(RecordCreateEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<RecordCreateEventArgs>(OnRecordCreateEvent), e);
                return;
            }

            string Success = e.SuccessCount.ToString() + " Aufnahmen angelegt\r\n" +
                e.ExistingCount.ToString() + " Aufnahmen bereits vorhanden\r\n" +
                e.ErrorCount.ToString() + " Aufnahmen nicht möglich";

            MessageBoxIcon icon = new MessageBoxIcon();
            if (e.ErrorCount > 0) icon = MessageBoxIcon.Error;
            else if (e.SuccessCount == 0) icon = MessageBoxIcon.Exclamation;
            else icon = MessageBoxIcon.Information;

            MessageBox.Show(Success, "Aufnahmen Programmieren", MessageBoxButtons.OK, icon);
        }

        private void btSearchFavoriteSave_Click(object sender, EventArgs e)
        {
            if (tbSearchFavoriteName.Text == "")
            {
                MessageBox.Show("Kein Name für den Favoriten angegeben", "Favoriten", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                tFavorite newFavorite = new tFavorite
                {
                    Filter = new tFilter
                    {
                        SearchText = tbSearchText.Text,
                        FulltextOption = (SearchFulltextOptions)boxSearchFulltextOptions.SelectedIndex,
                        SearchByTVStation = cbSearchUseTVStation.Checked,
                        TVStation = boxSearchTVStation.Text,
                        SearchByDate = cbSearchUseDate.Checked,
                        Date = dtSearchDate.Value,
                        SearchByStartTime = cbSearchUseTime.Checked,
                        StartTime1 = dtSearchTime1.Value,
                        StartTime2 = dtSearchTime2.Value,
                        DateOption = (SearchDateOptions)boxSearchDateRepeat.SelectedIndex
                    },
                    AutoProgram = cbAutoProgram.Checked
                };
                stv.Favorites[tbSearchFavoriteName.Text] = newFavorite;
                SearchFavoritesUpdate();
            }
        }

        private void btSearchFavoriteDelete_Click(object sender, EventArgs e)
        {
            if (lbSearchFavorites.SelectedItem != null)
            {
                string item = lbSearchFavorites.SelectedItem.ToString();
                stv.Favorites.Remove(item);
                tbSearchFavoriteName.Text = "";
                SearchFavoritesUpdate();
            }
        }

        private void lbSearchFavorites_SelectedIndexChanged(object sender, EventArgs e)
        {
            string item = "";
            if (lbSearchFavorites.SelectedItem != null)
            {
                item = lbSearchFavorites.SelectedItem.ToString();
                SearchFavoritesLoad(stv.Favorites[item], settings.SearchFavoritesExecuteSearch);
            }
            tbSearchFavoriteName.Text = item;
        }

        private void SearchFavoritesUpdate()
        {
            lbSearchFavorites.Items.Clear();
            lbSearchFavorites.Items.AddRange(stv.Favorites.Keys.ToArray());
        }

        private void SearchFavoritesLoad(tFavorite Favorite, bool ExecuteSearch = false)
        {
            tbSearchText.Text = Favorite.Filter.SearchText;
            boxSearchFulltextOptions.SelectedItem = Favorite.Filter.FulltextOption.ToDescription();
            cbSearchUseTVStation.Checked = Favorite.Filter.SearchByTVStation;
            boxSearchTVStation.SelectedItem = Favorite.Filter.TVStation;
            cbSearchUseDate.Checked = Favorite.Filter.SearchByDate;
            // Suchdatum anpassen, da MinDate immer der heutige Tag ist
            dtSearchDate.Value = (Favorite.Filter.Date > dtSearchDate.MinDate) ? Favorite.Filter.Date : dtSearchDate.MinDate;
            cbSearchUseTime.Checked = Favorite.Filter.SearchByStartTime;
            dtSearchTime1.Value = Favorite.Filter.StartTime1;
            dtSearchTime2.Value = Favorite.Filter.StartTime2;
            boxSearchDateRepeat.SelectedItem = Favorite.Filter.DateOption.ToDescription();
            cbAutoProgram.Checked = Favorite.AutoProgram;

            if (ExecuteSearch) { Search(); }
        }

        private void dtSearchTime1_ValueChanged(object sender, EventArgs e)
        {
            if (dtSearchTime1.Value > dtSearchTime2.Value)
                dtSearchTime2.Value = dtSearchTime1.Value;
        }

        private void dtSearchTime2_ValueChanged(object sender, EventArgs e)
        {
            if (dtSearchTime2.Value < dtSearchTime1.Value)
                dtSearchTime1.Value = dtSearchTime2.Value;
        }

        private void listView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.A && e.Control)
            {
                foreach (ListViewItem item in (sender as ListView).Items)
                {
                    item.Selected = true;
                }
                e.Handled = true;
            }
        }

        private async Task StvChangeTxdbLink(IEnumerable<tTelecast> Telecasts, bool SetIgnore)
        {
            tTxdbLinkCollection<string> changeLinks = new tTxdbLinkCollection<string>();
            foreach (tTelecast Telecast in Telecasts)
            {
                changeLinks.Add(TxdbTitleLinks.Find(Telecast.Title));
            }
            if (SetIgnore) { TxdbIgnore(changeLinks); }
            else
            {
                foreach (tTxdbLink<string> Link in changeLinks)
                {
                    bool Cancel = !await TxdbLinkDefine(Link);
                    if (Cancel) break;
                }
            }
            localTreeRefresh();
            CheckDuplicates(stv.Telecasts);
            StvTreeItemSelected();
            //StvTreeRefresh();
        }

        private void toolStvChangeTxdbLink_Click(object sender, EventArgs e)
        {
            StvChangeTxdbLink(StvListGetSelectedItems("Verknüpfung ändern"), false);
        }

        private void toolStvIgnoreTxdbLink_Click(object sender, EventArgs e)
        {
            StvChangeTxdbLink(StvListGetSelectedItems("Verknüpfung ändern"), true);
        }

        private void toolStvChangeEpisode_Click(object sender, EventArgs e)
        {
            TvdbChangeEpisode(StvListGetSelectedItems("Episode ändern"));
        }

        private async Task ShowsChangeTxdbLink(ListView.SelectedListViewItemCollection Selected, bool SetIgnore = false, bool SetDelete = false)
        {
            if (Selected.Count == 0)
            {
                MessageBox.Show("Keine Serie ausgewählt", "Verknüpfung ändern", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            }
            else
            {
                tTxdbLinkCollection<string> Links = new tTxdbLinkCollection<string>();
                foreach (ListViewItem Item in Selected)
                {
                    tTxdbLink<string> Link = Item.Tag as tTxdbLink<string>;
                    Links.Add(Link);
                }
                if (SetIgnore) { TxdbIgnore(Links); }
                else if (SetDelete) { TxdbDelete(Links); }
                else
                {
                    foreach (tTxdbLink<string> Link in Links)
                    {
                        bool Cancel = !await TxdbLinkDefine(Link);
                        if (Cancel) break;
                    }
                }
                localTreeRefresh();
                CheckDuplicates(stv.Telecasts);
                StvTreeItemSelected();
                showsListUpdate();
            }
        }

        private void toolShowEdit_Click(object sender, EventArgs e)
        {
            ShowsChangeTxdbLink(lvShows.SelectedItems);
        }

        private void toolShowIgnore_Click(object sender, EventArgs e)
        {
            ShowsChangeTxdbLink(lvShows.SelectedItems, true);
        }

        private void toolShowDeleteEntry_Click(object sender, EventArgs e)
        {
            ShowsChangeTxdbLink(lvShows.SelectedItems, false, true);
        }

        private void showsListUpdate()
        {
            lvShows.BeginUpdate();
            lvShows.Items.Clear();
            foreach (tTxdbLink<string> Link in TxdbTitleLinks.Where(link => link.Type == TxdbLinkTypes.Show))
            {
                ListViewItem item = new ListViewItem(Link.Key);
                tShow Show = local.Show(Link.ID);

                string tvdbTitle = "";
                string tvdbId = "";
                string episodeCount = "";
                string episodeLocalCount = "";

                switch (Link.Status)
                {
                    case TxdbLinkStatusOptions.Undefined:
                        tvdbTitle = "  (Verknüpfung noch nicht definiert)";
                        break;
                    case TxdbLinkStatusOptions.Ignored:
                        tvdbTitle = "  (Titel wird ignoriert)";
                        break;
                    case TxdbLinkStatusOptions.Defined:
                        tvdbTitle = Show.Title;
                        tvdbId = Link.ID.ToString();
                        episodeCount = local.Episodes.Show(Show).Count().ToString();
                        episodeLocalCount = local.Episodes.Show(Show).Local().Count().ToString();
                        break;
                }

                item.SubItems.Add(tvdbTitle);
                item.SubItems.Add(tvdbId);
                item.SubItems.Add(episodeCount);
                item.SubItems.Add(episodeLocalCount);
                item.SubItems.Add(Show.Foldername);
                item.Tag = Link;
                lvShows.Items.Add(item);
            }
            lvShows.EndUpdate();
        }

        private void btSettingSeriesDefaultName_Click(object sender, EventArgs e)
        {
            tbSettingSeriesName.Text = "<%show%> - <%episodexcode%> - <%episode%>";
        }

        private void tvSettings_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node.ForeColor == SystemColors.GrayText) { e.Cancel = true; }
        }

        private void tvSettings_AfterSelect(object sender, TreeViewEventArgs e)
        {
            string node = e.Node.Name;
            pnSettingBasics.Visible = (node == "nodeBasics");
            pnSettingDownloadManager.Visible = (node == "nodeDownloadManager");
            pnSettingLocal.Visible = (node == "nodeLocal");
            pnSettingQuality.Visible = (node == "nodeDownloadQuality");
            pnAutoDownload.Visible = (node == "nodeAutoDownload");
            pnStreaming.Visible = (node == "nodeStreaming");
            if (node == "nodeDownloads")
            {
                tvSettings.SelectedNode = tvSettings.Nodes["nodeDownloads"].Nodes["nodeDownloadManager"];
            }
        }

        private void cbUseXbmc_CheckedChanged(object sender, EventArgs e)
        {
            bool xbmc = cbUseXbmc.Checked;
            lbXbmcUrl.Enabled = xbmc;
            tbXbmcUrl.Enabled = xbmc;
            lbXbmcPort.Enabled = xbmc;
            numXbmcPort.Enabled = xbmc;
            lbXbmcUser.Enabled = xbmc;
            tbXbmcUser.Enabled = xbmc;
            lbXbmcPass.Enabled = xbmc;
            tbXbmcPass.Enabled = xbmc;
        }

        private void calEPG_DateChanged(object sender, DateRangeEventArgs e)
        {
            EpgChanged();
        }

        private void lvEpgTVStations_SelectedIndexChanged(object sender, EventArgs e)
        {
            EpgChanged();
        }

        private void olvEpgTvStations_SelectionChanged(object sender, EventArgs e)
        {
            EpgChanged();
        }

        private async Task EpgChanged()
        {
            tTVStation Station = (tTVStation)olvEpgTvStations.SelectedObject;
            if (Station != null)
            {
                imgEpgTvStation.ImageLocation = Station.LogoUrlLarge;
                lbEpgDate.Text = calEPG.SelectionStart.Date.ToLongDateString();
                olvEpgList.ClearObjects();
                tbEpgPublicText.Text = "";
                picEpgImage.Image = null;
                toolEpgCreateRecord.Enabled = false;
                olvEpgList.SetObjects(await stv.GetEPG(calEPG.SelectionStart.Date, Station.ID));
            }
        }

        private string TimeOfDay(DateTime Date, DateTime StartDate)
        {
            int StartHour = (StartDate - Date).Hours + (StartDate - Date).Days * 24;
            if (StartHour < 12) { return "Morning"; }
            else if (StartHour < 20) { return "Afternoon"; }
            else if (StartHour < 24) { return "Evening"; }
            else { return "Night"; }
        }

        private async void toolEpgCreateRecord_Click(object sender, EventArgs e)
        {
            if (olvEpgList.SelectedObjects.Count > 0)
            {
                await stv.CreateRecords(olvEpgList.SelectedObjects.Cast<tTelecast>());
            }
        }

        private void olvEpgList_SelectionChanged(object sender, EventArgs e)
        {
            toolEpgCreateRecord.Enabled = olvEpgList.SelectedObjects.Count > 0;

            picEpgImage.Image = null;
            tTelecast telecast = (tTelecast)olvEpgList.SelectedObject;

            if (telecast != null)
            {
                tbEpgPublicText.Text = telecast.PublicText;
                if (telecast.ImageURL != "")
                {
                    picEpgImage.LoadAsync(telecast.ImageURL);
                }
            }
            else
            {
                tbEpgPublicText.Text = "";
            }
        }

        private void cbUseTxDB_CheckedChanged(object sender, EventArgs e)
        {
            boxUseLocalArchive.Enabled = cbUseTxDB.Checked;
            if (!cbUseTxDB.Checked) { cbUseLocalArchive.Checked = false; }
        }

        private void cbUseLocalArchive_CheckedChanged(object sender, EventArgs e)
        {
            TreeNode node = tvSettings.Nodes["nodeLocal"];
            if (cbUseLocalArchive.Checked)
            {
                node.ForeColor = SystemColors.ControlText;
                foreach (TreeNode child in node.Nodes) { child.ForeColor = SystemColors.ControlText; }
            }
            else
            {
                node.ForeColor = SystemColors.GrayText;
                foreach (TreeNode child in node.Nodes) { child.ForeColor = SystemColors.GrayText; }
            }
        }

        private void cbManageDownloads_CheckedChanged(object sender, EventArgs e)
        {
            TreeNode node = tvSettings.Nodes["nodeDownloads"];
            if (cbManageDownloads.Checked)
            {
                node.ForeColor = SystemColors.ControlText;
                foreach (TreeNode child in node.Nodes) { child.ForeColor = SystemColors.ControlText; }
            }
            else
            {
                node.ForeColor = SystemColors.GrayText;
                foreach (TreeNode child in node.Nodes) { child.ForeColor = SystemColors.GrayText; }
            }
        }

        private tFilter GetAssistantSearchFilter(string tvStation, DateTime date)
        {
            return new tFilter
            {
                SearchText = "",
                SearchByTVStation = true,
                TVStation = tvStation,
                SearchByDate = true,
                useEpgDate = false,
                DateOption = SearchDateOptions.SingleDay,
                Date = date,
                SearchByStartTime = false
            };
        }

        private void OnAssistantSearchUpdateEvent(SearchUpdateEventArgs e)
        {
            tTelecastCollection searchResult = e.Telecasts;
            wlTelecastCollection wunschliste = (lvAssistant.Tag as wlTelecastCollection);

            foreach (wlTelecast item in wunschliste.OrderBy(wl => wl.Airdate))
            {
                List<tTelecast> rightStation = searchResult.FindAll(search => search.TVStation == item.TVStation);
                tTelecast found = rightStation.Find(search => search.StartDate == item.Airdate);

                if (found != null)
                {
                    item.Telecast = found;
                    item.Status = found.Status;
                }
                else
                {
                    if (!stv.TVStations.Select(tv => tv.Name).Contains(item.TVStation))
                    {
                        item.Status = TelecastStatus.UnknownTvStation;
                    }
                }

                if (item.Status == TelecastStatus.InEPG)
                {
                    if (item.EpisodeCode == "")
                    {
                        wunschliste.OrderBy(wl => wl.Airdate).Where(wl => wl.Title == item.Title).Skip(1).ToList().ForEach(wl => wl.Status = TelecastStatus.Duplicate);
                    }
                    else
                    {
                        wunschliste.OrderBy(wl => wl.Airdate).Where(wl => wl.EpisodeCode == item.EpisodeCode).Skip(1).ToList().ForEach(wl => wl.Status = TelecastStatus.Duplicate);
                    }
                }
            }

            toolAssistantSearch.Text = "Suchen";
            toolAssistantSearch.DisplayStyle = ToolStripItemDisplayStyle.Text;
            AssistantListUpdate();
        }

        private void AssistantListUpdate()
        {
            wlTelecastCollection Telecasts = lvAssistant.Tag as wlTelecastCollection;
            lvAssistant.Items.Clear();
            if (Telecasts != null)
            {
                foreach (wlTelecast Telecast in Telecasts)
                {
                    if ((settings.AssistantShowDuplicates || Telecast.Status != TelecastStatus.Duplicate) &
                        (settings.AssistantShowNonProgrammable || Telecast.Status != TelecastStatus.NotFound) &
                        (settings.AssistantShowNonProgrammable || Telecast.Status != TelecastStatus.UnknownTvStation) &
                        (settings.AssistantShowNonProgrammable || Telecast.Status != TelecastStatus.Future))
                    {
                        ListViewItem Item = new ListViewItem(Telecast.Title);
                        Item.SubItems.Add(settings.LocalUseSxxExxEpisodeCode ? Telecast.EpisodeCodeS : Telecast.EpisodeCode);
                        Item.SubItems.Add(Telecast.Airdate.ToString("dd.MM.yyyy HH:mm"));
                        Item.SubItems.Add(Telecast.TVStation);
                        Item.SubItems.Add(Telecast.Status.ToDescription()).Name = "status";
                        Item.Tag = Telecast;
                        if (Telecast.Status == TelecastStatus.Duplicate) { Item.ForeColor = Color.Gray; }
                        lvAssistant.Items.Add(Item);
                    }
                }
                groupAssistant.Text = "Suchergebnis: " + Telecasts.Count.ToString() + " Folgen";
            }
        }

        private async Task AssistantSearch()
        {
            if (tbAssistantShowTitle.Text == "")
            {
                MessageBox.Show("Bitte Titel der Serie eingeben", "Serienassistent", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            else
            {
                toolAssistantSearch.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
                toolAssistantSearch.Text = "Suche ...";

                OnTaskUpdateEvent(new TaskUpdateEventArgs(TaskUpdateOptions.Wunschliste, 0, 1));

                WunschlisteWrapper wl = new WunschlisteWrapper();
                wl.SearchString = tbAssistantShowTitle.Text;
                if (await wl.Search())
                {
                    lvAssistant.Tag = wl.Telecasts;
                    AssistantListUpdate();
                }
                OnTaskUpdateEvent(new TaskUpdateEventArgs(TaskUpdateOptions.Wunschliste, 1, 1));

                if (wl.TelecastsWithoutId().Any())
                {
                    stv.SearchUpdateEvent += new SearchUpdateEventHandler(OnAssistantSearchUpdateEvent);

                    tFilterCollection Filters = new tFilterCollection();

                    foreach (DateTime SearchDate in wl.TelecastsWithoutId().Dates)
                    {
                        foreach (string SearchTVStation in wl.TelecastsWithoutId().FindAll(SearchDate).TVStations)
                        {
                            Filters.Add(GetAssistantSearchFilter(SearchTVStation, SearchDate));
                        }
                    }
                    stv.NewSearch(Filters);
                }
                else
                {
                    OnTaskUpdateEvent(new TaskUpdateEventArgs(TaskUpdateOptions.Wunschliste, -1, 0));
                    AssistantListUpdate();
                }
            }
        }

        private void toolAssistantSearch_Click(object sender, EventArgs e)
        {
            AssistantSearch();
        }

        private wlTelecastCollection AssistantSelectedTelecasts()
        {
            wlTelecastCollection result = new wlTelecastCollection();
            foreach (ListViewItem item in lvAssistant.SelectedItems)
            {
                result.Add(item.Tag as wlTelecast);
            }
            return result;
        }

        private async void toolAssistantCreateRecord_Click(object sender, EventArgs e)
        {
            await stv.CreateRecords(AssistantSelectedTelecasts().Select(wl => wl.Telecast));
        }

        private void toolAssistantChangeTvStation_Click(object sender, EventArgs e)
        {
            FsChangeTvStationMapping(AssistantSelectedTelecasts().First().TVStation);
            AssistantSearch();
        }

        private void lvAssistant_SelectedIndexChanged(object sender, EventArgs e)
        {
            toolAssistantCreateRecord.Enabled = AssistantSelectedTelecasts().Any(telecast => telecast.Telecast.ID != 0);
        }

        private void toolAssistantShowDuplicates_Click(object sender, EventArgs e)
        {
            settings.AssistantShowDuplicates = toolAssistantShowDuplicates.Checked;
            AssistantListUpdate();
        }

        private void toolAssistantsShowNonProgrammable_Click(object sender, EventArgs e)
        {
            settings.AssistantShowNonProgrammable = toolAssistantShowNonProgrammable.Checked;
            AssistantListUpdate();
        }

        private void toolAssistantMapping_Click(object sender, EventArgs e)
        {
            FsTvStationDialog wlMap = new FsTvStationDialog();
            wlMap.ShowDialog();
        }

        private void tbAssistantShowTitle_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                AssistantSearch();
            }
        }

        private void EpgTvStationsSelect(int move)
        {
            int current = olvEpgTvStations.SelectedIndex;
            int prev = (current + olvEpgTvStations.GetItemCount() + move) % olvEpgTvStations.GetItemCount();
            olvEpgTvStations.SelectedIndex = prev;
            olvEpgTvStations.EnsureVisible(prev);
        }

        private void btEpgPrevChannel_Click(object sender, EventArgs e)
        {
            EpgTvStationsSelect(-1);
        }

        private void btEpgNextChannel_Click(object sender, EventArgs e)
        {
            EpgTvStationsSelect(+1);
        }

        private void btEpgPrevDay_Click(object sender, EventArgs e)
        {
            if (calEPG.SelectionStart > calEPG.MinDate)
            {
                calEPG.SetDate(calEPG.SelectionStart.AddDays(-1));
            }
        }

        private void btEpgNextDay_Click(object sender, EventArgs e)
        {
            if (calEPG.SelectionStart < calEPG.MaxDate)
            {
                calEPG.SetDate(calEPG.SelectionStart.AddDays(1));
            }
        }

        private void rbJDLPluginMode_CheckedChanged(object sender, EventArgs e)
        {
            cbJDLFullService.Enabled = rbJDLPluginMode.Checked;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (settings.UseXbmc)
            {
                apiXbmc xbmc = new apiXbmc
                {
                    Address = settings.XbmcUrl,
                    Port = settings.XbmcPort,
                    Username = settings.XbmcUser,
                    Password = settings.XbmcPass
                };
                if (!xbmc.VideoLibraryScan())
                {
                    MessageBox.Show("Aktualisierung der Kodi Datenbank nicht erfolgreich. Läuft Kodi und stimmen die Zugangsdaten?");
                }
            }
        }

        private void cbSynoUseHttps_CheckedChanged(object sender, EventArgs e)
        {
            if (cbSynoUseHttps.Checked)
            {
                lbSynoHttp.Text = "https://";
                numSynoServerPort.Value = 5001;
            }
            else
            {
                lbSynoHttp.Text = "http://";
                numSynoServerPort.Value = 5000;
            }
        }

        private void cbUseAutoDownloads_CheckedChanged(object sender, EventArgs e)
        {
            if (cbUseAutoDownloads.Checked)
            {
                rbAutoDownloadImmediate.Checked = true;
            }
            boxAutoDownloadOptions.Enabled = cbUseAutoDownloads.Checked;
            boxAutoDownloadTiming.Enabled = cbUseAutoDownloads.Checked;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            olvStvList.Refresh();
        }

        private async Task TestMethod()
        {
            StvApiV3 savetv = new StvApiV3();
            savetv.Username = settings.StvUsername;
            savetv.Password = settings.StvPassword;
            MessageBox.Show((await savetv.PostRecord(new[] { 12129504, 12129505 }, 5, 10)).ToString());
            MessageBox.Show((await savetv.DeleteRecord(new[] { 12129504, 12129505 })).ToString());
        }

        //306	Bildhöhe: 720
        //308	Bildbreite: 1280

        private void button2_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                string fileName = dialog.FileName;
                string folder = Path.GetDirectoryName(fileName);

                //List<string> arrHeaders = new List<string>();

                Shell32.Shell shell = new Shell32.Shell();
                Shell32.Folder objFolder = shell.NameSpace(folder);

                Shell32.FolderItem objFolderItem = objFolder.ParseName(Path.GetFileName(fileName));
                if (objFolderItem != null)
                {
                    string Width = objFolder.GetDetailsOf(objFolderItem, 308);
                    string Height = objFolder.GetDetailsOf(objFolderItem, 306);

                    MessageBox.Show($"{Width}x{Height}");
                }


                //Shell32.Shell shell = new Shell32.Shell();
                //Shell32.Folder objFolder = shell.NameSpace(folder);

                //for (int i = 0; i < 400; i++)
                ////for (int i = 0; i < short.MaxValue; i++)
                //{
                //    string header = objFolder.GetDetailsOf(null, i);
                //    if (String.IsNullOrEmpty(header))
                //        arrHeaders.Add("--");
                //    else
                //        arrHeaders.Add(header);
                //}

                //foreach (Shell32.FolderItem2 item in objFolder.Items())
                //{
                //    for (int i = 0; i < arrHeaders.Count; i++)
                //    {
                //        Console.WriteLine("{0}\t{1}: {2}", i, arrHeaders[i], objFolder.GetDetailsOf(item, i));
                //    }
                //}
            }
        }

        private void btGetTvIcons_Click(object sender, EventArgs e)
        {
            foreach (tTVStation Station in stv.TVStations)
            {

            }
        }

        private void toolLocalShowChange_Click(object sender, EventArgs e)
        {
            toolLocalShowFiles.Checked = false;
            toolLocalShowTelecasts.Checked = false;
            toolLocalShowTxdb.Checked = false;
            (sender as ToolStripButton).Checked = true;
            LocalArchiveTreeRefresh();
        }

        private void LocalArchiveTreeRefresh()
        {
            tvLocalArchiveTree.BeginUpdate();
            tvLocalArchiveTree.Nodes.Clear();
            if (toolLocalShowFiles.Checked)
            {
                tvLocalArchiveTree.Nodes.Add(local.ReadDirectories(Categories.Movie.ToDescription(), settings.LocalPathMovies));
                tvLocalArchiveTree.Nodes.Add(local.ReadDirectories(Categories.Series.ToDescription(), settings.LocalPathSeries));
                tvLocalArchiveTree.Nodes.Add(local.ReadDirectories(Categories.Info.ToDescription(), settings.LocalPathInfos));
                tvLocalArchiveTree.Nodes.Add(local.ReadDirectories(Categories.Other.ToDescription(), settings.LocalPathOther));
                tvLocalArchiveTree.ExpandAll();
            }
            tvLocalArchiveTree.EndUpdate();
        }

        private void tvLocalArchiveTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Text != "Serien")
            {
                LocalArchiveListRefresh(e.Node.Name);
            }
            else
            {
                olvLocalArchiveList.ClearObjects();
            }
        }

        private void LocalArchiveListRefresh(string Path)
        {
            olvLocalArchiveList.SetObjects(Directory.GetFiles(Path, "*.*", SearchOption.AllDirectories));
        }

        private void olvLocalArchiveList_SelectionChanged(object sender, EventArgs e)
        {
            LocalArchiveDetailRefresh(olvLocalArchiveList.SelectedObject as string);
        }

        private async Task LocalArchiveDetailRefresh(string Filename)
        {
            tbLocalArchiveDetailsSize.Text = "";
            tbLocalArchiveDetailsDuration.Text = "";
            if (!String.IsNullOrEmpty(Filename))
            {
                tMediaFile media = await tMediaFile.ReadFromFileAsync(Filename);
                tbLocalArchiveDetailsDuration.Text = media.DurationString;
                tbLocalArchiveDetailsSize.Text = media.SizeString;
            }
        }

        private async void toolLocalArchiveChangeTxdb_Click(object sender, EventArgs e)
        {
            TreeNode Folder = tvLocalArchiveTree.SelectedNode;
            IEnumerable<string> Filenames = olvLocalArchiveList.SelectedObjects.Cast<string>();

            switch (EnumEx.GetValueFromDescription<Categories>(Folder.Tag as string))
            {
                case Categories.Series:
                    tShow currentShow = local.Shows.FindByFolder(Folder.Name);
                    if (currentShow != null)
                    {
                        LocalArchiveChangeEpisodes(currentShow, Filenames);
                    }
                    else
                    {
                        await local.ReadShowFolder(Folder.Name);
                        olvLocalArchiveList.Refresh();
                    }
                    break;

                case Categories.Movie:
                    LocalArchiveChangeMovies(Filenames);
                    break;
            }
        }

        private void LocalArchiveChangeEpisodes(tShow Show, IEnumerable<string> Filenames)
        {
            foreach (string Filename in Filenames)
            {
                tEpisode currentEpisode = local.Episodes.FindByFileName(Filename);

                bool Cancel = false;

                fmTvdbEpisodes tvdbChangeEpisode = new fmTvdbEpisodes(Filename, Show, currentEpisode);
                switch (tvdbChangeEpisode.ShowDialog())
                {
                    case DialogResult.OK:
                        if (tvdbChangeEpisode.ReloadFlag)
                        {
                            local.Episodes.RemoveAll(episode => episode.ShowID == Show.ID);
                            local.Episodes.AddRange(tvdbChangeEpisode.Episodes());
                        }
                        if (currentEpisode != null) { currentEpisode.Filename = ""; }
                        local.Episode(tvdbChangeEpisode.Episode.ID).Filename = Filename;
                        break;

                    case DialogResult.Ignore:
                        if (currentEpisode != null) { currentEpisode.Filename = ""; }
                        break;

                    case DialogResult.Cancel:
                        Cancel = true;
                        break;
                }
                olvLocalArchiveList.RefreshObject(Filename);
                if (Cancel) break;
            }
        }

        private void LocalArchiveChangeMovies(IEnumerable<string> Filenames)
        {

        }

        private void toolLocalArchiveFileRename_Click(object sender, EventArgs e)
        {
            TreeNode Folder = tvLocalArchiveTree.SelectedNode;
            Categories MediaType = EnumEx.GetValueFromDescription<Categories>(Folder.Tag as string);
            IEnumerable<string> Filenames = olvLocalArchiveList.SelectedObjects.Cast<string>();

            switch (MediaType)
            {
                case Categories.Series:
                    tShow Show = local.Shows.FindByFolder(Folder.Name);
                    foreach (string Filename in Filenames)
                    {
                        tEpisode Episode = local.Episodes.FindByFileName(Filename);
                        if (Show != null & Episode != null)
                        {
                            string newFilename = Path.Combine(
                                Path.GetDirectoryName(Filename),
                                local.GetDefaultFilename(Show.ID, Episode.ID) + Path.GetExtension(Filename)
                                );
                            File.Move(Filename, newFilename);
                            Episode.Filename = newFilename;
                            olvLocalArchiveList.RemoveObject(Filename);
                            olvLocalArchiveList.AddObject(newFilename);
                        }
                    }
                    break;

                case Categories.Movie:
                    foreach (string Filename in Filenames)
                    {
                        tMovie Movie = local.Movies.FindByFileName(Filename);
                        if (Movie != null)
                        {
                            string newFilename = Path.Combine(
                                Path.GetDirectoryName(Filename),
                                local.GetDefaultFilename(Movie.ID) + Path.GetExtension(Filename)
                                );
                            File.Move(Filename, newFilename);
                            Movie.Filename = newFilename;
                            olvLocalArchiveList.RemoveObject(Filename);
                            olvLocalArchiveList.AddObject(newFilename);
                        }
                    }
                    break;
            }
        }

        private void cbExecuteSearch_CheckedChanged(object sender, EventArgs e)
        {
            settings.SearchFavoritesExecuteSearch = cbExecuteSearch.Checked;
        }

        private void TelecastChangeCategory(IEnumerable<tTelecast> Telecasts, string Category)
        {
            foreach (string stvTitle in Telecasts.Select(telecast => telecast.Title).Distinct())
            {
                switch (Category)
                {
                    case "movie":
                        TxdbTitleLinks.Find(stvTitle).Type = TxdbLinkTypes.Movie;
                        break;
                    case "series":
                        TxdbTitleLinks.Find(stvTitle).Type = TxdbLinkTypes.Show;
                        break;
                    case "info":
                    case "other":
                        TxdbTitleLinks.Find(stvTitle).Type = TxdbLinkTypes.Undefined;
                        break;
                }
            }
            foreach (tTelecast telecast in Telecasts)
            {
                switch (Category)
                {
                    case "movie":
                        telecast.Category = Categories.Movie;
                        break;
                    case "series":
                        telecast.Category = Categories.Series;
                        break;
                    case "info":
                        telecast.Category = Categories.Info;
                        break;
                    case "other":
                        telecast.Category = Categories.Other;
                        break;
                }
            }
            StvTreeRefresh();
        }

        private void toolChangeCategory_Click(object sender, EventArgs e)
        {
            TelecastChangeCategory(StvListGetSelectedItems("Kategorie ändern"), (sender as ToolStripMenuItem).Tag as string);
        }

        private void toolHelp_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start((sender as ToolStripButton).Tag as string);
        }

        private void toolStvShowAllRecords_Click(object sender, EventArgs e)
        {
            toolStvShowChannelRecords.Checked = false;
            toolStvShowManualRecords.Checked = false;
            toolStvShowAllRecords.Checked = true;
            StvTreeItemSelected();
        }

        private void toolStvShowManualRecords_Click(object sender, EventArgs e)
        {
            toolStvShowAllRecords.Checked = false;
            StvTreeItemSelected();
        }

        private void toolStvShowChannelRecords_Click(object sender, EventArgs e)
        {
            toolStvShowAllRecords.Checked = false;
            StvTreeItemSelected();
        }

        private void cbActivateLogging_CheckedChanged(object sender, EventArgs e)
        {
            settings.EnableDebugLogging = cbActivateDebugLogging.Checked;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OperatingSystem win = System.Environment.OSVersion;
            MessageBox.Show($"Major {win.Version.Major}; Minor: {win.Version.Minor}; Revision; {win.Version.Revision}");
        }

        private async void button3_Click_1(object sender, EventArgs e)
        {
            FsClient client = new FsClient();
            FsSeries series = await client.FindSeriesAsync("terra x");
            FsTelecastCollection telecasts = await client.GetEpisodesAsync(series);
            MessageBox.Show($"{series.Title} ({series.Code}): {telecasts.Count} Ausstrahlungen");
        }

        private void FsChangeTvStationMapping(string FsTvStation)
        {
            FsTvStationMappingEditDialog FsEditTvStation = new FsTvStationMappingEditDialog(stv.TVStations, FsTvStation);
            if (FsEditTvStation.ShowDialog() == DialogResult.OK)
            {
                FsTvStationMapping wlMap = FsTvStationMapping.ReadFromFile();
                wlMap[FsTvStation] = FsEditTvStation.StvTvStation;
                wlMap.SaveToFile();
            }
        }

        private void SmSeriesRefresh()
        {
            lvSmSeries.SetObjects(SeriesManager.Series);
        }

        private void SmTelecastsRemapTvStations(IEnumerable<FsTelecast> telecasts)
        {
            FsTvStationMapping mapping = FsTvStationMapping.ReadFromFile();
            foreach (FsTelecast item in telecasts)
            {
                string fsTvStation = item.TVStation;
                if (!stv.TVStations.Exists(fsTvStation))
                {
                    item.TVStation = mapping.GetSaveTvStation(fsTvStation);
                }
            }
        }

        private async Task SmEpisodesReloadFsSeries(SmSeries series)
        {
            imgSmWaitingCursor.Location = new Point(lvSmEpisodes.Width / 2 - 16, lvSmEpisodes.Height / 2 - 16 + 25);
            imgSmWaitingCursor.Visible = true;
            SeriesManager.Episodes.RemoveEpisodes(series);
            FsClient client = new FsClient();
            FsTelecastCollection telecasts = await client.GetEpisodesAsync(series.Fernsehserien);
            SmTelecastsRemapTvStations(telecasts);
            foreach (FsTelecast item in telecasts)
            {
                SeriesManager.Episodes.Add(new SmEpisode(item));
            }
            imgSmWaitingCursor.Visible = false;
        }

        private async Task SmEpisodesReload()
        {
            lvSmEpisodes.ClearObjects();
            SmSeries series = (lvSmSeries.SelectedObject as SmSeries);
            if (series != null)
            {
                if (DateTime.Now.Subtract(series.Fernsehserien.LastUpdate).Days >= 1)
                {
                    await SmEpisodesReloadFsSeries(series);
                }

                int startDays = toolSmViewArchive.Checked ? -28 : 0;
                int endDays = toolSmViewArchive.Checked ? 0 : 28;
                SmEpisodeCollection filterEpisodes = SeriesManager.Episodes.FilterEpisodes(series, DateTime.Now.AddDays(startDays), DateTime.Now.AddDays(endDays));
                lvSmEpisodes.SetObjects(filterEpisodes);

                // Mögliche passende Sendungen auf Save.TV suchen
                FsTelecastCollection fsTelecasts = filterEpisodes.GetFsTelecasts();
                tTelecastCollection stvTelecasts = new tTelecastCollection();
                foreach (DateTime SearchDate in fsTelecasts.Dates)
                {
                    foreach (string SearchTVStation in fsTelecasts.FindAll(SearchDate).TVStations)
                    {
                        foreach (tTelecast item in await stv.GetTvStationDaySearch(SearchTVStation, SearchDate, false))
                        {
                            SmEpisode found = filterEpisodes.FindEpisode(item.TVStation, item.StartDate);
                            if (found != null)
                            {
                                if (stv.Telecasts.Contains(item.ID))
                                {
                                    found.SaveTv = stv.Telecasts.GetById(item.ID);
                                }
                                else
                                {
                                    found.SaveTv = item;
                                }
                                lvSmEpisodes.RefreshObject(found);
                            }
                        }
                    }
                }
            }
        }

        private void lvSmSeries_SelectionChanged(object sender, EventArgs e)
        {
            SmEpisodesReload();
        }

        private void toolSmViewChange_Click(object sender, EventArgs e)
        {
            toolSmViewArchive.Checked = false;
            toolSmViewProgrammed.Checked = false;
            (sender as ToolStripButton).Checked = true;
            SmUpdateToolbar();
            SmEpisodesReload();
        }

        private void SmUpdateToolbar()
        {
            toolSmCreateRecord.Enabled = toolSmViewProgrammed.Checked & stv.Status == StvStatusOptions.Ready;
            toolSmDownload.Enabled = toolSmViewArchive.Checked & stv.Status == StvStatusOptions.Ready;
        }

        private void toolSmEditFsTvStationMapping_Click(object sender, EventArgs e)
        {
            if (lvSmEpisodes.SelectedObject == null)
            {
                MessageBox.Show("Bitte eine Sendung auswählen, deren TV Sender neu zugeordnet werden soll.");
            }
            else
            {
                string FsTvStation = (lvSmEpisodes.SelectedObject as SmEpisode).Fernsehserien.TVStation;
                FsTvStationMappingEditDialog FsEditTvStation = new FsTvStationMappingEditDialog(stv.TVStations, FsTvStation);
                if (FsEditTvStation.ShowDialog() == DialogResult.OK)
                {
                    FsTvStationMapping mapping = FsTvStationMapping.ReadFromFile();
                    mapping[FsTvStation] = FsEditTvStation.StvTvStation;
                    mapping.SaveToFile();
                    SmTelecastsRemapTvStations(SeriesManager.Episodes.Select(episode => episode.Fernsehserien));
                    SmEpisodesReload();
                }
            }
        }

        private async void toolSmCreateRecord_Click(object sender, EventArgs e)
        {
            await stv.CreateRecords(lvSmEpisodes.SelectedObjects.Cast<SmEpisode>().Select(episode => episode.SaveTv));
            SmEpisodesReload();
        }

        private async void toolSmDownload_Click(object sender, EventArgs e)
        {
            SmSeries series = (lvSmSeries.SelectedObject as SmSeries);
            IEnumerable<SmEpisode> episodes = lvSmEpisodes.SelectedObjects.Cast<SmEpisode>();
            IEnumerable<tTelecast> telecasts = episodes.Select(episode => episode.SaveTv);
            
            if (!Directory.Exists(downloader.DestinationPath))
            {
                MessageBox.Show("Downloadverzeichnis " + downloader.DestinationPath + " ist nicht erreichbar.\r\nBitte Einstellungen überprüfen.", "Download Starten", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                tabControl1.SelectedTab = tabSettings;
                tvSettings.SelectedNode = tvSettings.Nodes["nodeDownloads"];
                tbDownloadPath.Focus();
            }
            else
            {
                tDownloadCollection Downloads = new tDownloadCollection();
                foreach (SmEpisode episode in episodes)
                {
                    tDownload download = new tDownload(episode.SaveTv);

                    // detaillierten Dateinamen setzen
                    download.localFilename = $"{series.Title} - {episode.Episode.Code(settings.LocalUseSxxExxEpisodeCode ? EpisodeCodeTypes.sCode : EpisodeCodeTypes.xCode)} - {episode.Title} ({episode.SaveTv.StartDate.ToString("yyyy-MM-dd HH:mm")})";

                    download.AdFree = settings.StvPreferAdFree;
                    download.Format = settings.StvDefaultVideoFormat;
                    if (download.Format == RecordFormats.HD & !episode.SaveTv.hasHD) download.Format = RecordFormats.SD;
                    download.Status = DownloadStatus.Queued;
                    Downloads.Add(download);
                }

                downloader.AddRange(Downloads);
                Application.DoEvents(); //sicherstellen, dass alle UI-Vorgänge abgeschlossen sind, bevor die asynchrone Abfrage gestartet wird?

                IEnumerable<tDownload> allDownloads = await stv.GetDownloadLinksParallel(Downloads);
                IEnumerable<tDownload> runDownloads = allDownloads.Where(download => download.Status > DownloadStatus.Error);

                List<Task> allTasks = new List<Task>();
                foreach (tDownload download in runDownloads)
                {
                    if (string.IsNullOrEmpty(download.stvDownloadFilename))
                    {
                        allTasks.Add(stv.GetDownloadFilename(download));
                    }
                    download.Status = DownloadStatus.Submitting;
                }
                await Task.WhenAll(allTasks);
                downloader.RunRange(runDownloads);
            }

        }

        private void toolSmSeriesAdd_Click(object sender, EventArgs e)
        {
            FsFindSeriesDialog fsFindSeries = new FsFindSeriesDialog();
            if (fsFindSeries.ShowDialog() == DialogResult.OK)
            {
                SmSeries newSeries = new SmSeries()
                {
                    Title = fsFindSeries.Result.Title,
                    Fernsehserien = fsFindSeries.Result,
                    InfoProvider = InfoProviders.Fernsehserien
                };
                SeriesManager.Series.Add(newSeries);
                //lvSmSeries.SetObjects(SeriesManager.Series);
                lvSmSeries.AddObject(newSeries);
                lvSmSeries.SelectedObject = newSeries;
            }
        }

        private void toolSmSeriesDelete_Click(object sender, EventArgs e)
        {
            SmSeries deleteSeries = (SmSeries)lvSmSeries.SelectedObject;
            if (deleteSeries != null)
            {
                lvSmSeries.RemoveObject(deleteSeries);
                SeriesManager.Series.Remove(deleteSeries);
                SeriesManager.Episodes.RemoveEpisodes(deleteSeries);
            }
        }
    }
}

