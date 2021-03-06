﻿using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.ComponentModel;
using PropertyChanged;

namespace PolloUpdater
{
    public class RepositoryData
    {
        [JsonProperty("DownloadRoot")]
        public string DownloadRoot;
        [JsonProperty("Files")]
        public List<List<string>> Files;
    }

    [ImplementPropertyChanged]
    public class Repository
    {
        RepositoryData data;
        public bool inProgress = false;
        private string _url;
        public const string DefaultUrlBase = "https://koti.kapsi.fi/darkon/polloeskadroona/repo/";
        public const string DefaultUrl = DefaultUrlBase + "updater.json";

        public int ProgressToDo { get; set; }
        public int ProgressDone { get; set; }
        public string ProgressFile { get; set; }
        public List<List<string>> IncomingChanges { get; set; }
        public List<string> PruneChanges { get; set; }

        public int CreateRepoProgressToDo { get; set; }
        public int CreateRepoProgressDone { get; set; }

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

                GetChangedFiles();
                GetFilesToPrune();
                ProgressFile = string.Format("Pending {0} changes", IncomingChanges.Count + PruneChanges.Count);
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

        public List<List<string>> CreateRepositoryData(IEnumerable<string> paths)
        {
            CreateRepoProgressToDo = 100;
            CreateRepoProgressDone = 0;
            var files = new List<List<string>>();
            var found = GetFilesFromDirectories(paths);
            CreateRepoProgressToDo = found.Count();
            foreach (var filePath in found)
            {
                using (var sr = new StreamReader(filePath))
                {
                    var hash = CalculateHash(sr.BaseStream);
                    var trimmedPath = filePath.Replace("\\", "/");
                    files.Add(new List<string>() { trimmedPath, hash });
                    CreateRepoProgressDone++;
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

        public void GetChangedFiles()
        {
            var collectedFiles = new List<List<string>>();
            foreach (var fileEntry in data.Files)
            {
                string filePath = fileEntry[0];
                string repositoryHash = fileEntry[1];

                if (ShouldDownloadFile(filePath, repositoryHash))
                {
                    collectedFiles.Add(fileEntry);
                }
            }
            IncomingChanges = collectedFiles;
            ProgressToDo = IncomingChanges.Count;
        }

        void GetFilesToPrune()
        {
            var directoriesToPrune = new List<string>();
            foreach (var fileEntry in data.Files)
            {
                string filePath = fileEntry[0];

                char[] sep = { '/' };
                string dirName = filePath.Split(sep)[0];

                if (!directoriesToPrune.Contains(dirName)) directoriesToPrune.Add(dirName);
            }

            var filesToPrune = new List<string>();

            foreach (var pruneDir in directoriesToPrune)
            {
                if (!Directory.Exists(pruneDir)) continue;

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
                        filesToPrune.Add(item);
                    }
                }
            }

            PruneChanges = filesToPrune;
        }

        void PruneOldFiles()
        {
            ProgressFile = "Pruning old files";
            foreach (var item in PruneChanges)
            {
                ProgressFile = string.Format("Pruning {0}", item);
                File.Delete(item);
            }
            ProgressFile = "";
        }

        public Task Sync()
        {
            return Task.Run(() =>
            {
                if (inProgress) return;

                inProgress = true;
                ProgressToDo = 100;
                ProgressDone = 0;
                ProgressFile = "Checking existing files";
                if (IncomingChanges == null) GetChangedFiles();
                if (PruneChanges == null) GetFilesToPrune();
                ProgressToDo = IncomingChanges.Count + PruneChanges.Count;

                foreach (var fileEntry in IncomingChanges)
                {
                    string filePath = fileEntry[0];
                    string repositoryHash = fileEntry[1];

                    ProgressFile = string.Format("Downloading {0}", filePath);
                    DownloadFile(filePath, repositoryHash);
                    ProgressDone++;
                }

                PruneOldFiles();

                inProgress = false;
                ProgressFile = string.Format("Synchronization Completed ({0} files changed)", ProgressToDo);
                IncomingChanges = null;
                PruneChanges = null;
            });
        }
    }
}