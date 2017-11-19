using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using STVM.Data;
using STVM.Helper;
using TVDB.Web;
using TVDB.Model;

namespace STVM.Wrapper.Tvdb

{
    public partial class TvdbShow : Form
    {
        public string BasePath;
        private string Foldername {
            get
            { return tbFolderName.Text; }
            set
            { tbFolderName.Text = value; }
        }
        private bool foldernameAllowChange
        {
            set
            {
                tbFolderName.Enabled = value;
                btSelectFolder.Enabled = value;
            }
        }
        public string StvTitle
        {
            get { return stvTitle; }
        }
        private string stvTitle
        {
            get { return tbSTVTitle.Text; }
            set { tbSTVTitle.Text = value; }
        }
        private bool stvTitleAllowChange
        {
            set
            { tbSTVTitle.Enabled = value; }
        }

        public bool HideIgnoreAll
        {
            get { return !btCancel.Visible; }
            set { btCancel.Visible = !value; }
        }

        public bool HideFolderName
        {
            get { return !lbFolderName.Enabled; }
            set
            {
                lbFolderName.Enabled = !value;
                tbFolderName.Enabled = !value;
                btSelectFolder.Enabled = !value;
            }
        }

        private string tvdbSearchText
        {
            get
            { return boxTvdbLookup.Text; }
            set
            { boxTvdbLookup.Text = value; }
        }

        public tShowCollection KnownShows
        {
            set
            {
                boxTvdbLookup.Items.Clear();
                foreach (tShow show in value)
                {
                    boxTvdbLookup.Items.Add(show);
                }
            }
        }

        const string tvdbApiKey = "668868776BCB4E09";  // my own TheTVDB Api Key

        private WebInterface tvdb;
        private Language german;
        private Mirror mirror;
        private List<Series> _searchResults;
        private Series _selection = null;
        private SeriesDetails _series = null;

        public TvdbShow()
        {
            InitializeComponent();
            this.Font = SystemFonts.MessageBoxFont;
            string AppFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create) + "\\" + Application.ProductName;

            // API initialisieren
            tvdb = new WebInterface(tvdbApiKey, AppFolder);
            german = new Language()
            {
                Id = 14,
                Name = "Deutsch",
                Abbreviation = "de"
            };
            mirror = new Mirror()
            {
                Address = "http://thetvdb.com",
                ContainsBannerFile = true,
                ContainsZipFile = true,
                ContainsXmlFile = false
            };

            Foldername = "";
            foldernameAllowChange = true;
            stvTitle = "";
            stvTitleAllowChange = true;
            HideIgnoreAll = false;
            HideFolderName = false;

        }

        public new tShow Show()
        {
            tShow result = new tShow();
            result.Foldername = Foldername;
            if (_series != null)
            {
                result.ID = _series.Series.Id;
                result.Title = _series.Series.Name;
                result.Summary = _series.Series.Overview;
                result.Continuing = _series.Series.Status == "Continuing";
                DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                result.LastUpdate = epoch.AddSeconds(_series.Series.LastUpdated);
            }
            return result;
        }

        public tEpisodeCollection Episodes()
        {
            tEpisodeCollection result = new tEpisodeCollection();
            if (_series != null)
            {
                foreach (Episode episode in _series.Series.Episodes.Where(ep => ep.Name != null))
                {
                    tEpisode addEpisode = new tEpisode();
                    addEpisode.ID = episode.Id;
                    addEpisode.ShowID = episode.SeriesId;
                    addEpisode.Title = episode.Name;
                    addEpisode.Season = episode.SeasonNumber;
                    addEpisode.Episode = episode.Number;
                    addEpisode.AbsoluteEpisode = episode.AbsoluteNumber;
                    addEpisode.Summary = episode.Overview;
                    if (episode.EpImageFlag > 0)
                    {
                        addEpisode.ImageURL = String.Format("http://thetvdb.com/banners/{0}/{1}.jpg", episode.SeriesId, episode.Id);
                    }
                    result.Add(addEpisode);
                }
            }
            return result;
        }

        public async Task<bool> SearchFromStvTitle(string SearchString, bool SelectFirstHit)
        {
            if (SearchString != "")
            {
                stvTitle = SearchString;
                stvTitleAllowChange = false;
                // get default Foldername from StvTitle
                Foldername = Path.Combine(BasePath, DirectoryHelper.GetCleanPath(stvTitle));
                return await Search(stvTitle, SelectFirstHit);
            }
            else return false;
        }

        public async Task<bool> SearchFromFoldername(string SearchString, bool SelectFirstHit)
        {
            if (SearchString != "")
            {
                Foldername = SearchString;
                foldernameAllowChange = false;
                // get default StvTitle from Foldername
                stvTitle = Path.GetFileName(Foldername);
                return await Search(stvTitle, SelectFirstHit);
            }
            else return false;
        }

        public async Task<bool> SearchFromTVDBTitle(string SearchString, bool SelectFirstHit)
        {
            if (SearchString != "")
            {
                tvdbSearchText = SearchString;
                stvTitle = "wird automatisch zugewiesen";
                stvTitleAllowChange = false;
                // get default Foldername from TvdbSearchText
                Foldername = Path.Combine(BasePath, DirectoryHelper.GetCleanPath(tvdbSearchText));
                return await Search(tvdbSearchText, SelectFirstHit);
            }
            else return false;
        }

        private async Task<bool> Search(string SearchString, bool SelectFirstHit)
        {
            bool result = false;

            if (SearchString != "")
            {
                _searchResults = await tvdb.GetSeriesByName(SearchString, german.Abbreviation, mirror);

                // perfekten Hit gefunden? Dann direkt übernehmen.
                string firstHit = "";
                if (_searchResults.Count > 0)
                {
                    firstHit = _searchResults[0].Name;
                }
                if (SelectFirstHit &&
                    firstHit.Equals(SearchString, StringComparison.CurrentCultureIgnoreCase) &
                    firstHit.Equals(Path.GetFileName(Foldername), StringComparison.CurrentCultureIgnoreCase))
                {
                    _selection = _searchResults[0];
                    _series = await tvdb.GetFullSeriesById(_selection.Id, german.Abbreviation, mirror);
                    this.DialogResult = DialogResult.OK;
                    result = true;
                }

                else
                {
                    tvdbSearchText = SearchString;
                    UpdateSearchResult();

                    result = (this.ShowDialog() == DialogResult.OK);
                }

            }
            return result;
        }

        private void UpdateSearchResult()
        {
            olvSearchResults.SetObjects(_searchResults);
            int count = _searchResults.Count;
            lbSeriesFound.Text = count.ToString() + " Serien gefunden in TVDB Datenbank";
            olvSearchResults.SelectedIndex = 0;
            // SelectionChange(0);
        }

        private async Task SelectionChange(int Index)
        {
            _selection = _searchResults[Index];
            _series = await tvdb.GetFullSeriesById(_selection.Id, german.Abbreviation, mirror);
        }

        private void SeriesUpdate()
        {
            lbEpisodeCount.Text = _series.Series.Episodes.Count.ToString() + " Episoden";
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            lbLastEntry.Text = "Letzter verfügbarer Eintrag: " + epoch.AddSeconds(_series.Series.LastUpdated).ToShortDateString();
            tbDetails.Text = _series.Series.Overview;
        }

        private async void olvSearchResults_SelectionChanged(object sender, EventArgs e)
        {
            _selection = olvSearchResults.SelectedObject as Series;
            _series = await tvdb.GetFullSeriesById(_selection.Id, german.Abbreviation, mirror);
            SeriesUpdate();
        }

        private new void Close()
        {
            bool canClose = true;

            if (canClose && tbSTVTitle.Text == "")
            {
                MessageBox.Show("Es muss ein STV Serientitel angegeben sein", "STV MANAGER", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                canClose = false;
            }

            if (canClose && _selection == null)
            {
                canClose = (MessageBox.Show("Kein Eintrag aus der TVDB ausgewählt. Serie ohne Verknüpfung zur TVDB anlegen?", "STV MANAGER", 
                    MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes);
            }

            if (canClose && tbFolderName.Text == "")
            {
                canClose = (MessageBox.Show("Kein Verzeichnis angegeben. Serie ohne Basisverzeichnis anlegen?", "STV MANAGER", 
                    MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes);
            }

            if (canClose && !Directory.Exists(tbFolderName.Text))
            {
                DialogResult result = MessageBox.Show("Verzeichnis " + tbFolderName.Text + " nicht gefunden. Verzeichnis erstellen?", "STV MANAGER", 
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Error);
                if (result == DialogResult.Yes)
                {
                    Directory.CreateDirectory(tbFolderName.Text);
                    canClose = true;
                }
                else if (result == DialogResult.No)
                {
                    canClose = true;
                }
                else
                {
                    canClose = false;
                }
            }

            if (canClose)
            {
                this.DialogResult = DialogResult.OK;
                base.Close();
            }
        }

        private void Ignore()
        {
            _series = null;
            this.DialogResult = DialogResult.Ignore;
            base.Close();
        }

        private void Cancel()
        {
            _series = null;
            this.DialogResult = DialogResult.Cancel;
            base.Close();
        }

        private void btCancel_Click(object sender, EventArgs e)
        {
            Cancel();
        }

        private void btIgnore_Click(object sender, EventArgs e)
        {
            Ignore();
        }
        private void btAdd_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void fmAddShow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                Close();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                Cancel();
            }
        }

        private async void btTvdbLookup_Click(object sender, EventArgs e)
        {
            _searchResults = await tvdb.GetSeriesByName(tvdbSearchText, german.Abbreviation, mirror);
            UpdateSearchResult();
        }

        private async void TvdbLookup_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                _searchResults = await tvdb.GetSeriesByName(tvdbSearchText, german.Abbreviation, mirror);
                e.Handled = true;
                UpdateSearchResult();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                Cancel();
            }
        }

        private void SelectFolder()
        {
            folderBrowserDialog1.SelectedPath = Foldername;
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                Foldername = folderBrowserDialog1.SelectedPath;
            }
        }

        private void btSelectFolder_Click(object sender, EventArgs e)
        {
            SelectFolder();
        }

        private async void btReload_Click(object sender, EventArgs e)
        {
            _series = await tvdb.GetFullSeriesById(_series.Series.Id, german.Abbreviation, mirror);
            SeriesUpdate();
        }

    }


}
