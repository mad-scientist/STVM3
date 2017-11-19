using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Web;
using System.IO;
using System.Threading.Tasks;

namespace STVM.Wrapper.Http
{
    public class httpRequest
    {
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
                string query = this.ToString();
                return String.IsNullOrEmpty(query) ? String.Empty : "?" + query; 
            }

            public FormUrlEncodedContent ToContent()
            {
                return new FormUrlEncodedContent(this);
            }

            public byte[] ByteEncode()
            {
                return System.Text.Encoding.ASCII.GetBytes(this.ToString()); 
            }
        }

        public event ServerErrorEventHandler ServerErrorEvent;
        public HttpParameters GetParameters;
        public HttpParameters PostParameters;
        public string Response { get; private set; }
        private HttpClient client;

        public async Task<HttpStatusCode> GetAsync(string URL)
        {
            HttpStatusCode result;
            try
            {
                HttpResponseMessage synoResponse = await client.GetAsync(URL + GetParameters.ToQuery()).ConfigureAwait(false);
                synoResponse.EnsureSuccessStatusCode();
                var raw_response = await synoResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                Response = Encoding.UTF8.GetString(raw_response, 0, raw_response.Length);
                result = synoResponse.StatusCode;
            }
            catch(Exception e)
            {
                if (ServerErrorEvent != null) { ServerErrorEvent(new ServerErrorEventArgs(e.Message)); }
                result = HttpStatusCode.ServiceUnavailable;
            }
            finally
            {
                // clear request parameters
                GetParameters.Clear();
                PostParameters.Clear();
            }
            return result;
        }

        public async Task<HttpStatusCode> PostAsync(string URL, string Referer)
        {
            client.DefaultRequestHeaders.Add("referer", Referer);
            return await PostAsync(URL);
        }

        public async Task<HttpStatusCode> PostAsync(string URL)
        {
            HttpStatusCode result;
            try
            {
                HttpResponseMessage synoResponse = await client.PostAsync(URL + GetParameters.ToQuery(), PostParameters.ToContent()).ConfigureAwait(false);
                synoResponse.EnsureSuccessStatusCode();
                var raw_response = await synoResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                Response = Encoding.UTF8.GetString(raw_response, 0, raw_response.Length);
                result = synoResponse.StatusCode;
            }
            catch (Exception e)
            {
                if (ServerErrorEvent != null) { ServerErrorEvent(new ServerErrorEventArgs(e.Message)); }
                result = HttpStatusCode.ServiceUnavailable;
            }
            finally
            {
                GetParameters.Clear();
                PostParameters.Clear();
            }
            return result;
        }

        public httpRequest()
        {
            client = new HttpClient();
            GetParameters = new HttpParameters();
            PostParameters = new HttpParameters();
        }

    }

    // event fired on server error
    public delegate void ServerErrorEventHandler(ServerErrorEventArgs e);
    public class ServerErrorEventArgs : EventArgs
    {
        public readonly string Message;
        public ServerErrorEventArgs(string message)
        {
            Message = message;
        }
    }


}
