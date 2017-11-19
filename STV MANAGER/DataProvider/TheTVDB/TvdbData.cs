using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using STVM.Data;

namespace STVM.DataProvider.TheTVDB
{
    public class TvdbSeries
    {
        public string Title;
        public int TvdbShowId;
        public DateTime LastUpdate;
        public string Summary;
    }

    public class TvdbEpisode
    {
        public String Title;
        public EpisodeCode Episode;
        public int TvdbShowId;
        public int TvdbEpisodeId;
        public string Summary;
        public Uri Image;
    }
}
