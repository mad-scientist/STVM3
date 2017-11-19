using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using STVM.Data;
using System.Threading.Tasks;

namespace STVM.Download
{
    class dlmExternal : dlmBase
    {
        private Timer demon;

        public override string Name
        {
            get { return "Extern"; }
        }

        public dlmExternal()
            : base()
        {
            Method = Data.DownloadMethods.ExternalDlm;
            CanCancel = false;
            NeedCredentials = false;
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

        public override Task AddRange(IEnumerable<tDownload> Downloads)
        {
            IEnumerable<string> downloadURLs = SendTelecastLink ?
                Downloads.Select(download => "https://www.save.tv/STV/M/obj/archive/VideoArchiveDetails.cfm?TelecastId=" + download.TelecastID.ToString()) :
                Downloads.Select(download => download.stvDownloadURL);

            internalDownloads.AddRange(Downloads);
            System.Windows.Forms.Clipboard.SetText(string.Join("\r\n", downloadURLs));
            foreach (tDownload Download in Downloads)
            {
                Download.Status = DownloadStatus.Waiting;
            }
            return Task.Delay(0);
        }

        public override Task RestoreRange(IEnumerable<tDownload> Downloads)
        {
            internalDownloads.AddRange(Downloads);
            return Task.Delay(0);
        }
    }
}
