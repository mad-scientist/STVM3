using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Threading.Tasks;
using STVM.Wrapper.Http;

namespace STVM.Wrapper.JDL
{
    class JdlWrapper
    {
        private const string jdlURL = "http://127.0.0.1:9666/flashgot";

        public string Referer { get; set; }

        public JdlWrapper()
        { }

        public async Task<bool> taskCreate(string URL, string Description, string DestinationPath)
        {
            return await taskCreate(new[] { URL }, new[] { Description }, DestinationPath);
        }

        public void OnLoginError(ServerErrorEventArgs e)
        {
            System.Windows.Forms.MessageBox.Show("Es kann keine Verbindung mit JDownloader aufgenommen werden. Ist JDownloader gestartet?", "JDownloader", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
        }

        public void OnServerError(ServerErrorEventArgs e)
        {
            System.Windows.Forms.MessageBox.Show("Downloads können nicht übergeben werden. Ist JDownloader gestartet?", "JDownloader", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
        }

        public async Task<bool> Login()
        {
            httpRequest jdl = new httpRequest();
            jdl.ServerErrorEvent += OnLoginError;

            return await jdl.GetAsync(jdlURL) == HttpStatusCode.OK;
        }

        public async Task<bool> taskCreate(IEnumerable<string> URLs, IEnumerable<string> Descriptions, string DestinationPath)
        {
            bool result = true;
            for (int i = 0; i < URLs.Count(); i+=100)
            {
                httpRequest jdl = new httpRequest();
                jdl.ServerErrorEvent += OnServerError;

                jdl.PostParameters.Add("urls", string.Join("\n", URLs.Skip(i).Take(100)));
                jdl.PostParameters.Add("description", string.Join("\n", Descriptions.Skip(i).Take(100)));
                jdl.PostParameters.Add("referer", Referer);
                if (Directory.Exists(DestinationPath)) { jdl.PostParameters.Add("dir", DestinationPath); }
                jdl.PostParameters.Add("autostart", "1");
                result = result & (await jdl.PostAsync(jdlURL, "http://127.0.0.1:9666/flashgot") == HttpStatusCode.OK);
            }
            return result;
        }
    }

 

}
