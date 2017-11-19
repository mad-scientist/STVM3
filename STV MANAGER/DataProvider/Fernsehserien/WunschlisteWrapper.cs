using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HtmlAgilityPack;
using System.Net;
using System.IO;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using STVM.Data;
using STVM.Stv.Data;
using System.Web.Script.Serialization;
using STVM.Wrapper.Http;
using STVM.DataProvider.Fernsehserien;

namespace STVM.DataProvider.Fernsehserien
{
    public partial class WunschlisteWrapper : Form
    {
        const string fsSearchURL = "https://www.fernsehserien.de/fastsearch";
        const string fsServerURL = "https://www.fernsehserien.de/";

        public WunschlisteWrapper()
        {
            InitializeComponent();
            this.Font = SystemFonts.MessageBoxFont;
            Telecasts = new wlTelecastCollection();
            Mapping = FsTvStationMapping.ReadFromFile();
        }

        public FsTvStationMapping Mapping;

        public wlTelecastCollection Telecasts;

        public wlTelecastCollection TelecastsWithoutId()
        {
            wlTelecastCollection result = new wlTelecastCollection();
            foreach (wlTelecast item in this.Telecasts.Where(wl => wl.Telecast.ID == 0))
            {
                result.Add(item);
            }
            return result;
        }

        public string SearchString
        {
            get { return tbSearchShowTitle.Text; }
            set { tbSearchShowTitle.Text = value; }
        }

        //private class httpGet
        //{
        //    private HttpWebRequest request;
        //    private HttpWebResponse response;

        //    public string Data
        //    {
        //        get
        //        {
        //            //Translate data from the Web-Response to a string
        //            Stream dataStream = response.GetResponseStream();
        //            StreamReader streamreader = new StreamReader(dataStream, Encoding.UTF8);
        //            string data = streamreader.ReadToEnd();
        //            streamreader.Close();
        //            return data;
        //        }
        //    }

        //    public HttpStatusCode StatusCode
        //    {
        //        get { return response.StatusCode; }
        //    }

        //    public string Redirect
        //    {
        //        get { return response.Headers["Location"]; }
        //    }

        //    public httpGet(string URL)
        //    {
        //        request = (HttpWebRequest)WebRequest.Create(URL);
        //        request.AllowAutoRedirect = false;
        //        request.Method = "GET";

        //        //Send Web-Request and receive a Web-Response
        //        Application.DoEvents();
        //        Cursor.Current = Cursors.WaitCursor;
        //        response = (HttpWebResponse)request.GetResponse();
        //        Cursor.Current = Cursors.Default;
        //    }
        //}

        private FsSeriesCollection FoundShows;

        private enum LookupResults { None, Found, Multiple }

        private async Task<LookupResults> Lookup(string SearchTitle)
        {
            httpRequest fsSuche = new Wrapper.Http.httpRequest();
            fsSuche.PostParameters.Add("suchwort", SearchTitle);
            if (await fsSuche.PostAsync(fsSearchURL) == HttpStatusCode.OK)
            {
                List<fsFastSearchResponse> response = new List<fsFastSearchResponse>();
                try
                {
                    var jss = new JavaScriptSerializer();
                     response = jss.Deserialize<List<fsFastSearchResponse>>(fsSuche.Response);
                }
                catch (Exception e)
                {
                    MessageBox.Show("Fehler beim Einlesen von Fernsehserien.de. Möglicherweise wurde die Website geändert.",
                        "Serienassistent", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                if (response.Any())
                {
                    FoundShows = new FsSeriesCollection();
                    foreach (fsFastSearchResponse item in response.Where(resp => resp.a == "s"))
                    {
                        int blank = item.l.IndexOf(" ");
                        FsSeries show = new FsSeries()
                        {
                            Title = item.t,
                            Country = item.l.Substring(0, blank),
                            Years = item.l.Substring(blank + 1),
                            Code = item.s
                        };
                        FoundShows.Add(show);
                    }
                    return (FoundShows.Count == 1) ? LookupResults.Found : LookupResults.Multiple;
                }
                else return LookupResults.None;
            }
            else return LookupResults.None;
        }

        private void wlResultUpdate(FsSeriesCollection Shows)
        {
            lvSearchResult.BeginUpdate();
            lvSearchResult.Items.Clear();
            foreach (FsSeries show in Shows)
            {
                ListViewItem item = new ListViewItem(show.Title);
                item.SubItems.Add(show.Country);
                item.SubItems.Add(show.Years);
                item.Tag = show.Code;
                lvSearchResult.Items.Add(item);
            }
            lvSearchResult.EndUpdate();

            lbSeriesFound.Text = Shows.Count.ToString() + " Serien gefunden auf Fernsehserien.de";
        }

        private string GetValue(HtmlNode item, string tag)
        {
            IEnumerable<HtmlNode> classNodes = item.Descendants("span").Where(node => node.Attributes["class"] != null);
            HtmlNode tagNode = classNodes.FirstOrDefault(node => node.Attributes["class"].Value == tag);
            if (tagNode != null)
            {
                return tagNode.InnerText;
            }
            else
            {
                return String.Empty;
            }
        }

        private string GetTitle(HtmlNode item)
        {
            try
            {
                HtmlNode titleItem = item.Descendants("span").First(node => node.Attributes["class"].Value == "l4");
                HtmlNode removeChild = titleItem.Descendants("span").FirstOrDefault(node => node.Attributes["class"] != null);
                if (removeChild != null) removeChild.Remove();
                return titleItem.InnerText.Trim();
            }
            catch
            {
                return "";
            }
        }

        private string MapSaveTVName(string TVStation)
        {
            if (Mapping.ContainsKey(TVStation))
            {
                return Mapping[TVStation];
            }
            else return TVStation;
        }

        private wlTelecastCollection ParseShow(HtmlAgilityPack.HtmlDocument html)
        {
            wlTelecastCollection result = new wlTelecastCollection();
            HtmlNode epg_list = html.DocumentNode.SelectSingleNode("//article").Element("table");
            if (epg_list != null)
            {
                string year = DateTime.Now.Year.ToString();
                foreach (HtmlNode node in epg_list.Elements("tbody")) //entfernt: .Where(d => d.GetAttributeValue("class", "").Contains("ep-hover")))
                {
                    if (node.Id.Contains("jahr"))
                    {
                        year = node.Id.Substring(5);
                    }
                    List<HtmlNode> data = node.FirstChild.Elements("td").ToList();
                    //0   savetv
                    //1   wochentag
                    //2   datum
                    //3
                    //4   zeit
                    //5
                    //6   sender
                    //7   teilserie
                    //8   absolute episode
                    //9 
                    //10  season
                    //11  episode
                    //12 
                    //13  titel
                    wlTelecast wl = new wlTelecast();
                    int colspan = 0;
                    int index = 0;
                    for (int item = 0; item < data.Count; item++)
                    {
                        index = item + colspan;
                        string itemText = data[item].InnerText;
                        switch (index)
                        {
                            //case 0:
                            //    HtmlNode saveTv = data[item].Element("a");
                            //    if (saveTv != null)
                            //    {
                            //        string saveTvUrl = HtmlEntity.DeEntitize(saveTv.Attributes["href"].Value);
                            //        string qs = saveTvUrl.Substring(saveTvUrl.IndexOf("?") + 1);
                            //        string saveTvTelecast = System.Web.HttpUtility.ParseQueryString(qs).Get("tc");
                            //        int telecastId;
                            //        Int32.TryParse(saveTvTelecast, out telecastId);
                            //        wl.Telecast.ID = telecastId;
                            //    }
                            //    break;
                            case 2:
                                if (!String.IsNullOrWhiteSpace(itemText))
                                {
                                    wl.Date = itemText + year;
                                }
                                else wl.Date = "01.01.9999";
                                break;
                            case 4:
                                if (!String.IsNullOrWhiteSpace(itemText))
                                {
                                    wl.Time = data[item].InnerText.Substring(0, 5);
                                }
                                break;
                            case 6:
                                wl.TVStation = MapSaveTVName(itemText);
                                break;
                            case 7:
                                wl.SubSeries = itemText;
                                break;
                            case 8:
                                wl.AbsoluteEpisode = itemText;
                                break;
                            case 10:
                                wl.Season = itemText.Trim(new[] { '.' });
                                break;
                            case 11:
                                wl.Episode = itemText;
                                break;
                            case 13:
                                wl.Title = itemText;
                                break;
                        }
                        colspan += data[item].GetAttributeValue("colspan", 1) - 1;
                    }
                    if (wl.Telecast.ID != 0)
                        wl.Status = TelecastStatus.InEPG;
                    else if (wl.Airdate < DateTime.Now.Date.AddDays(28))
                        wl.Status = TelecastStatus.NotFound;
                    else
                        wl.Status = TelecastStatus.Future;
                    if (
                        !String.IsNullOrWhiteSpace(wl.Title) &
                        !wl.TVStation.Contains("(CH)") &
                        !wl.TVStation.Contains("(A)")
                        )
                    {
                        result.Add(wl);
                    }
                }
            }
            return result;
        }

        private async Task<wlTelecastCollection> LoadShow(string ShowURL)
        {
            wlTelecastCollection result = new wlTelecastCollection();
            httpRequest fsPage = new httpRequest();
            if (await fsPage.GetAsync(fsServerURL + ShowURL + "/sendetermine") == HttpStatusCode.OK)
            {
                HtmlAgilityPack.HtmlDocument html = new HtmlAgilityPack.HtmlDocument();
                html.LoadHtml(fsPage.Response);
                result = ParseShow(html);
            }
            return result;
        }

        private async Task SearchTitle(string Text)
        {
            if (Text != "")
            {
                switch (await Lookup(SearchString))
                {
                    case LookupResults.None:
                        break;

                    case LookupResults.Found:
                        Telecasts = await LoadShow(FoundShows.First().Code);
                        this.DialogResult = System.Windows.Forms.DialogResult.OK;
                        this.Close();
                        break;

                    case LookupResults.Multiple:
                        wlResultUpdate(FoundShows);
                        break;
                }
            }
        }

        private async void btSearchTitle_Click(object sender, EventArgs e)
        {
            await SearchTitle(tbSearchShowTitle.Text);
        }

        private async void tbSearchShowTitle_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                await SearchTitle(tbSearchShowTitle.Text);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                Cancel();
            }

        }

        private async void btLoad_Click(object sender, EventArgs e)
        {
            if (lvSearchResult.SelectedItems.Count == 0)
            {
                MessageBox.Show("Keine Serie ausgewählt", "STV MANAGER", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            else
            {
                Telecasts = await LoadShow((string)lvSearchResult.SelectedItems[0].Tag);
                this.DialogResult = System.Windows.Forms.DialogResult.OK;
                this.Close();
            }
        }

        private void btCancel_Click(object sender, EventArgs e)
        {
            Cancel();
        }

        public async Task<bool> Search()
        {
            bool result = false;
            if (SearchString != "")
            {
                switch (await Lookup(SearchString))
                {
                    case LookupResults.None:
                        result = false;
                        break;

                    case LookupResults.Found:
                        Telecasts = await LoadShow(FoundShows.First().Code);
                        result = true;
                        break;

                    case LookupResults.Multiple:
                        wlResultUpdate(FoundShows);
                        result = (this.ShowDialog() == DialogResult.OK);
                        break;
                }
            }
            return result;
        }

        private void Cancel()
        {
            this.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.Close();
        }

    }

    public class fsFastSearchResponse
    {
        public string a { get; set; } // Typ "s" für Serie
        public string t { get; set; } // Titel als Text "Polizeiruf 110"
        public string l { get; set; } // Jahr und Land "D/A/CH 1971-"
        public string s { get; set; } // Titel als URL-String "polizeiruf-110"
        public string b { get; set; } // Thumbnail
        public string c { get; set; } // Produzent
    }

    public class wlTelecastCollection : List<wlTelecast>
    {
        public wlTelecastCollection()
        { }

        public wlTelecastCollection(IEnumerable<wlTelecast> items) : base(items)
        { }

        public IEnumerable<DateTime> Dates
        {
            get { return this.Select(telecast => telecast.Airdate.Date).Distinct().OrderBy(date => date); }
        }

        public IEnumerable<string> TVStations
        {
            get { return this.Select(telecast => telecast.TVStation).Distinct().OrderBy(tv => tv); }
        }

        public IEnumerable<int> TelecastIDs
        {
            get { return this.Where(telecast => telecast.Telecast.ID != 0).Select(telecast => telecast.Telecast.ID); }
        }

        public wlTelecastCollection FindAll(DateTime Date)
        {
            return new wlTelecastCollection(this.Where(telecast => telecast.Airdate.Date == Date.Date)); 
        }
    }

    public class wlTelecast
    {
        public string TVStation;
        public string Date;
        public string Time;
        public string SubSeries;
        public string Season;
        public string Episode;
        public string AbsoluteEpisode;
        public string Title;
        public tTelecast Telecast;
        public TelecastStatus Status;

        public wlTelecast()
        {
            TVStation = "";
            Date = "01.01.2000";
            Time = "00:00";
            SubSeries = "";
            Season = "";
            Episode = "";
            AbsoluteEpisode = "";
            Title = "";
            Telecast = new tTelecast();
            Status = TelecastStatus.Unknown;
        }

        public string EpisodeCode
        {
            get { return Season == "" ? Episode : Season + "x" + Episode; }
        }

        public string EpisodeCodeS
        {
            get { return Season == "" ? Episode : "S" + Season + "E" + Episode; }
        }

        public DateTime Airdate
        {
            get
            {
                int year = int.Parse(Date.Substring(6, 4));
                int month = int.Parse(Date.Substring(3, 2));
                int day = int.Parse(Date.Substring(0, 2));
                int hour = int.Parse(Time.Substring(0, 2));
                int minute = int.Parse(Time.Substring(3, 2));

                return new DateTime(year, month, day, hour, minute, 0);
            }
        }

        public string SearchString()
        {
            MatchCollection matches = Regex.Matches(Title, @"(?![–&-,\.\(\)]*\S*[–&-,\.\(\)]+)\S+");
            return string.Join(" ", matches.Cast<Match>());
        }
    }

}
