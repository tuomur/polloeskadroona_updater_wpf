using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.IO;
using Newtonsoft.Json;
using MahApps.Metro.Controls;

namespace PolloUpdater
{
    /// <summary>
    /// Interaction logic for CreateRepositoryWindow.xaml
    /// </summary>
    public partial class CreateRepositoryWindow : MetroWindow
    {
        Repository r;
        public CreateRepositoryWindow()
        {
            InitializeComponent();
            r = new Repository(null);
            DataContext = r;
            RepoProgress.Visibility = Visibility.Hidden;
            foreach (var item in r.GetDirectoriesFromPath(Directory.GetCurrentDirectory()))
            {
                var cb = new CheckBox() { Content = System.IO.Path.GetFileName(item) };
                ListOfFolders.Items.Add(cb);
            }
            
        }

        private async void CreateRepo_Click(object sender, RoutedEventArgs e)
        {
            CreateRepo.IsEnabled = false;
            RepoProgress.Visibility = Visibility.Visible;

            var asd = new List<string>();
            foreach (CheckBox item in ListOfFolders.Items)
            {
                if (item.IsChecked.HasValue)
                {
                    if ((bool)item.IsChecked) asd.Add((string)item.Content);
                }
            }

            if (asd.Count > 0)
            {
                await Task.Run(() =>
                {
                    var newRepo = new RepositoryData();
                    newRepo.DownloadRoot = Repository.DefaultUrlBase;

                    newRepo.Files = r.CreateRepositoryData(asd.AsEnumerable());
                    string output = JsonConvert.SerializeObject(newRepo);

                    try
                    {
                        using (var wr = new StreamWriter("updater.json", false))
                        {
                            wr.Write(output);
                            wr.Flush();
                        }
                        MessageBox.Show("Metadata created successfully.");
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Unable to write metadata file.");
                    }
                });
            }

            CreateRepo.IsEnabled = true;
            RepoProgress.Visibility = Visibility.Hidden;
        }
    }
}
