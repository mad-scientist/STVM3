using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Windows.Forms;
using HtmlAgilityPack;
using System.Web.Script.Serialization;
using STVM.Wrapper.Http;

namespace STVM.DataProvider.Fernsehserien
{
    public class FsClient
    {
        const string fsSearchURL = "https://www.fernsehserien.de/fastsearch";
        const string fsServerURL = "https://www.fernsehserien.de/";

        private List<FsTelecast> ParseFsResponse(HtmlAgilityPack.HtmlDocument html)
        {
            List<FsTelecast> result = new List<FsTelecast>();
            HtmlNode epg_list = html.DocumentNode.SelectSingleNode("//article").Element("table");
            if (epg_list != null)
            {
                string year = DateTime.Now.Year.ToString();
                foreach (HtmlNode node in epg_list.Elements("tbody")) // entfernt: .Where(d => d.GetAttributeValue("class", "").Contains("ep-hover")))
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

                    FsTelecast FsItem = new FsTelecast();
                    FsItem.Episode = new Data.EpisodeCode();

                    int colspan = 0;
                    int index = 0;
                    for (int item = 0; item < data.Count; item++)
                    {
                        index = item + colspan;
                        string itemText = data[item].InnerText;
                        switch (index)
                        {
                            //case 0:   // Links mit TelecastIDs von Save.TV werden nach einer EPG-Änderung nicht aktualisiert
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
                                    if (itemText.Length == 6) FsItem.Date = itemText + year;
                                    else FsItem.Date = itemText;
                                }
                                break;
                            case 4:
                                if (!String.IsNullOrWhiteSpace(itemText))
                                {
                                    FsItem.StartTime = data[item].InnerText.Substring(0, 5);
                                    FsItem.EndTime = data[item].InnerText.Substring(6, 5);
                                }
                                break;
                            case 6:
                                FsItem.TVStation = itemText;
                                break;
                            case 7:
                                if (itemText != "")
                                {
                                    FsItem.Episode.Season = itemText;
                                }
                                break;
                            case 8:
                                int AbsoluteEpisode = 0;
                                int.TryParse(itemText, out AbsoluteEpisode);
                                FsItem.Episode.AbsoluteEpisode = AbsoluteEpisode;
                                break;
                            case 10:
                                if (itemText != "")
                                {
                                    FsItem.Episode.Season = itemText.Trim(new[] { '.' });
                                }
                                break;
                            case 11:
                                int Episode = 0;
                                int.TryParse(itemText, out Episode);
                                FsItem.Episode.Episode = Episode;
                                break;
                            case 13:
                                FsItem.Title = itemText;
                                break;
                        }
                        colspan += data[item].GetAttributeValue("colspan", 1) - 1;
                    }
                    if (
                        !string.IsNullOrWhiteSpace(FsItem.Title) &
                        !FsItem.TVStation.Contains("(CH)") &
                        !FsItem.TVStation.Contains("(A)")
                        )
                    {
                        result.Add(FsItem);
                    }
                }
            }
            return result;
        }

        public async Task<FsTelecastCollection> GetEpisodesAsync(FsSeries Series)
        {
            FsTelecastCollection result = new FsTelecastCollection();
            httpRequest fsPage = new httpRequest();
            for (int page = -2; page <= 1; page++)
            {
                if (await fsPage.GetAsync(fsServerURL + Series.Code + "/sendetermine/" + page.ToString()) == HttpStatusCode.OK)
                {
                    HtmlAgilityPack.HtmlDocument html = new HtmlAgilityPack.HtmlDocument();
                    html.LoadHtml(fsPage.Response);
                    result.AddRange(ParseFsResponse(html));

                    // more data available?
                    HtmlNode dates_header = html.DocumentNode.SelectSingleNode("//article").Element("h2");
                    HtmlNode dates_header_next = dates_header.SelectSingleNode("//span[@class='rechts']");
                    if (!dates_header_next.HasChildNodes) break;
                }
            }
            if (result.Any())
            {
                Series.LastUpdate = DateTime.Now;
                result.ForEach(telecast => telecast.FsShowId = Series.FsShowId);
            }
            return result;
        }

        public async Task<List<FsSeries>> GetSeriesAsync(string SearchString)
        {
            List<FsSeries> result = new List<FsSeries>();
            httpRequest fsSuche = new httpRequest();
            fsSuche.PostParameters.Add("suchwort", SearchString);
            if (await fsSuche.PostAsync(fsSearchURL) == HttpStatusCode.OK)
            {
                List<FsFastSearchResponse> response = new List<FsFastSearchResponse>();
                try
                {
                    var jss = new JavaScriptSerializer();
                    response = jss.Deserialize<List<FsFastSearchResponse>>(fsSuche.Response);
                }
                catch (Exception e)
                {
                    MessageBox.Show("Fehler beim Einlesen von Fernsehserien.de. Möglicherweise wurde die Website geändert.",
                        "Serienassistent", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                if (response.Any())
                {
                    foreach (FsFastSearchResponse item in response.Where(resp => resp.a == "s"))
                    {
                        FsSeries series = new FsSeries(item);
                        result.Add(series);
                    }
                }
            }
            return result;
        }

        public async Task<FsSeries> FindSeriesAsync(string SearchString)
        {
            FsSeries result = new FsSeries();
            List<FsSeries> response = await GetSeriesAsync(SearchString);
            if (response.Any())
            {
                if (String.Equals(response.First().Title, SearchString, StringComparison.OrdinalIgnoreCase))
                {
                    result = response.First();
                }
                else
                {
                    FsFindSeriesDialog SelectSeries = new FsFindSeriesDialog(response);
                    SelectSeries.SearchString = SearchString;
                }
            }
            return result;
        }
    }

}
