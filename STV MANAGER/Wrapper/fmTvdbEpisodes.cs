using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using TVDB.Web;
using TVDB.Model;
using STVM.Data;
using STVM.Stv.Data;

namespace STVM.Wrapper.Tvdb
{
    public partial class fmTvdbEpisodes : Form
    {
        const string tvdbApiKey = "668868776BCB4E09";  // my own TheTVDB Api Key

        private WebInterface tvdb;
        private Language german;
        private Mirror mirror;

        private SeriesDetails _series;
        private tShow _show;
        private tEpisode _episode;

        public bool ReloadFlag = false;

        public fmTvdbEpisodes()
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
        }

        public fmTvdbEpisodes(tTelecast Telecast, tShow Show, tEpisode Episode)
            : this()
        {
            boxReferenceData.Text = "Save.TV Daten";
            lbStvTitle.Text = Telecast.Title;
            lbStvSubTitle.Text = Telecast.SubTitle;

            _show = Show;
            _episode = Episode;
        }

        public fmTvdbEpisodes(string Filename, tShow Show, tEpisode Episode)
            : this()
        {
            boxReferenceData.Text = "Dateieigenschaften";
            lbTitle.Text = "Dateiname";
            lbStvTitle.Text = Path.GetFileName(Filename);
            lbSubTitle.Text = "ID-Tag:";
            lbStvSubTitle.Text = TagLib.File.Create(Filename).Tag.Title;

            _show = Show;
            _episode = Episode;
        }

        private void fmTvdbEpisodes_FormClosed(object sender, FormClosedEventArgs e)
        {
        }

        private async void btReload_Click(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;
            _series = await tvdb.GetFullSeriesById(_show.ID, german.Abbreviation, mirror);
            ReloadFlag = true;
            EpisodesUpdate(Episodes());
        }

        private async void fmTvdbEpisodes_Load(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;
            _series = await tvdb.GetFullSeriesById(_show.ID, german.Abbreviation, mirror);
            Cursor.Current = Cursors.Default;
            lbTvdbTitle.Text = _series.Series.Name;
            EpisodesUpdate(Episodes());
            lvEpisodes.Select();
        }

        private void EpisodesUpdate(List<tEpisode> FilterEpisodes)
        {
            lvEpisodes.Items.Clear();
            foreach (tEpisode episode in FilterEpisodes)
            {
                ListViewItem item = new ListViewItem(episode.EpisodeCode());
                item.SubItems.Add(episode.Title);
                item.Tag = episode;
                lvEpisodes.Items.Add(item);
            }
            lbEpisodeCount.Text = _series.Series.Episodes.Count.ToString() + " Episoden";
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            lbLastEntry.Text = "Letzter Eintrag: " + epoch.AddSeconds(_series.Series.LastUpdated).ToShortDateString();

            if (_episode != null)
            {
                if (FilterEpisodes.Count() != 0)
                {
                    ListViewItem found = lvEpisodes.FindItemWithText(_episode.Title, true, 0);
                    if (found != null)
                    {
                        found.Selected = true;
                        lvEpisodes.TopItem = found;
                    }
                }
                else { System.Media.SystemSounds.Asterisk.Play(); }
            }

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

        public tEpisode Episode
        {
            get { return _episode; }
        }

        private void lvEpisodes_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lvEpisodes.SelectedItems.Count > 0)
            {
                _episode = lvEpisodes.SelectedItems[0].Tag as tEpisode;
                tbDetails.Text = _episode.Summary;
            }
        }

        private new void Close()
        {
            this.DialogResult = DialogResult.OK;
            base.Close();
        }

        private void Ignore()
        {
            _episode = new tEpisode();
            this.DialogResult = DialogResult.Ignore;
            base.Close();
        }

        private void Cancel()
        {
            _episode = new tEpisode();
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

        private void btOK_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void fmTvdbEpisodes_KeyDown(object sender, KeyEventArgs e)
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

        private void tbFilter_KeyUp(object sender, KeyEventArgs e)
        {
            if (tbFilter.Text == "")
            {
                EpisodesUpdate(Episodes());
            }
            else
            {
                EpisodesUpdate(Episodes().FindAll(episode => episode.Title.Contains(tbFilter.Text, StringComparison.CurrentCultureIgnoreCase)));
            }
        }
    }
}
