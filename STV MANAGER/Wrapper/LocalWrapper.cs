using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Xml;
using System.IO;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Reflection;
using STVM.Data;
using STVM.Stv.Data;
using STVM.Wrapper.Tvdb;
using STVM.Wrapper.Tmdb;
using STVM.Helper;


namespace STVM.Wrapper.Local
{
    [DataContract (Namespace = "", Name = "Local_Wrapper")]
    public class localWrapper
    {
        [DataMember]
        public string ShowsBasePath;
        [DataMember]
        public string MoviesBasePath;

        [DataMember]
        public tShowCollection Shows;
        [DataMember]
        public tEpisodeCollection Episodes;
        [DataMember]
        public tMovieCollection Movies;

        public bool UseSxxExxEpisodeCode;

        public tShow Show(int ID)
        {
            tShow result = this.Shows.Find(ID);
            if (result == null)
            {
                result = new tShow();
            }
            return result;
        }

        public tMovie Movie(int ID)
        {
            if (ID != 0)
            {
                tMovie result = this.Movies.Find(ID);
                if (result != null)
                {
                    return result;
                }
                else return new tMovie();
            }
            else return new tMovie();
        }

        public tShow Show(tEpisode Episode)
        {
            tShow result = this.Shows.Find(Episode.ShowID);
            if (result != null)
            {
                return result;
            }
            else
            {
                return new tShow();
            }
        }

        public tEpisode Episode(int ID)
        {
            if (ID != 0)
            {
                tEpisode result = this.Episodes.Find(ID);
                if (result != null)
                {
                    return result;
                }
                else return new tEpisode();
            }
            else return new tEpisode();
        }

        public localWrapper()
        {
            ShowsBasePath = "";
            Shows = new tShowCollection();
            Episodes = new tEpisodeCollection();
            Movies = new tMovieCollection();
        }

        const string xmlFilename = "LocalArchive.xml";
        private string xmlFile;

        public static localWrapper ReadFromXML(string xmlPath)
        {
            localWrapper result;
            string readFile = Path.Combine(xmlPath, xmlFilename);

            if (File.Exists(readFile))
            {
                try
                {
                    DataContractSerializer serializer = new DataContractSerializer(typeof(localWrapper));
                    FileStream readFileStream = new FileStream(readFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                    result = (localWrapper)serializer.ReadObject(readFileStream);
                    readFileStream.Close();
                }
                catch (SerializationException e)
                {
                    System.Windows.Forms.MessageBox.Show("Fehler beim Einlesen von " + readFile + "\r\nDaten werden zurückgesetzt.",
                        "STV MANAGER", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    result = new localWrapper();
                }
            }
            else
            {
                result = new localWrapper();
            }
            result.xmlFile = readFile;
            return result;
        }

        public void SaveToXML()
        {
            DirectoryInfo directory = Directory.CreateDirectory(Path.GetDirectoryName(xmlFile));
            if (directory.Exists)
            {
                DataContractSerializer serializer = new DataContractSerializer(typeof(localWrapper));
                XmlTextWriter writeFileStream = new XmlTextWriter(xmlFile, null);
                writeFileStream.Formatting = Formatting.Indented;
                serializer.WriteObject(writeFileStream, this);
                writeFileStream.Flush();
                writeFileStream.Close();
            }
        }

        public void Remove(tShow Show)
        {
            Remove(Show, true);
        }

        public void Remove (tShow Show, bool DeleteIfNotEmpty)
        {
            if (this.Episodes.Show(Show).Local().Count() == 0 | DeleteIfNotEmpty)
            {
                // Episoden löschen
                this.Episodes.RemoveAll(episode => episode.ShowID == Show.ID);
                // Show löschen
                this.Shows.Remove(Show);
            }
        }

        public void Remove(tMovie Movie)
        {
            Remove(Movie, true);        
        }

        public void Remove(tMovie Movie, bool DeleteIfNotEmpty)
        {
            if (Movie.Filename == "" | DeleteIfNotEmpty)
                this.Movies.Remove(Movie);
        }

        public void UpdateArchive()
        {
            if (Directory.Exists(ShowsBasePath))
            {
                foreach (string Folder in Directory.GetDirectories(ShowsBasePath))
                {
                    this.ReadShowFolder(Folder);
                }
            }

            if (Directory.Exists(MoviesBasePath))
            {
                this.ReadMovieFolder(MoviesBasePath);
            }

            SaveToXML();
        }

        public void Refresh()
        {
            if (MessageBox.Show(
                "Soll die gesamte Datenbank mit " + Shows.Count().ToString() + " Serien wirklich neu aufgebaut werden?",
                "STV MANAGER", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Shows.Clear();
                UpdateArchive();
            }
        }

        public void ReadEpisodeQuality(tEpisode Episode)
        {
            Type t = Type.GetTypeFromProgID("Shell.Application");
            dynamic shell = Activator.CreateInstance(t);

            Shell32.Folder objFolder = shell.NameSpace(Path.GetDirectoryName(Episode.Filename));
            if (objFolder != null)
            {
                Shell32.FolderItem objItem = objFolder.ParseName(Path.GetFileName(Episode.Filename));
                if (objItem != null)
                {
                    Shell32.ShellFolderItem objExtItem = (Shell32.ShellFolderItem)objItem;
                    int videoLines = Convert.ToInt32(objExtItem.ExtendedProperty("System.Video.FrameHeight"));
                    Episode.isHd = videoLines >= 700;
                }
            }
        }

        public async Task ReadEpisodeFolder(tShow Show, string Foldername)
        {
            List<tEpisode> result = new List<tEpisode>();
            if (Directory.Exists(Foldername))
            {
                Shell32.Shell shell = new Shell32.Shell();
                Shell32.Folder objFolder = shell.NameSpace(Foldername);
                foreach (Shell32.FolderItem2 item in objFolder.Items())
                {
                    if (item.IsFolder) { await ReadEpisodeFolder(Show, item.Path); }
                    else if (Enum.GetNames(typeof(VideoFormats)).Contains(Path.GetExtension(item.Path).Replace(".", "")))
                    {
                        tEpisode episodeFile = new tEpisode();
                        if (episodeFile.ReadFromFilename(item.Path))
                        {
                            // Episode in der Datenbank suchen
                            tEpisode newEpisode = this.Episodes.Show(Show).Find(episodeFile.Season, episodeFile.Episode);

                            // Dateiname zuordnen
                            if (newEpisode != null)
                            {
                                newEpisode.Filename = episodeFile.Filename;
                            }
                            // wenn Episode noch nicht in lokaler Datenbank, dann hinzufügen
                            else
                            {
                                newEpisode = episodeFile;
                                newEpisode.ShowID = Show.ID;
                                this.Episodes.Add(newEpisode);
                            }
                            await Task.Run(() => ReadEpisodeQuality(newEpisode));
                        }
                    }
                }
            }
        }

        public async Task ReadShowFolder(string FolderName)
        {
            tShow result = new tShow();

            // Ordner bereits definiert?
            result = Shows.Find(show => show.Foldername.Equals(FolderName, StringComparison.CurrentCultureIgnoreCase));

            // Serie im Ordner definieren
            if (result == null)
            {
                TvdbShow addShow = new TvdbShow
                {
                    BasePath = ShowsBasePath
                };
                if (await addShow.SearchFromFoldername(FolderName, true))
                {
                    result = addShow.Show();
                    Shows.Add(result);
                    Episodes.AddRange(addShow.Episodes());
                }
            }

            // wenn Serie definiert, Dateien einlesen
            if (result != null && Directory.Exists(result.Foldername))
            {
                await ReadEpisodeFolder(result, result.Foldername);
            }
        }

        public void ReadMovieFolder(string Foldername)
        {
            if (Directory.Exists(Foldername))
            {
                foreach (string videoFormat in Enum.GetNames(typeof(VideoFormats)))
                {
                    foreach (string Filename in Directory.GetFiles(Foldername, "*." + videoFormat, SearchOption.AllDirectories))
                    {
                        // Titel und Jahr aus Dateiname einlesen
                        tMovie movieFile = new tMovie();
                        movieFile.Filename = Filename;
                        if (movieFile.ReadFilename())
                        {
                            // Film in der TMDb suchen
                            fmAddMovie tmdb = new fmAddMovie();
                            tmdb.StvTitle = movieFile.Title;
                            if (tmdb.Search(true))
                            {
                                // falls noch nicht vorhanden, in die lokale Datenbank einfügen
                                tMovie addMovie = this.Movies.FindOrAdd(tmdb.Movie());
                                addMovie.Filename = Filename;
                            }
                        }
                    }
                }
            }
        }

        public async Task<tShow> AddFromTvdbTitle(string TvdbTitle, bool SelectFirstHit)
        {
            TvdbShow addShow = new TvdbShow
            {
                BasePath = ShowsBasePath,
                KnownShows = this.Shows
            };

            if (await addShow.SearchFromTVDBTitle(TvdbTitle, SelectFirstHit))
            {
                tShow result = addShow.Show();
                if (this.Shows.Find(result.ID) == null)
                {
                    this.Shows.Add(result);
                    this.Episodes.AddRange(addShow.Episodes());
                }
                if (result.Foldername != "")
                {
                    ReadEpisodeFolder(result, result.Foldername);
                }
                return result;
            }
            else return null;
        }

        public TreeNode ReadDirectories(string Title, string Foldername)
        {
            TreeNode result = new TreeNode()
            {
                Text = Title,
                Name = Foldername,
                Tag = Title
            };
            if (Directory.Exists(Foldername))
            {
                foreach (string Folder in Directory.GetDirectories(Foldername).OrderBy(folder => Path.GetFileName(folder)))
                {
                    result.Nodes.Add(new TreeNode()
                    {
                        Text = Path.GetFileName(Folder),
                        Name = Folder,
                        Tag = Title
                    });
                }
            }
            return result;
        }

        public string GetDefaultFilename(int ShowID, int EpisodeID)
        {
            return (
                Show(ShowID).Title + " - " +
                Episode(EpisodeID).EpisodeCode(UseSxxExxEpisodeCode) + " - " +
                Episode(EpisodeID).Title
                );
        }

        public string GetDefaultFilename(int MovieID)
        {
            return (
                Movie(MovieID).Title + " (" + 
                Movie(MovieID).Year.ToString() + ")"
                );
        }
    }

}
