using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Xml;
using System.IO;
using STVM.Stv;
using STVM.Stv.Data;
using STVM.Data;

namespace STVM
{
    [Serializable]
    public class SettingsWrapper
    {
        public string StvUsername;
        public string StvPassword;
        public bool StvSavePassword;
        public bool StvMobileQuality;
        public bool StvShowDuplicates;
        public bool StvShowLocalAvailable;
        [OptionalField]
        public bool StvShowHasAdfree;
        [OptionalField]
        public bool StvShowHasNoAdfree;
        [OptionalField]
        public bool StvShowBroken;
        [OptionalField]
        public bool StvHideNoSchnittliste;
        [OptionalField]
        public bool StvHideBroken;
        [OptionalField]
        public bool StvDoNotManageProgramming;
        [OptionalField]
        public bool StvAutoServerUpdate;
        public bool StvPreferAdFree;
        public string StvSortOption;
        public Data.DownloadMethods StvDownloadMethod;
        public string SynoServerURL;
        public bool SynoUseHttps;
        public bool SynoUseSSH;
        [OptionalField]
        public int SynoServerPort;
        public string SynoUsername;
        public string SynoPassword;
        public bool SynoSavePassword;
        public string LocalPathDownloads;
        public string LocalPathSeries;
        public string LocalPathMovies;
        public string LocalPathInfos;
        public string LocalPathOther;
        [OptionalField]
        public string LocalNameSeries;
        public bool LocalShowAll;
        [OptionalField]
        public bool LocalUseSxxExxEpisodeCode;
        [OptionalField]
        public bool UseXbmc;
        public string XbmcUrl;
        public int XbmcPort;
        public string XbmcUser;
        public string XbmcPass;
        [OptionalField]
        public bool UseTxdb;
        [OptionalField]
        public bool UseLocalArchive;
        [OptionalField]
        public System.Drawing.Size Size;
        [OptionalField]
        public System.Drawing.Point Position;
        [OptionalField]
        public bool Maximized;
        [OptionalField]
        public bool AssistantShowDuplicates;
        [OptionalField]
        public bool AssistantShowNonProgrammable;
        [OptionalField]
        public bool AssistantHideNonProgrammable;
        [OptionalField]
        public bool ManageDownloads;
        [OptionalField]
        public bool JDLPluginMode;
        [OptionalField]
        public bool JDLFullService;
        [OptionalField]
        public bool ExternalDlmTelecastLinks;
        [OptionalField]
        public int InternalDlmMaximumConnections;
        [OptionalField]
        public RecordFormats StvDefaultVideoFormat;
        [OptionalField]
        public AutoDownloadOptions AutoDownload;
        [OptionalField]
        public bool AutoDownloadAwaitAdFree;
        [OptionalField]
        public bool AutoDownloadSkipDuplicates;
        [OptionalField]
        public DateTime AutoDownloadSchedule;
        [OptionalField]
        public bool UseWebApi;
        [OptionalField]
        public bool SearchFavoritesExecuteSearch;
        [OptionalField]
        public string StreamingPlayerPath;
        [OptionalField]
        public bool EnableDebugLogging;

        [OnDeserializing]
        // Set default values for new OptionalFields
        internal void OnDeserializing(StreamingContext context)
        {
            SetDefaults();
        }

        [NonSerialized()]
        private string xmlFile;
        const string xmlFilename = "Settings.xml";

        public SettingsWrapper()
        {
            SetDefaults();
        }

        private void SetDefaults()
        {
            StvSavePassword = true;
            StvMobileQuality = false;
            StvShowDuplicates = true;
            StvShowBroken = true;
            StvShowHasAdfree = true;
            StvShowHasNoAdfree = true;
            StvShowLocalAvailable = true;
            StvAutoServerUpdate = true;
            StvPreferAdFree = true;
            StvDoNotManageProgramming = false;
            StvSortOption = "type";
            StvDownloadMethod = Data.DownloadMethods.InternalDlm;
            LocalNameSeries = "<%show%> - <%episodexcode%> - <%episode%>";
            StvHideNoSchnittliste = false;
            StvHideBroken = false;
            LocalUseSxxExxEpisodeCode = false;
            XbmcPort = 80;
            XbmcUser = "kodi";
            UseTxdb = false;
            UseLocalArchive = false;
            Size = new System.Drawing.Size(1000, 760);
            Position = new System.Drawing.Point(20, 20);
            Maximized = false;
            AssistantShowDuplicates = true;
            AssistantHideNonProgrammable = true;
            AssistantShowNonProgrammable = true;
            ManageDownloads = false;
            SynoServerPort = 5000;
            JDLPluginMode = false;
            JDLFullService = false;
            ExternalDlmTelecastLinks = false;
            InternalDlmMaximumConnections = 3;
            StvDefaultVideoFormat = RecordFormats.SD;
            AutoDownload = AutoDownloadOptions.Never;
            AutoDownloadAwaitAdFree = true;
            AutoDownloadSkipDuplicates = true;
            AutoDownloadSchedule = new DateTime(2015, 1, 1, 0, 0, 0);
            UseWebApi = false;
            SearchFavoritesExecuteSearch = false;
            StreamingPlayerPath = "";
            EnableDebugLogging = false;
        }

        public static SettingsWrapper ReadFromXML(string xmlPath)
        {
            SettingsWrapper result;
            string readFile = Path.Combine(xmlPath, xmlFilename);
            if (File.Exists(readFile))
            {
                try
                {
                    DataContractSerializer serializer = new DataContractSerializer(typeof(SettingsWrapper));
                    FileStream readFileStream = new FileStream(readFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                    result = (SettingsWrapper)serializer.ReadObject(readFileStream);
                    readFileStream.Close();
                }
                catch (SerializationException e)
                {
                    System.Windows.Forms.MessageBox.Show("Fehler beim Einlesen der Einstellungen aus " + readFile + "\r\nEinstellungen werden zurückgesetzt.", 
                        "STV MANAGER", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    result = new SettingsWrapper();
                }
            }
            else
            {
                result = new SettingsWrapper();
            }
            result.xmlFile = readFile;
            return result;
        }

        public void SaveToXML()
        {
            if (!StvSavePassword) { StvPassword = ""; }
            if (!SynoSavePassword) { SynoPassword = ""; }

            DirectoryInfo directory = Directory.CreateDirectory(Path.GetDirectoryName(xmlFile));
            if (directory.Exists)
            {
                DataContractSerializer serializer = new DataContractSerializer(typeof(SettingsWrapper));
                XmlTextWriter writeFileStream = new XmlTextWriter(xmlFile, null);
                writeFileStream.Formatting = Formatting.Indented;
                serializer.WriteObject(writeFileStream, this);
                writeFileStream.Flush();
                writeFileStream.Close();
            }
        }

    }

}
