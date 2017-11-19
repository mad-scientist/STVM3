using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using STVM.Data;
using STVM.Helper;
using STVM.Stv.Data;
using STVM.DataProvider.Fernsehserien;
using STVM.DataProvider.TheTVDB;

namespace STVM.SeriesManager
{
    public class SmManager
    {
        public SmSeriesCollection Series;
        public SmEpisodeCollection Episodes;

        private const string SettingFileName = "SeriesManager.js";

        public static SmManager ReadFromFile()
        {
            string FileName = Path.Combine(ApplicationEx.AppDataPath(), SettingFileName);
            if (File.Exists(FileName))
            {
                // deserialize JSON directly from a file
                using (StreamReader file = File.OpenText(FileName))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    return (SmManager)serializer.Deserialize(file, typeof(SmManager));
                }
            }
            else
            {
                FsSeries FsTerraX = new FsSeries { Title = "Terra X", Code = "terra-x", FsShowId = 3245 };
                TvdbSeries TvdbTerraX = new TvdbSeries { Title = "Terra X", TvdbShowId = 126301 };

                SmManager result = new SmManager()
                {
                    Series = new SmSeriesCollection { new SmSeries { Title = "Terra X", Fernsehserien = FsTerraX, Tvdb = TvdbTerraX, InfoProvider = InfoProviders.Fernsehserien } },
                    Episodes = new SmEpisodeCollection { }
                };
                result.SaveToFile();
                return result;
            }
        }

        public void SaveToFile()
        {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(Path.Combine(ApplicationEx.AppDataPath(), SettingFileName), json);
        }


    }

    public class SmSeries
    {
        public string Title;
        public FsSeries Fernsehserien;
        public TvdbSeries Tvdb;
        public string LocalFolder;
        public InfoProviders InfoProvider;

        [JsonIgnore]
        public int SeriesId
        {
            get
            {
                switch(InfoProvider)
                {
                    case InfoProviders.Fernsehserien: return Fernsehserien.FsShowId;
                    case InfoProviders.Tvdb: return Tvdb.TvdbShowId;
                    default: return 0;
                }
            }
        }
    }

    public class SmSeriesCollection: List<SmSeries>
    {
    }

    public class SmEpisode
    {
        public string Title;
        public FsTelecast Fernsehserien;
        public TvdbEpisode Tvdb;
        public tTelecast SaveTv;
        public InfoProviders InfoProvider;

        public SmEpisode()
        {
            Title = "";
            Fernsehserien = new FsTelecast();
            Tvdb = new TvdbEpisode();
            SaveTv = new tTelecast();
            InfoProvider = InfoProviders.SaveTv;
        }

        public SmEpisode(FsTelecast Telecast): this()
        {
            Title = Telecast.Title;
            Fernsehserien = Telecast;
            InfoProvider = InfoProviders.Fernsehserien;
        }

        [JsonIgnore]
        public EpisodeCode Episode
        {
            get
            {
                switch (InfoProvider)
                {
                    case InfoProviders.Fernsehserien: return Fernsehserien.Episode;
                    case InfoProviders.Tvdb: return Tvdb.Episode;
                    default: return new EpisodeCode();
                }
            }
        }

        [JsonIgnore]
        public int SeriesId
        {
            get
            {
                switch (InfoProvider)
                {
                    case InfoProviders.Fernsehserien: return Fernsehserien.FsShowId;
                    case InfoProviders.Tvdb: return Tvdb.TvdbShowId;
                    default: return 0;
                }
            }
        }
    }

    public class SmEpisodeCollection: List<SmEpisode>
    {
        public SmEpisodeCollection GetEpisodes(SmSeries Series)
        {
            SmEpisodeCollection result = new SmEpisodeCollection();
            result.AddRange(this.Where(episode => episode.SeriesId == Series.SeriesId));
            return result;
        }

        public SmEpisodeCollection FilterEpisodes(SmSeries Series, DateTime StartDate, DateTime EndDate)
        {
            SmEpisodeCollection result = new SmEpisodeCollection();
            result.AddRange(this.Where(series => series.SeriesId == Series.SeriesId).Where(episode => episode.Fernsehserien.Airing.StartDate >= StartDate & episode.Fernsehserien.Airing.StartDate <= EndDate));
            return result;
        }

        public SmEpisode FindEpisode(string TvStation, DateTime StartDate)
        {
            return this.FirstOrDefault(episode => episode.Fernsehserien.TVStation == TvStation & episode.Fernsehserien.Airing.StartDate == StartDate);
        }

        public void RemoveEpisodes(SmSeries Series)
        {
            this.RemoveAll(episode => episode.SeriesId == Series.SeriesId);
        }

        public FsTelecastCollection GetFsTelecasts()
        {
            return new FsTelecastCollection(this.Select(episode => episode.Fernsehserien));
        }
    }
}
