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
        

        public MainWindow()
        {
            InitializeComponent();

            r = new Repository(Repository.DefaultUrl);
            DataContext = r;
            LoadRepositoryData();
        }

        private async void LoadRepositoryData()
        {
            StartButton.IsEnabled = false;
            DownloadProgressBar.IsIndeterminate = true;
            try
            {
                await r.LoadData();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(string.Format("Unhandled exception: {0}", ex.Message));
            }
            DownloadProgressBar.IsIndeterminate = false;
            StartButton.IsEnabled = true;
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            StartButton.IsEnabled = false;
            try
            {
                await r.Sync();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(string.Format("Unhandled exception: {0}", ex.Message));
            }
            
            StartButton.IsEnabled = true;
        }

        private void OpenCreateWindow_Click(object sender, RoutedEventArgs e)
        {
            var w = new CreateRepositoryWindow();
            w.Show();
        }

    }

}
