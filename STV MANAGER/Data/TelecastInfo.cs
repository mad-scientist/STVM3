using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.ComponentModel;
using Newtonsoft.Json;

namespace STVM.Data
{
    public enum InfoProviders
    {
        [DescriptionAttribute("Save.TV")]
        SaveTv,
        [DescriptionAttribute("TheMovieDB")]
        Tmdb,
        [DescriptionAttribute("TheTVDB")]
        Tvdb,
        [DescriptionAttribute("Fernsehserien.de")]
        Fernsehserien
    }

    public enum Categories
    {
        [DescriptionAttribute("unbekannt")]
        Unknown,
        [DescriptionAttribute("Filme")]
        Movie,
        [DescriptionAttribute("Serien")]
        Series,
        [DescriptionAttribute("Info")]
        Info,
        [DescriptionAttribute("Andere")]
        Other
    }

    public class EpisodeCode
    {
        public string Season;
        public int Episode;
        public int AbsoluteEpisode;

        public EpisodeCode(string season, int episode)
        {
            Season = season;
            Episode = episode;
        }

        public EpisodeCode(int season, int episode)
        {
            Season = season.ToString("D2");
            Episode = episode;
        }

        public EpisodeCode() { }

        [JsonIgnore]
        public string xCode
        {
            get
            {
                string S = String.IsNullOrEmpty(Season) ? "1" : Season;
                return S + "x" + Episode.ToString("D2");
            }
        }

        [JsonIgnore]
        public string sCode
        {
            get
            {
                string S = String.IsNullOrEmpty(Season) ? "1" : Season;
                return "S" + S + "E" + Episode.ToString("D2");
            }
        }

        public string Code(EpisodeCodeTypes type)
        {
            switch (type)
            {
                case EpisodeCodeTypes.sCode: return sCode;
                case EpisodeCodeTypes.xCode: return xCode;
                default: return xCode;
            }
        }

        private bool HasTextSeason
        {
            get
            {
                return NumSeason == 0;
            }
        }

        private int NumSeason
        {
            get
            {
                int S;
                int.TryParse(Season, out S);
                return S;
            }
        }

        public static int Compare(EpisodeCode c1, EpisodeCode c2)
        {
            int result;
            if (c1.HasTextSeason | c2.HasTextSeason)
            {
                result = String.Compare(c1.Season, c2.Season);
            }
            else
            {
                result = c1.NumSeason - c2.NumSeason;
            }
            if (result == 0)
            {
                result = c1.Episode - c2.Episode;
            }
            return result;
        }

        public static bool TryParse(string s, out EpisodeCode result)
        {
            int Seas;
            int Epis;
            if (s.Count(ch => ch == 'x') == 1)
            {
                string seas = s.Substring(0, s.IndexOf("x"));
                string epis = s.Substring(s.IndexOf("x") + 1, s.Length - s.IndexOf("x") - 1);
                bool isSeason = int.TryParse(seas, out Seas);
                bool isEpisode = int.TryParse(epis, out Epis);

                if (isSeason & isEpisode)
                {
                    result = new EpisodeCode(Seas, Epis);
                    return true;
                }
                else
                {
                    result = null;
                    return false;
                }
            }

            else if (s.IndexOf("S") == 0 & s.IndexOf("E") == 3)
            {
                string seas = s.Substring(1, s.IndexOf("E") - 1);
                string epis = s.Substring(s.IndexOf("E") + 1, s.Length - s.IndexOf("E") - 1);
                bool isSeason = int.TryParse(seas, out Seas);
                bool isEpisode = int.TryParse(epis, out Epis);

                if (isSeason & isEpisode)
                {
                    result = new EpisodeCode(Seas, Epis);
                    return true;
                }
                else
                {
                    result = null;
                    return false;
                }
            }

            else if (int.TryParse(s, out Epis))
            {
                result = new EpisodeCode("", Epis);
                return true;
            }

            else
            {
                result = null;
                return false;
            }
        }
    }

    public enum EpisodeCodeTypes
    {
        xCode,  // 1x01
        sCode   // S01E01
    }
}

