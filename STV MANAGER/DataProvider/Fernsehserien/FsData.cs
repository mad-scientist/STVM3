using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using System.Threading.Tasks;
using STVM.Data;
using STVM.Helper;

namespace STVM.DataProvider.Fernsehserien
{
    public class FsSeries
    {
        public string Title;
        public string Country;
        public string Years;
        public string Code;
        public int FsShowId;
        public Uri ImageSmall;
        public Uri ImageLarge;
        public DateTime LastUpdate;

        public FsSeries() { }

        public FsSeries(FsFastSearchResponse response)
        {
            int blank = response.l.IndexOf(" ");
            Title = response.t;
            Country = response.l.Substring(0, blank);
            Years = response.l.Substring(blank + 1);
            Code = response.s;
            int cut = "http://bilder.fernsehserien.de/sendung/v".Length;
            string guessFsShowId = response.b.Substring(cut, response.b.Length - cut - 4); // b=http://bilder.fernsehserien.de/sendung/v23283.png
            int convert = 0;
            int.TryParse(guessFsShowId, out convert);
            FsShowId = convert;
            ImageSmall = new Uri(response.b);
        }
    }

    public class FsSeriesCollection : List<FsSeries> { }

    public class FsTelecast
    {
        public string Title;
        public EpisodeCode Episode;
        public String Date = "01.01.1900";
        public String StartTime = "00:00";
        public string EndTime = "00:00";
        public string TVStation = "";
        public int FsEpisodeId;
        public int FsShowId;

        [JsonIgnore]
        public tAiring Airing
        {
            get
            {
                int year = int.Parse(Date.Substring(6, 4));
                int month = int.Parse(Date.Substring(3, 2));
                int day = int.Parse(Date.Substring(0, 2));
                int startHour = int.Parse(StartTime.Substring(0, 2));
                int startMin = int.Parse(StartTime.Substring(3, 2));
                int endHour = int.Parse(EndTime.Substring(0, 2));
                int endMin = int.Parse(EndTime.Substring(3, 2));

                return new tAiring
                {
                    StartDate = new DateTime(year, month, day, startHour, startMin, 0),
                    EndDate = new DateTime(year, month, day, endHour, endMin, 0)
                };
            }
        }
    }

    public class FsTelecastCollection : List<FsTelecast>
    {
        public FsTelecastCollection()
        { }

        public FsTelecastCollection(IEnumerable<FsTelecast> items) : base(items)
        { }

        public IEnumerable<DateTime> Dates
        {
            get { return this.Select(telecast => telecast.Airing.StartDate.Date).Distinct().OrderBy(date => date); }
        }

        public IEnumerable<string> TVStations
        {
            get { return this.Select(telecast => telecast.TVStation).Distinct().OrderBy(tv => tv); }
        }

        public FsTelecastCollection FindAll(DateTime Date)
        {
            return new FsTelecastCollection(this.Where(telecast => telecast.Airing.StartDate.Date == Date.Date));
        }
    }

    public class FsFastSearchResponse
    {
        public string a { get; set; } // Typ "s" für Serie
        public string t { get; set; } // Titel als Text "Polizeiruf 110"
        public string l { get; set; } // Jahr und Land "D/A/CH 1971-"
        public string s { get; set; } // Titel als URL-String "polizeiruf-110"
        public string b { get; set; } // Thumbnail
        public string c { get; set; } // Produzent
    }

    public class FsTvStationMapping : Dictionary<string, string>
    {
        private const string MappingFileName = "WunschlisteMapping.js";

        public static FsTvStationMapping ReadFromFile()
        {
            string FileName = Path.Combine(ApplicationEx.AppDataPath(), MappingFileName);
            if (File.Exists(FileName))
            {
                // deserialize JSON directly from a file
                using (StreamReader file = File.OpenText(FileName))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    return (FsTvStationMapping)serializer.Deserialize(file, typeof(FsTvStationMapping));
                }
            }
            else
            {
                FsTvStationMapping result = new FsTvStationMapping()
                {
                    { "Anixe", "ANIXE SD" },
                    { "ARD-alpha", "ARD alpha" },
                    { "BR Fernsehen", "BR"},
                    { "BBC World News (GB)", "BBC World"},
                    { "CNBC Europe (GB)", "CNBC Europe"},
                    { "Disney Channel", "Disney Channel "},
                    { "hr-Fernsehen", "hr"},
                    { "kabel eins", "Kabel 1"},
                    { "MDR", "mdr"},
                    { "One", "ONE HD"},
                    { "Phoenix", "phoenix"},
                    { "rbb", "RBB"},
                    { "Sat.1", "SAT.1" },
                    { "Sat.1 Gold", "SAT.1 Gold"},
                    { "ServusTV Deutschland", "ServusTV"},
                    { "Sport1", "SPORT 1"},
                    { "Super RTL", "SUPER RTL"},
                    { "SWR Fernsehen", "SWR"},
                    { "ZDFkultur", "zdf.kultur"},
                    { "ZDFneo", "zdf_neo"},
                    { "ZDFinfo", "zdfinfo"}
                };
                result.SaveToFile();
                return result;
            }
        }

        public void SaveToFile()
        {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(Path.Combine(ApplicationEx.AppDataPath(), MappingFileName), json);
        }

        public string GetSaveTvStation(string FsTvStation)
        {
            if (this.ContainsKey(FsTvStation))
            {
                return this[FsTvStation];
            }
            else
            {
                return FsTvStation;
            }
        }
    }

}
