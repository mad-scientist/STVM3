using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using STVM;
using STVM.Data;
using System.Threading.Tasks;

namespace STVM.Download
{
    abstract class dlmBase
    {
        protected List<tDownload> internalDownloads;

        public string DestinationPath { get; set; }
        public int MaximumConnections { get; set; }
        public bool SendTelecastLink { get; set; }

        public virtual string Username { get; set; }
        public virtual string Password { get; set; }
        public virtual string Address { get; set; }
        public virtual int Port { get; set; }
        public virtual bool UseHttps { get; set; }

        public DownloadMethods Method { get; protected set; }
        public bool CanCancel { get; protected set; }
        public bool NeedCredentials { get; protected set; }

        public abstract string Name { get; }

        public virtual event LogEventHandler LogEvent;

        public dlmBase()
        {
            internalDownloads = new List<tDownload>();
            CanCancel = false;
            NeedCredentials = false;
            SendTelecastLink = false;
        }

        public virtual Task<bool> Login()
        {
            return Task.FromResult(true);
        }

        public virtual Task Logout()
        {
            return Task.Delay(0);
        }

        public virtual Task Add(tDownload Download)
        {
            return AddRange(new[] { Download });
        }

        public abstract Task AddRange(IEnumerable<tDownload> Downloads);

        public virtual void Restore(tDownload Download)
        {
            RestoreRange(new[] { Download });
        }

        public abstract Task RestoreRange(IEnumerable<tDownload> Downloads);

        public virtual Task Cancel(tDownload Download)
        {
            return CancelRange(new[] { Download });
        }

        public virtual Task CancelRange(IEnumerable<tDownload> Downloads)
        {
            return Task.Delay(0);
        }

        public virtual void Clear()
        { }

        public virtual bool CanClose()
        {
            return true;
        }
    }
}
