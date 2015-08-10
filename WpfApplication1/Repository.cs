using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.ComponentModel;

namespace PolloUpdater
{
    public class RepositoryData
    {
        public string DownloadRoot;
        public List<List<string>> Files;
    }

    public class Repository : INotifyPropertyChanged
    {
        RepositoryData data;
        public bool inProgress = false;
        public int filesToDownload = 100;
        public int filesDownloaded = 0;
        public string currentlyDownloading = "";
        private string _url;
        public event PropertyChangedEventHandler PropertyChanged;

        public int ProgressToDo
        { 
            get {
                return filesToDownload;
            } 
            set {
                filesToDownload = value;
                OnPropertyChanged("ProgressToDo");
            }
        }

        public int ProgressDone
        {
            get
            {
                return filesDownloaded;
            }
            set
            {
                filesDownloaded = value;
                OnPropertyChanged("ProgressDone");
            }
        }

        public string ProgressFile
        {
            get
            {
                return currentlyDownloading;
            }
            set
            {
                currentlyDownloading = value;
                OnPropertyChanged("ProgressFile");
            }
        }

        public Repository(string url)
        {
            _url = url;
        }

        public Task LoadData()
        {
            Task t = Task.Run(() =>
            {
                ProgressFile = "Downloading repository information...";
                WebRequest request = WebRequest.Create(_url);
                WebResponse response = request.GetResponse();
                Stream rs = response.GetResponseStream();
                string value = new StreamReader(rs).ReadToEnd();
                rs.Close();
                data = JsonConvert.DeserializeObject<RepositoryData>(value);
                ProgressFile = "Ready to synchronize";
            });
            return t;
        }

        public IEnumerable<string> GetDirectoriesFromPath(string path)
        {
            // by convention, arma mod folders start with @
            return Directory.EnumerateDirectories(path, "@*");
        }

        public IEnumerable<string> GetFilesFromDirectories(IEnumerable<string> searchDirectories)
        {
            var listing = new List<string>();
            foreach (var sd in searchDirectories)
            {
                foreach (var i in Directory.EnumerateFiles(sd, "*", SearchOption.AllDirectories))
                {
                    listing.Add(i);
                }
            }
            return listing.AsEnumerable();
        }

        public List<List<string>> CreateRepositoryData(string basePath)
        {
            var files = new List<List<string>>();
            foreach (var filePath in GetFilesFromDirectories(GetDirectoriesFromPath(basePath)))
            {
                using (var sr = new StreamReader(filePath))
                {
                    var hash = CalculateHash(sr.BaseStream);
                    var trimmedPath = filePath.Remove(0, basePath.Length).TrimStart('\\');
                    trimmedPath = trimmedPath.Replace("\\", "/");
                    files.Add(new List<string>() { trimmedPath, hash });
                }
            }
            return files;
        }

        public string CalculateHash(Stream input)
        {
            byte[] hash = (new SHA1Managed()).ComputeHash(input);
            return string.Join("", hash.Select(b => b.ToString("x2")).ToArray());
        }

        public bool ShouldDownloadFile(string filePath, string repositoryHash)
        {
            if (!File.Exists(filePath)) return true;

            using (StreamReader f = new StreamReader(filePath))
            {
                if (CalculateHash(f.BaseStream) != repositoryHash) return true;
            }

            return false;
        }

        public bool DownloadFile(string filePath, string repositoryHash)
        {
            var url = data.DownloadRoot + filePath;
            var request = WebRequest.Create(url);
            var response = request.GetResponse();

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));

            using (var writer = new StreamWriter(filePath, false))
            {
                Stream rs = response.GetResponseStream();

                rs.CopyTo(writer.BaseStream);
                writer.Flush();
                response.Close();
            }

            bool hashOk;
            using (var writtenFile = new StreamReader(filePath))
            {
                hashOk = CalculateHash(writtenFile.BaseStream) == repositoryHash;
            }
            return hashOk;
        }

        public Task Sync()
        {
            Task t = Task.Run(() =>
            {
                if (inProgress) return;

                inProgress = true;
                ProgressToDo = 100;
                ProgressDone = 0;
                ProgressFile = "Checking existing files";

                var directoriesToPrune = new List<string>();

                var collectedFiles = new List<List<string>>();
                foreach (var fileEntry in data.Files)
                {
                    string filePath = fileEntry[0];
                    string repositoryHash = fileEntry[1];

                    char[] sep = { '/' };
                    bool asd = filePath.Contains(sep[0]);
                    string dirName = filePath.Split(sep)[0];

                    if (!directoriesToPrune.Contains(dirName)) directoriesToPrune.Add(dirName);

                    if (ShouldDownloadFile(filePath, repositoryHash))
                    {
                        collectedFiles.Add(fileEntry);
                    }
                }

                ProgressFile = "Pruning old files";
                foreach (var pruneDir in directoriesToPrune)
                {
                    foreach (var item in Directory.EnumerateFiles(pruneDir, "*", SearchOption.AllDirectories))
                    {
                        string slashItem = item.Replace("\\", "/");
                        bool itemBelongsToRepo = false;
                        foreach (var repoItem in data.Files)
                        {
                            string repoItemPath = repoItem[0];
                            if (repoItemPath == slashItem) itemBelongsToRepo = true;
                        }
                        if (!itemBelongsToRepo) 
                        {
                            ProgressFile = string.Format("Pruning {0}", item);
                            File.Delete(item);
                        } 
                        
                    }
                }
                

                ProgressToDo = collectedFiles.Count;

                foreach (var fileEntry in collectedFiles)
                {
                    string filePath = fileEntry[0];
                    string repositoryHash = fileEntry[1];

                    ProgressFile = string.Format("Downloading {0}", filePath);
                    DownloadFile(filePath, repositoryHash);
                    ProgressDone++;
                }
                inProgress = false;
                ProgressFile = string.Format("Synchronization Completed ({0} files changed)", ProgressToDo);
            });
            return t;
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}