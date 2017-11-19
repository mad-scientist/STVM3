using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using STVM.Data;
using STVM.Wrapper.JDL;
using System.Threading.Tasks;

namespace STVM.Download
{
    class dlmJDownloader : dlmBase
    {
        private JdlWrapper jdl;
        private Timer demon;

        public override string Name
        {
            get { return "JDownloader"; }
        }

        public dlmJDownloader()
            : base()
        {
            Method = Data.DownloadMethods.JDownloader;
            CanCancel = false;
            NeedCredentials = false;
            jdl = new JdlWrapper()
            {
                Referer = "STV MANAGER"
            };
            demon = new Timer();
            demon.Interval = 10 * 1000;
            demon.Tick += demon_Tick;
            demon.Enabled = true;
        }

        private void demon_Tick(object sender, EventArgs e)
        {
            foreach (tDownload Download in internalDownloads)
            {
                if (File.Exists(Path.Combine(DestinationPath, Download.stvDownloadFilename)))
                {
                    Download.Status = DownloadStatus.Finished;
                }
            }
        }

        public override Task<bool> Login()
        {
            return jdl.Login();
        }

        public string buildTelecastLink(tDownload Download)
        {
            string result = "https://www.save.tv/STV/M/obj/archive/VideoArchiveDetails.cfm?TelecastId=";
            result += Download.TelecastID.ToString();
            result += "&adsfree=" + Download.AdFree.ToString().ToLower();
            result += "&preferformat=" + ((int)Download.Format).ToString();
            return result;
        }

        public override async Task AddRange(IEnumerable<tDownload> Downloads)
        {
            IEnumerable<string> downloadURLs = SendTelecastLink ?
                Downloads.Select(download => buildTelecastLink(download)) :
                Downloads.Select(download => download.stvDownloadURL);

            internalDownloads.AddRange(Downloads);
            DownloadStatus result =
                await jdl.taskCreate(downloadURLs, Downloads.Select(download => download.localFilename), DestinationPath) ? DownloadStatus.Waiting : DownloadStatus.Error;
            foreach (tDownload Download in Downloads)
            {
                Download.Status = result;
            }
        }

        public override Task RestoreRange(IEnumerable<tDownload> Downloads)
        {
            internalDownloads.AddRange(Downloads);
            return Task.Delay(0);
        }

    }
}
