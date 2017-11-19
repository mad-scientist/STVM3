using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using WMPLib;


namespace STVM.Wrapper.Media
{
    class tMediaFile
    {
        private int duration;
        private int width;
        private int height;

        public int Duration { get { return duration; } }
        public int Width { get { return width; } }
        public int Height { get { return height; } }

        public static tMediaFile ReadFromFile(string Filename)
        {
            int _width = 0;
            int _height = 0;
            ulong _duration = 0;
            const int TICK = 10000000; // unit of measure: 100 nanoseconds

            Type t = Type.GetTypeFromProgID("Shell.Application");
            dynamic shell = Activator.CreateInstance(t);

            Shell32.Folder objFolder = shell.NameSpace(Path.GetDirectoryName(Filename));
            if (objFolder != null)
            {
                Shell32.FolderItem objItem = objFolder.ParseName(Path.GetFileName(Filename));
                if (objItem != null)
                {
                    Shell32.ShellFolderItem objExtItem = (Shell32.ShellFolderItem)objItem;
                    _height = Convert.ToInt32(objExtItem.ExtendedProperty("System.Video.FrameHeight"));
                    _width = Convert.ToInt32(objExtItem.ExtendedProperty("System.Video.FrameWidth"));
                    _duration = Convert.ToUInt64(objExtItem.ExtendedProperty("System.Media.Duration"));
                }
            }

            tMediaFile result = new tMediaFile()
            {
                duration = (int)(_duration / TICK),
                width = _width,
                height = _height
            };
            return result;
        }

        public async static Task<tMediaFile> ReadFromFileAsync(string Filename)
        {
            return await Task.Run(() => ReadFromFile(Filename));
        }

        public string DurationString
        {
            get { return String.Format("{0:0}'", Duration / 60); }
        }

        public string SizeString
        {
            get { return $"{Width}x{Height}"; }
        }
    }

}
