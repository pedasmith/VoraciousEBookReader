using SimpleEpubReader.Database;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SimpleEpubReader
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void OnDownloadGutenberg(object sender, RoutedEventArgs e)
        {
            var ctl = new GutenbergDownloadControl();
            var dlg = new ContentDialog()
            {
                Content = ctl,
                Title = "Downloading",
                PrimaryButtonText = "Ok"
            };
            var result = await dlg.ShowAsync();

        }

        /// <summary>
        /// Given a folder, finds all of the .EPUB files and makes sure they are in the 
        /// download database.
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="getFullData"></param>
        /// <returns></returns>
        public static async Task MarkAllDownloadedFiles(BookDataContext bookdb, IFolder folder, bool getFullData = false)
        {
            var files = await folder.GetFilesAsync();
            foreach (var file in files)
            {
                var filename = file.Name;
                if (filename.ToLower().EndsWith(".epub"))
                {
                    await InsertFileIntoDatabase(bookdb, folder, filename, getFullData);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"{filename} is not an e-book");
                }
            }
        }

    }
}
