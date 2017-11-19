using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using STVM.Data;
using STVM.Wrapper.Synology;
using System.Threading.Tasks;

namespace STVM.Download
{
    class dlmSynology : dlmBase
    {
        private Timer synoDemon;
        private bool Lock;
        private apiDownloadStation syno;
        private new List<tSynoDownload> internalDownloads;

        public override string Username
        {
            get { return syno.Username; }
            set { syno.Username = value; }
        }
        public override string Password
        {
            get { return syno.Password; }
            set { syno.Password = value; }
        }
        public override string Address
        {
            get { return syno.Address; }
            set { syno.Address = value; }
        }
        public override int Port
        {
            get { return syno.Port; }
            set { syno.Port = value; }
        }

        public override bool UseHttps
        {
            get { return syno.useHTTPS; }
            set { syno.useHTTPS = value; }
        }

        public override string Name
        {
            get { return "Synology Download Station"; }
        }

        public override event LogEventHandler LogEvent
        {
            add { syno.LogEvent += value; }
            remove { syno.LogEvent -= value; }
        }

        public dlmSynology()
            : base()
        {
            Method = Data.DownloadMethods.Synology;
            CanCancel = true;
            NeedCredentials = true;
            internalDownloads = new List<tSynoDownload>();
            synoDemon = new Timer();
            synoDemon.Interval = 5 * 1000;
            synoDemon.Tick += synoDemon_Tick;
            syno = new apiDownloadStation();
            Lock = false;
        }

        async void synoDemon_Tick(object sender, EventArgs e)
        {
            if (!Lock && internalDownloads.Any(dl => dl.Download.Status < DownloadStatus.Finished))
            {
                Lock = true;
                synoGetInfoResponse info = await syno.taskGetInfo(internalDownloads.Select(i => i.ID));

                foreach (tSynoDownload internalDownload in internalDownloads)
                {
                    tDownload download = internalDownload.Download;
                    synoTask task = info.data.tasks.FirstOrDefault(t => t.id == internalDownload.ID);
                    if (task == null)
                    {
                        if (download.Status < DownloadStatus.Finished &&
                            File.Exists(Path.Combine(DestinationPath, download.stvDownloadFilename)))
                        {
                            download.Status = DownloadStatus.Finished;
                        }
                    }
                    else
                    {
                        switch (task.status)
                        {
                            case "downloading":
                            case "2":
                                download.Status = DownloadStatus.Progressing;
                                download.Size = (int)(task.size / 1024 / 1024);
                                download.Received = (int)(task.additional.transfer.size_downloaded / 1024 / 1024);
                                break;

                            case "error":
                            case "10":
                                download.Status = DownloadStatus.Error;
                                break;

                            case "finished":
                            case "5":
                                if (File.Exists(Path.Combine(DestinationPath, download.stvDownloadFilename)))
                                {
                                    download.Status = DownloadStatus.Finished;
                                    internalDownload.ID = "";
                                }
                                break;
                        }
                    }
                }
                Lock = false;
            }

            // regelmäßig aufräumen, wenn die IDs leer sind
            internalDownloads.RemoveAll(sd => sd.ID == "");
        }

        private async Task Remove(tSynoDownload internalDownload)
        {
            synoDeleteResponse response = await syno.taskDelete(new[] {internalDownload.ID});
            internalDownloads.Remove(internalDownload);
        }

        public override async Task<bool> Login()
        {
            bool synoLogin = await syno.Login(); 
            synoDemon.Enabled = synoLogin;
            return synoLogin;
        }

        public override async Task Logout()
        {
            await syno.Logout();
        }

        public override async Task AddRange(IEnumerable<tDownload> Downloads)
        {
            Lock = true;
            if (await syno.taskCreate(Downloads.Select(download => download.stvDownloadURL)))
            {
                synoListResponse tasks = await syno.taskList();

                foreach (tDownload download in Downloads)
                {
                    tSynoDownload sd = new tSynoDownload()
                    {
                        Download = download,
                        ID = tasks.data.tasks.Last(task => task.additional.detail.uri == download.stvDownloadURL).id
                    };
                    internalDownloads.Add(sd);

                    download.Status = DownloadStatus.Waiting;
                }
            }
            Lock = false;
        }

        public override async Task CancelRange(IEnumerable<tDownload> Downloads)
        {
            List<string> IDs = new List<string>();
            foreach (tDownload Download in Downloads)
            {
                tSynoDownload synoDownload = internalDownloads.FirstOrDefault(dl => dl.Download == Download);
                if (synoDownload != null && synoDownload.Download.Status == DownloadStatus.Progressing) { IDs.Add(synoDownload.ID); }
            }
            synoDeleteResponse deleteResponse = await syno.taskDelete(IDs);

            foreach(synoDeleteResponseData response in deleteResponse.data)
            {
                tSynoDownload sd = internalDownloads.First(dl => dl.ID == response.id);
                if (response.error == 0)
                {
                    sd.Download.Status = DownloadStatus.Cancelled;
                }
                else
                {
                    sd.Download.Status = DownloadStatus.Error;
                }
                sd.ID = "";
            }
        }

        public override async Task RestoreRange(IEnumerable<tDownload> Downloads)
        {
            synoListResponse synoTaskList = await syno.taskList();
            foreach (tDownload download in Downloads)
            {
                synoTask synoDownload = synoTaskList.data.tasks.LastOrDefault(task => task.additional.detail.uri == download.stvDownloadURL);
                string synoID = "";
                if (synoDownload != null)
                {
                    synoID = synoDownload.id;
                    synoTaskList.data.tasks.Remove(synoDownload);
                }

                tSynoDownload internalDownload = new tSynoDownload()
                {
                    Download = download,
                    ID = synoID
                };
                internalDownloads.Add(internalDownload);
            }
        }

        private class tSynoDownload
        {
            public tDownload Download;
            public string ID;
        }
    }
}
