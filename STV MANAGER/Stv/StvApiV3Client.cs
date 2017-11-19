using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Net;
using System.Net.Http;
using System.Web.Script.Serialization;
using STVM.Data;

namespace STVM.Stv.ApiV3
{
    class StvClientV3
    {
        public event LogEventHandler LogEvent;

        private string username;
        private string password;

        public string Username
        {
            get { return username; }
            set
            {
                if (value != username)
                {
                    username = value;
                    auth.Reset();
                }
            }
        }
        public string Password
        {
            get { return password; }
            set
            {
                if (value != password)
                {
                    password = value;
                    auth.Reset();
                }
            }
        }

        private const string baseUrl = "https://api.save.tv:443";
        private const string authUrl = "https://auth.save.tv";
        private const string clientId = "07f805dc51224ed783cc68bf7284a191";
        private const string clientSecret = "96625a899fee42bbbdc9809f4503675ff53b6fe24cb541228092ad4993858005";

        private AuthResponse auth; 

        public StvClientV3()
        {
            auth = new AuthResponse();
        }

        private async Task<AuthResponse> Authorize()
        {
            if (auth.Expiry > DateTime.Now.AddMinutes(5))
            {
                return auth;
            }
            else
            {
                // reset token
                auth.Reset();

                QueryBuilder authQuery = new QueryBuilder() {
                    { "grant_type", "password" },
                    { "client_id", clientId },
                    { "client_secret", clientSecret },
                    { "username", Username },
                    { "password", Password }
                };

                HttpClient savetv = new HttpClient();
                savetv.BaseAddress = new Uri(authUrl);
                HttpRequestMessage stvRequest = new HttpRequestMessage(HttpMethod.Post, "/token");
                stvRequest.Content = authQuery.ToContent();

                if (LogEvent != null) { LogEvent(new LogEventArgs("Authorization request for Username: " + Username + " Password: ****")); }
                HttpResponseMessage stvResponse = await savetv.SendAsync(stvRequest);
                var raw_response = await stvResponse.Content.ReadAsByteArrayAsync();
                string string_response = Encoding.UTF8.GetString(raw_response, 0, raw_response.Length);

                switch (stvResponse.StatusCode)
                {
                    case HttpStatusCode.OK:
                        {
                            var jss = new JavaScriptSerializer();
                            auth.Token = jss.Deserialize<Token>(string_response);
                            auth.Expiry = DateTime.Now.AddSeconds(auth.Token.expires_in);
                            auth.Success = true;
                            if (LogEvent != null) { LogEvent(new LogEventArgs("Authorization received")); }
                            break;
                        }
                    case (HttpStatusCode)400:
                    case HttpStatusCode.Unauthorized:
                    case (HttpStatusCode)422:
                        {
                            var jss = new JavaScriptSerializer();
                            auth.Error = jss.Deserialize<AuthError>(string_response);
                            auth.Success = false;
                            if (LogEvent != null) { LogEvent(new LogEventArgs("Authorization error: " + auth.Error.error_description)); }
                            break;
                        }
                    default:
                        auth.Error.error_description = "Unbekannter Serverfehler";
                        auth.Success = false;
                        if (LogEvent != null) { LogEvent(new LogEventArgs("Request Error: Unbekannter Serverfehler")); }
                        break;
                }

                return auth;
            }
        }

        public async Task<Response<T>> ProcessResponse<T>(HttpResponseMessage stvResponse)
        {
            Response<T> result = new Response<T>();
            var raw_response = await stvResponse.Content.ReadAsByteArrayAsync();
            string string_response = Encoding.UTF8.GetString(raw_response, 0, raw_response.Length);
            var jss = new JavaScriptSerializer();

            switch (stvResponse.StatusCode)
            {
                case HttpStatusCode.OK:
                    result.Success = true;
                    result.Data = jss.Deserialize<T>(string_response);
                    if (LogEvent != null) { LogEvent(new LogEventArgs("Success [" + result.Data.GetType().ToString() + "]")); }
                    break;

                case HttpStatusCode.Unauthorized:
                case (HttpStatusCode)422:
                    result.Success = false;
                    result.Errors = jss.Deserialize<List<Error>>(string_response);
                    if (LogEvent != null) { LogEvent(new LogEventArgs("Request Error: " + result.Errors[0].userMessage)); }
                    break;

                default:
                    result.Success = false;
                    result.Errors.Add(new Error { userMessage = "Unbekannter Serverfehler" });
                    if (LogEvent != null) { LogEvent(new LogEventArgs("Request Error: Unbekannter Serverfehler")); }
                    break;
            }
            return result;
        }

        public async Task<Response<T>> Request<T>(HttpMethod Command, string Element, dynamic Query)
        {
            AuthResponse stvAuth = await Authorize();
            if (stvAuth.Success)
            {
                HttpClient savetv = new HttpClient();
                savetv.BaseAddress = new Uri(baseUrl);
                string requestUri = (Command == HttpMethod.Post) ? Element : Element + Query.ToQuery();
                HttpRequestMessage stvRequest = new HttpRequestMessage(Command, requestUri);
                stvRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", stvAuth.Token.access_token);
                if (LogEvent != null) { LogEvent(new LogEventArgs("Request: " + requestUri)); }

                HttpResponseMessage stvResponse = new HttpResponseMessage();
                if (Command == HttpMethod.Get | Command == HttpMethod.Delete)
                {
                    stvResponse = await savetv.SendAsync(stvRequest);
                }
                else if (Command == HttpMethod.Post)
                {
                    string Content = new JavaScriptSerializer().Serialize(Query);
                    stvRequest.Content = new StringContent(Content, Encoding.UTF8, "application/json");
                    stvResponse = await savetv.SendAsync(stvRequest);
                }
                return await ProcessResponse<T>(stvResponse);
            }
            else
            {
                string errorMessage;
                if (stvAuth.Error.error_description.StartsWith("Login failed"))
                {
                    errorMessage = "Login für User " + Username + " nicht erfolgreich";
                }
                else
                {
                    errorMessage = stvAuth.Error.error_description;
                }
                return new Response<T>()
                {
                    Success = false,
                    Errors = new List<Error>() { new Error() { userMessage = errorMessage } }
                };
            }
        }

        public async Task<Response<T>> GetRequest<T>(string Request, QueryBuilder Query)
        {
            return await Request<T>(HttpMethod.Get, Request, Query);
        }

        public async Task<Response<T>> DeleteRequest<T>(string Request, QueryBuilder Query)
        {
            return await Request<T>(HttpMethod.Delete, Request, Query);
        }

        public async Task<Response<T>> PostRequest<T>(string Request, dynamic Query)
        {
            return await Request<T>(HttpMethod.Post, Request, Query);
        }
    }
}
