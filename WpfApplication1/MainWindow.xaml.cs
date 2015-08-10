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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Security.Cryptography;

namespace PolloUpdater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Repository r;
        const string url = "https://koti.kapsi.fi/darkon/polloeskadroona/repo/updater.json";

        public MainWindow()
        {
            InitializeComponent();

            r = new Repository(url);
            DataContext = r;
            LoadRepositoryData();
        }

        private async void LoadRepositoryData()
        {
            StartButton.IsEnabled = false;
            DownloadProgressBar.IsIndeterminate = true;
            await r.LoadData();
            DownloadProgressBar.IsIndeterminate = false;
            StartButton.IsEnabled = true;
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            StartButton.IsEnabled = false;
            await r.Sync();
            StartButton.IsEnabled = true;
        }

    }

}
