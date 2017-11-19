using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Web;
using System.Web.Script.Serialization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Specialized;
using STVM.Dialogs;
using STVM.Data;
using STVM.Wrapper.Http;
using Renci.SshNet;

namespace STVM.Wrapper.Synology
{
    public class apiDownloadStation
    {
        private httpRequest httpDownloadStation;

        public string Address { get; set; }
        public int Port { get; set; }
        public bool useHTTPS { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public event LogEventHandler LogEvent;

        public apiDownloadStation()
        {
            httpDownloadStation = new httpRequest();
            Address = "";
            Port = 0;
            useHTTPS = true;
            Username = "";
            Password = "";
        }

        private string URL
        {
            get
            {
                string url = useHTTPS ? "https://" : "http://";
                url += Address + ":" + Port.ToString();
                url += "/webapi/";
                return url;
            }
        }

        private string UrlAuth
        {
            get { return URL + "auth.cgi"; }
        }

        private string UrlTask
        {
            get { return URL + "DownloadStation/task.cgi"; }
        }

        private bool HasCredentials()
        {
            return ((Username != "") && (Password != ""));
        }

        private string SessionID;

        public async Task<bool> Login()
        {
            // GET /webapi/auth.cgi?api=SYNO.API.Auth&version=2&method=login&account=admin&passwd=12345&session=DownloadStation&format=sid
            bool result = false;
            SessionID = "";

            httpRequest syno = new httpRequest();
            syno.GetParameters.Add("api", "SYNO.API.Auth");
            syno.GetParameters.Add("version", "2");
            syno.GetParameters.Add("method", "login");
            syno.GetParameters.Add("account", Username);
            syno.GetParameters.Add("passwd", Password);
            syno.GetParameters.Add("session", "DownloadStation");
            syno.GetParameters.Add("format", "sid");

            LogEvent(new LogEventArgs("Synology: " + UrlAuth + " account=" + Username + " passwd=********"));
            HttpStatusCode Status = await syno.GetAsync(UrlAuth);
            string jsonResponse = syno.Response;
            LogEvent(new LogEventArgs("Synology: " + jsonResponse));

            if (!string.IsNullOrEmpty(jsonResponse))
            {
                var jss = new JavaScriptSerializer();
                var dict = jss.Deserialize<Dictionary<string, dynamic>>(jsonResponse);

                if (dict["success"] == true)
                {
                    SessionID = dict["data"]["sid"];
                    result = true;
                }
            }
            return result;
        }

        public async Task Logout()
        {
            // GET /webapi/auth.cgi?api=SYNO.API.Auth&version=1&method=logout&session=DownloadStation
            httpRequest syno = new httpRequest();
            syno.GetParameters.Add("_sid", SessionID);
            syno.GetParameters.Add("api", "SYNO.API.Auth");
            syno.GetParameters.Add("version", "1");
            syno.GetParameters.Add("method", "logout");
            syno.GetParameters.Add("session", "DownloadStation");

            HttpStatusCode Status = await syno.GetAsync(UrlAuth);
            string jsonResponse = syno.Response;
        }

        public bool sshMove(string Filename, string Destination, string SshPassword)
        {
            if (File.Exists(Filename) & SshPassword != "")
            {
                string linuxFilename = "\"" + Filename.Replace("\\", "/").Replace("Z:", "/volume1/Data") + "\"";
                string linuxDestination = "\"" + Destination.Replace("\\", "/").Replace("V:", "/volume1/video") + "/" + "\"";

                SshClient synoSSH = new SshClient(Address, "admin", SshPassword);
                synoSSH.Connect();

                // sicherstellen, dass das Zielverzeichnis existiert
                synoSSH.RunCommand("mkdir -p " + linuxDestination);  // -p  no error if existing, make parent directories as needed
                // Datei verschieben
                synoSSH.RunCommand("mv " + linuxFilename + " " + linuxDestination);

                synoSSH.Disconnect();
                return true;
            }
            else return false;
        }

        public async Task<bool> taskCreate(string URI)
        {
            return await taskCreate(new[] { URI });
        }

        public async Task<bool> taskCreate(IEnumerable<string> URIs)
        {
            // POST /webapi/DownloadStation/task.cgi
            // api=SYNO.DownloadStation.Task&version=1&method=create&uri=ftps://192.0.0.1:21/test/test.zip&username=admin&password=123

            bool result = true;
            int uriCount = URIs.Count();
            for (int i = 0; i < uriCount; i += 20)
            {
                string uriList = string.Join(",", URIs.Skip(i).Take(20));

                httpRequest syno = new httpRequest();
                syno.PostParameters.Add("_sid", SessionID);
                syno.PostParameters.Add("api", "SYNO.DownloadStation.Task");
                syno.PostParameters.Add("version", "1");
                syno.PostParameters.Add("method", "create");
                syno.PostParameters.Add("uri", uriList);

                LogEvent(new LogEventArgs("Synology: " + UrlTask + " _sid=" + SessionID + " method=create uri=" + uriList));
                HttpStatusCode Status = await syno.PostAsync(UrlTask);
                string jsonResponse = syno.Response;

                var jss = new JavaScriptSerializer();
                var response = jss.Deserialize<synoTaskCreateResponse>(jsonResponse);
                result = response.success;
                if (response.success)
                {
                    LogEvent(new LogEventArgs("Synology: Success"));
                }
                else
                {
                    LogEvent(new LogEventArgs("Synology: Error Code " + response.error.code.ToString()));
                }
                if (!result) break;
            }
            return result;
        }

        public async Task<synoListResponse> taskList()
        {
            httpRequest syno = new httpRequest();
            syno.GetParameters.Add("_sid", SessionID);
            syno.GetParameters.Add("api", "SYNO.DownloadStation.Task");
            syno.GetParameters.Add("version", "1");
            syno.GetParameters.Add("method", "list");
            syno.GetParameters.Add("additional", "detail");

            LogEvent(new LogEventArgs("Synology: " + UrlTask + " _sid=" + SessionID + " method=list"));
            HttpStatusCode Status = await syno.GetAsync(UrlTask);
            string jsonResponse = syno.Response;
            LogEvent(new LogEventArgs("Synology: " + jsonResponse));

            if (Status == HttpStatusCode.OK)
            {
                var jss = new JavaScriptSerializer();
                var result = jss.Deserialize<synoListResponse>(jsonResponse);
                return result;
            }
            else return new synoListResponse();
        }


        public async Task<synoGetInfoResponse> taskGetInfo(IEnumerable<string> IDs)
        {
            synoGetInfoResponse result = new synoGetInfoResponse();
            if (IDs.Any())
            {
                httpRequest syno = new httpRequest();
                syno.GetParameters.Add("_sid", SessionID);
                syno.GetParameters.Add("api", "SYNO.DownloadStation.Task");
                syno.GetParameters.Add("version", "1");
                syno.GetParameters.Add("method", "getinfo");
                syno.GetParameters.Add("id", string.Join(",", IDs));
                syno.GetParameters.Add("additional", "file,transfer");

                try
                {
                    LogEvent(new LogEventArgs("Synology: " + UrlTask + " _sid=" + SessionID + " method=getinfo id=" + String.Join(",", IDs)));
                    HttpStatusCode Status = await syno.GetAsync(UrlTask);
                    string jsonResponse = syno.Response;
                    LogEvent(new LogEventArgs("Synology: " + jsonResponse));
                    if (Status == HttpStatusCode.OK)
                    {
                        var jss = new JavaScriptSerializer();
                        result = jss.Deserialize<synoGetInfoResponse>(jsonResponse);
                    }
                }
                catch (Exception e)
                {
                    System.Windows.Forms.MessageBox.Show(e.Message);
                }
            }
            return result; 
        }

        public async Task<synoDeleteResponse> taskDelete(IEnumerable<string> IDs)
        {
            synoDeleteResponse result = new synoDeleteResponse();
            if (IDs.Any())
            {
                httpRequest syno = new httpRequest();
                syno.GetParameters.Add("_sid", SessionID);
                syno.GetParameters.Add("api", "SYNO.DownloadStation.Task");
                syno.GetParameters.Add("version", "1");
                syno.GetParameters.Add("method", "delete");
                syno.GetParameters.Add("id", string.Join(",", IDs));
                syno.GetParameters.Add("force_complete", "false");

                LogEvent(new LogEventArgs("Synology: " + UrlTask + " _sid=" + SessionID + " method=delete id=" + String.Join(",", IDs)));
                HttpStatusCode Status = await syno.GetAsync(UrlTask);
                string jsonResponse = syno.Response;
                LogEvent(new LogEventArgs("Synology: " + jsonResponse));

                if (Status == HttpStatusCode.OK)
                {
                    var jss = new JavaScriptSerializer();
                    result = jss.Deserialize<synoDeleteResponse>(jsonResponse);
                }
            }
            return result;
        }
    }


    public class synoDetail
    {
        public int connected_leechers { get; set; }
        public int connected_seeders { get; set; }
        public string create_time { get; set; }
        public string destination { get; set; }
        public string priority { get; set; }
        public int total_peers { get; set; }
        public string uri { get; set; }
    }

    public class synoFile
    {
        public string filename { get; set; }
        public string priority { get; set; }
        public string size { get; set; }
        public string size_downloaded { get; set; }
    }

    public class synoTransfer
    {
        public long size_downloaded { get; set; }
        public long size_uploaded { get; set; }
        public int speed_download { get; set; }
        public int speed_upload { get; set; }
    }

    public class synoAdditional
    {
        public synoDetail detail { get; set; }
        public List<synoFile> file { get; set; }
        public synoTransfer transfer { get; set; }
    }

    public class synoStatusExtra
    {
        public string error_detail { get; set; }
    }

    public class synoTask
    {
        public synoAdditional additional { get; set; }
        public string id { get; set; }
        public long size { get; set; }
        public string status { get; set; }
        public synoStatusExtra status_extra { get; set; }
        public string title { get; set; }
        public string type { get; set; }
        public string username { get; set; }
    }

    public class synoErrorCode
    {
        public int code { get; set; }
    }

    public class synoTaskCreateResponse
    {
        public synoErrorCode error { get; set; }
        public bool success { get; set; }

        public synoTaskCreateResponse()
        {
            error = new synoErrorCode();
            success = false;
        }
    }

    public class synoListReponseData
    {
        public int offeset { get; set; }
        public List<synoTask> tasks { get; set; }
        public int total { get; set; }

        public synoListReponseData()
        {
            tasks = new List<synoTask>();
        }
    }

    public class synoListResponse
    {
        public synoListReponseData data { get; set; }
        public synoErrorCode error { get; set; }
        public bool success { get; set; }

        public synoListResponse()
        {
            data = new synoListReponseData();
            error = new synoErrorCode();
            success = false;
        }
    }

    public class synoGetInfoResponseData
    {
        public List<synoTask> tasks { get; set; }

        public synoGetInfoResponseData()
        {
            tasks = new List<synoTask>();
        }
    }

    public class synoGetInfoResponse
    {
        public synoGetInfoResponseData data { get; set; }
        public synoErrorCode error { get; set; }
        public bool success { get; set; }

        public synoGetInfoResponse()
        {
            data = new synoGetInfoResponseData();
            error = new synoErrorCode();
            success = false;
        }
    }

    public class synoDeleteResponseData
    {
        public int error { get; set; }
        public string id { get; set; }
    }

    public class synoDeleteResponse
    {
        public List<synoDeleteResponseData> data { get; set; }
        public synoErrorCode error { get; set; }
        public bool success { get; set; }

        public synoDeleteResponse()
        {
            data = new List<synoDeleteResponseData>();
            error = new synoErrorCode();
            success = false;
        }
    }
}
