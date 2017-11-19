using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.IO;
using System.Web;
using System.Web.Script.Serialization;
using System.Collections.Specialized;
using System.ComponentModel;
using STVM.Stv.Data;


namespace STVM.Stv.Http
{
    public class LoginStatusEventArgs : EventArgs
    {
        public readonly bool LoginStatus;
        public LoginStatusEventArgs(bool loginStatus)
        {
            LoginStatus = loginStatus;
        }
    }

    public class stvHTTP
    {
        const string stvServerURL = "https://www.save.tv";
        const string stvLoginURL = "/STV/M/Index.cfm";
        const string stvDownloadURL = "/STV/M/obj/cRecordOrder/croGetDownloadUrl.cfm";
        const string stvFrameUrl = "/STV/M/misc/miscShowFrameSet.cfm";

        public delegate void LoginStatusEventHandler(object sender, LoginStatusEventArgs e);
        public event LoginStatusEventHandler LoginStatusEvent;

        private HttpClient client;

        private string stvUsername = "";
        private string stvPassword = "";

        public string Username
        {
            get { return this.stvUsername; }
            set { this.stvUsername = value; }
        }

        public string Password
        {
            get { return this.stvPassword; }
            set { this.stvPassword = value; }
        }

        private bool stvLoginSuccess = false;

        public bool LoginSuccess
        {
            get { return stvLoginSuccess; }
        }

        public async Task<bool> Login()
        {
            // already logged in?
            if (!stvLoginSuccess)
            {

                HttpParameters Parameters = new HttpParameters();
                Parameters.Add("sUsername", stvUsername);
                Parameters.Add("sPassword", stvPassword);

                HttpResponseMessage response = await client.PostAsync(stvLoginURL, Parameters.ToContent());
                // successful login returns redirect to /STV/M/misc/miscShowFrameSet.cfm
                stvLoginSuccess = (response.StatusCode == HttpStatusCode.Found) &&
                    (response.Headers.Location.ToString() == stvFrameUrl);

                if (stvLoginSuccess)
                {
                    if (LoginStatusEvent != null)
                    {
                        LoginStatusEventArgs e = new LoginStatusEventArgs(true);
                        LoginStatusEvent(this, e);
                    }
                }
                else
                {
                    Logout();
                }
            }
            return stvLoginSuccess;
        }

        public void Logout()
        {
            stvLoginSuccess = false;
            if (LoginStatusEvent != null)
            {
                LoginStatusEventArgs e = new LoginStatusEventArgs(false);
                LoginStatusEvent(this, e);
            }
        }

        public GetDownloadUrlResponse ReadFromJson(string json)
        {
            GetDownloadUrlResponse result = new GetDownloadUrlResponse();

            var jss = new JavaScriptSerializer();
            GetDownloadUrlResponseObject stvResponse = jss.Deserialize<GetDownloadUrlResponseObject>(json);

            result.TelecastID = Int32.Parse(stvResponse.ARRVIDEOURL[0]);
            result.Success = stvResponse.ARRVIDEOURL[1] == "OK";

            if (!result.Success) { result.Message = stvResponse.ARRVIDEOURL[2]; }
            else
            {
                result.DownloadUrl = stvResponse.ARRVIDEOURL[2];
                result.StreamingUrl = stvResponse.ARRVIDEOURL[3];
                result.BrowserUrl = stvResponse.ARRVIDEOURL[4];
                result.VideoFormat = (RecordFormats)(Int32.Parse(stvResponse.ARRVIDEOURL[5].Substring(0,1)));
            }
            return result;
        }

        public async Task<GetDownloadUrlResponse> GetDownloadURL(int ID, RecordFormats VideoFormat, bool AdFree)
        {
            if (await Login())
            {
                HttpParameters Parameters = new HttpParameters();
                Parameters.Add("TelecastId", ID.ToString());
                Parameters.Add("iFormat", GetHttpVideoFormat(VideoFormat));
                Parameters.Add("bAdFree", AdFree ? "1" : "0");
                string json = await client.GetStringAsync(stvDownloadURL + Parameters.ToQuery()).ConfigureAwait(false);

                return ReadFromJson(json);
            }
            else { return new GetDownloadUrlResponse(); }
        }

        public async Task<string> GetDownloadFilename(string DownloadUrl)
        {
            HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Head, DownloadUrl);
            HttpResponseMessage response = await client.SendAsync(message);
            return response.Content.Headers.ContentDisposition.FileName;
        }

        public stvHTTP()
        {
            HttpClientHandler clientHandler = new HttpClientHandler()
            {
                AllowAutoRedirect = false
            };
            client = new HttpClient(clientHandler);
            client.BaseAddress = new Uri(stvServerURL);
        }

        private string GetHttpVideoFormat(RecordFormats VideoFormat)
        {
            switch (VideoFormat)
            {
                case RecordFormats.SD: return "0";
                case RecordFormats.Mobile: return "1";
                case RecordFormats.HD: return "2";
                default: return "0";
            }
        }
    }

    public class GetDownloadUrlResponseObject
    {
        public List<string> ARRVIDEOURL { get; set; }
    }

    public class GetDownloadUrlResponse
    {
        public int TelecastID { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public string DownloadUrl { get; set; }
        public string StreamingUrl { get; set; }
        public string BrowserUrl { get; set; }
        public RecordFormats VideoFormat { get; set; }

        public GetDownloadUrlResponse()
        {
            TelecastID = 0;
            Success = false;
            Message = "";
            DownloadUrl = "";
            StreamingUrl = "";
            BrowserUrl = "";
            VideoFormat = RecordFormats.Undefined;
        }

   }

    public class HttpParameters : Dictionary<string, string>
    {
        public override string ToString()
        {
            NameValueCollection query = HttpUtility.ParseQueryString(String.Empty);
            foreach (string Key in this.Keys)
            {
                query[Key] = this[Key];
            }
            return query.ToString();
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
