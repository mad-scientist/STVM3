using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Net.Http;
using System.Globalization;
using System.ComponentModel;

namespace STVM.Stv.ApiV3
{
    public class AuthResponse
    {
        public bool Success { get; set; }
        public AuthError Error { get; set; }
        public Token Token { get; set; }
        public DateTime Expiry { get; set; }

        public AuthResponse()
        {
            Success = false;
            Error = new AuthError();
            Token = new Token();
            Expiry = DateTime.Now;
        }

        public void Reset()
        {
            Success = false;
            Error = new AuthError();
            Token = new Token();
            Expiry = DateTime.Now;
        }
    }

    public class AuthError
    {
        public string error { get; set; }
        public string error_description { get; set; }
    }

    public class Token
    {
        public string access_token { get; set; }
        public string token_type { get; set; }
        public int expires_in { get; set; }
    }

    public class Response<T>
    {
        public bool Success { get; set; }
        public List<Error> Errors { get; set; }
        public T Data { get; set; }

        public Response()
        {
            Errors = new List<Error>();
        }
    }

    public class Error
    {
        public List<string> developerMessages { get; set; }
        public List<object> properties { get; set; }
        public string reference { get; set; }
        public string userMessage { get; set; }
        public string id { get; set; }
    }

    public enum RecordStates
    /*
    1 = The user has requested the format.
    2 = The format was successfully recorded or the recording process failed.
    3 = The format was recorded and encoded successful and the user can download the format.
    4 = The recording or encoding process produced errors. The user cannot download the format.
    5 = The user has deleted the format. */
    {
        Programmed = 1,
        Encoding = 2,
        InArchive = 3,
        Broken = 4,
        Deleted = 5
    }

    public enum RecordStateIds
    /*
    1 = Aufnahme programmiert
    2 = Aufnahme läuft
    4 = Aufnahme verfügbar */
    {
        Programmed = 1,
        Encoding = 2,
        InArchive = 4
    }

    public class RecordCount
    {
        public int count { get; set; }
        public int? usedLength { get; set; }
        public int? allowedLength { get; set; }
    }

    public class RecordState
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class RecordFormat
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class Format
    {
        public RecordState recordState { get; set; }
        public RecordFormat recordFormat { get; set; }
        public int uncutVideoSize { get; set; }
        public string retentionDate { get; set; }
    }

    public class TvCategory
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class TvStation
    {
        public int id { get; set; }
        public bool isRecordable { get; set; }
        public string largeLogoUrl { get; set; }
        public string smallLogoUrl { get; set; }
        public string name { get; set; }
    }

    public class Telecast
    {
        public const string fieldsDefault = "id,startdate,enddate,episode,subject,subtitle,title,tvcategory.id,tvstation.id,tvstation.name,year,country,description,imageurl500";
        public const string fieldsDetail = "id,description,imageurl500";

        public int id { get; set; }
        public string endDate { get; set; }
        public string startDate { get; set; }
        public TvCategory tvCategory { get; set; }
        public TvStation tvStation { get; set; }
        public string subject { get; set; }
        public string episode { get; set; }
        public int year { get; set; }
        public string country { get; set; }
        public string subTitle { get; set; }
        public string title { get; set; }
        public string imageUrl500 { get; set; }
        public string description { get; set; }

        public DateTime startDateLocal
        {
            get
            {
                DateTime serverTime;
                DateTime.TryParse(startDate,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out serverTime);
                return TimeZoneInfo.ConvertTimeFromUtc(serverTime, TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time"));
            }
        }

        public DateTime endDateLocal
        {
            get
            {
                DateTime serverTime;
                DateTime.TryParse(endDate,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out serverTime);
                return TimeZoneInfo.ConvertTimeFromUtc(serverTime, TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time"));
            }
        }
    }

    public class TelecastQuery
    {
        public int[] dayOfWeek { get; set; }
        public string exactTitle { get; set; }
        public DateTime? minStartDate { get; set; }
        public DateTime? maxStartDate { get; set; }
        public DateTime? minStartTime { get; set; }
        public DateTime? maxStartTime { get; set; }
        public string searchText { get; set; }
        public bool searchFullText { get; set; }
        public int tvStation { get; set; }

        public TelecastQuery()
        {
            dayOfWeek = new int[] { };
            exactTitle = "";
            searchText = "";
            tvStation = 0;
        }
    }

    public class Tag
    {
        public string value { get; set; }
        public string key { get; set; }
    }

    public class Channel
    {
        public int id { get; set; }
        public string name { get; set; }
    }

    public class Record
    {
        public const string fieldsDefault = "telecastid,updatedate,adfreeavailable,formats,tags.key,tags.value,channels.id,channels.name,telecast.id,telecast.startdate,telecast.enddate,telecast.episode,telecast.subject,telecast.subtitle,telecast.title,telecast.tvcategory.id,telecast.tvstation.id,telecast.tvstation.name,telecast.year,telecast.country,telecast.description,telecast.imageurl500";

        public int telecastId { get; set; }
        public List<Format> formats { get; set; }
        public List<Tag> tags { get; set; }
        public List<Channel> channels { get; set; }
        public Telecast telecast { get; set; }
        public bool adFreeAvailable { get; set; }
        public string updateDate { get; set; }
    }

    public class PostRecordRequest
    {
        public List<int> telecastIds { get; set; }
        public int followUpTime { get; set; }
        public int leadTime { get; set; }
    }

    public class RecordResponse
    {
        public bool success { get; set; }
        public int telecastId { get; set; }
        public List<Error> errors { get; set; }
    }

    public class Contract
    {
        public bool hasXXLPackage { get; set; }
        public bool hasXLPackage { get; set; }
    }

    public class RecordBuffer
    {
        public int followUpTime { get; set; }
        public int leadTime { get; set; }
    }

    public class User
    {
        public Contract contract { get; set; }
        public RecordBuffer recordBuffer { get; set; }
        public string fullName { get; set; }
        public string id { get; set; }
    }

    public class RecordDownloadUrl
    {
        public int estimatedFileSize { get; set; }
        public string fileName { get; set; }
        public string htmlVideoPlayerUrl { get; set; }
        public string streamingUrl { get; set; }
        public string downloadUrl { get; set; }
    }

    // event fired on Save.TV server error
    public delegate void SaveTvErrorEventHandler(SaveTvErrorEventArgs e);
    public class SaveTvErrorEventArgs : EventArgs
    {
        public readonly string Message;
        public SaveTvErrorEventArgs(string message)
        {
            Message = message;
        }
    }

    public class QueryBuilder : Dictionary<string, string>
    {
        public override string ToString()
        {
            NameValueCollection query = HttpUtility.ParseQueryString(String.Empty);
            foreach (string Key in this.Keys)
            {
                query[Key] = this[Key];
            }

            // this code block is taken from httpValueCollection.ToString() method
            // and modified so it encodes strings with HttpUtility.UrlEncode
            if (query.Count == 0)
                return String.Empty;
            else
            {
                var sb = new StringBuilder();

                for (int i = 0; i < query.Count; i++)
                {
                    string text = query.GetKey(i);
                    {
                        text = HttpUtility.UrlEncode(text);

                        string val = (text != null) ? (text + "=") : string.Empty;
                        string[] vals = query.GetValues(i);

                        if (sb.Length > 0)
                            sb.Append('&');

                        if (vals == null || vals.Length == 0)
                            sb.Append(val);
                        else
                        {
                            if (vals.Length == 1)
                            {
                                sb.Append(val);
                                sb.Append(HttpUtility.UrlEncode(vals[0]));
                            }
                            else
                            {
                                for (int j = 0; j < vals.Length; j++)
                                {
                                    if (j > 0)
                                        sb.Append('&');

                                    sb.Append(val);
                                    sb.Append(HttpUtility.UrlEncode(vals[j]));
                                }
                            }
                        }
                    }
                }
                return sb.ToString();
            }
        }

        public new void Add(string key, string value)
        {
            if (!String.IsNullOrEmpty(value))
            {
                base.Add(key, value);
            }
        }

        public string ToQuery()
        {
            return "?" + this.ToString();
        }

        public FormUrlEncodedContent ToContent()
        {
            return new FormUrlEncodedContent(this);
        }
    }

}
