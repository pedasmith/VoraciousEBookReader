using SimpleEpubReader.UwpClasses;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace SimpleEpubReader.Database
{
    public enum BookStatus { Error, New, Existing };
    public interface IndexReader
    {
        void BookEnd(BookStatus status, BookData book);
        Task LogAsync(string str);
        void SetFileSize(ulong size);
        Task OnStreamProgressAsync(uint bytesRead);
        Task OnStreamTotalProgressAsync(ulong bytesRead);
        Task OnStreamCompleteAsync();
        CoreDispatcher GetDispatcher();
        Task OnAddNewBook(BookData bookData);
        Task OnTotalBooks(int nbooks); // How many books have been checked (new and old together)
        Task OnReadComplete(int nBooksTotal, int nNewBooks);
    }


    public sealed partial class GutenbergDownloadControl : UserControl, IndexReader
    {
        GutenbergDownloader gd = new GutenbergDownloader();
        public ContentDialog DialogParent = null;
        CancellationTokenSource cts = null;

        ulong FileBytes = 100_000_000; // kind of random amount....
        int NNewBooks;
        public GutenbergDownloadControl()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// In the future, might want to download in the background; it will be nicer for the user
        /// in the case of a slow connection. The catalog files are alas fairly large and are
        /// pretty well non-optimized (e.g., there's no delta catalog)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>

        Task DownloadTask = null;
        private async void OnDownloadOrCancel(object sender, RoutedEventArgs e)
        {
            if (cts == null) // Not running a download; must be a download request
            {
                // Download!
                uiOk.IsEnabled = false;
                uiCancel.IsEnabled = true;
                uiDownloadOrCancel.IsEnabled = false;

                //uiDownloadOrCancel.Content = "Cancel"; // switch to cancel mode
                DownloadTask = DoDownloadAsync();
                await DownloadTask;

                uiOk.IsEnabled = true; // after the download is done, re-enable the button.
                uiCancel.IsEnabled = true;
                uiDownloadOrCancel.IsEnabled = true;
                uiDownloadOrCancel.Content = "Download Again";
            }
            else
            {
                await DoCancelAsync();


                uiOk.IsEnabled = true;
                uiCancel.IsEnabled = true;
                uiDownloadOrCancel.IsEnabled = true;
                uiDownloadOrCancel.Content = "Download again";
            }
        }

        /// <summary>
        /// Is only enabled after a complete download
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (DialogParent != null)
            {
                var dp = DialogParent;
                DialogParent = null;
                dp.Hide(); // take down the dialog
            }
        }

        private async void OnCancel(object sender, RoutedEventArgs e)
        {
            if (cts == null) // Not running a download; must be a download request
            {
                if (DialogParent != null)
                {
                    var dp = DialogParent;
                    DialogParent = null;
                    dp.Hide(); // take down the dialog
                }
            }
            else // is doing a download; cancel it and reset.
            {
                await DoCancelAsync();


                uiOk.IsEnabled = true;
                uiCancel.IsEnabled = true;
                uiDownloadOrCancel.IsEnabled = true;
                uiDownloadOrCancel.Content = "Download again";
            }
        }

        public static Uri CurrentGutenbergCatalogLocation
        {
            get
            {
                var uri = new Uri("http://www.gutenberg.org/cache/epub/feeds/rdf-files.tar.zip");
                return uri;
            }
        }

        DateTimeOffset StartTime;
        /// <summary>
        /// Primary real code to download file and then parse it for new books.
        /// </summary>
        /// <returns></returns>
        private async Task DoDownloadAsync()
        { 
            cts = new CancellationTokenSource();
            var bookdb = BookDataContext.Get();
            // http://www.gutenberg.org/wiki/Gutenberg:Feeds
            // http://www.gutenberg.org/cache/epub/feeds/rdf-files.tar.zip
            var uri = CurrentGutenbergCatalogLocation;
            var folder = FolderMethods.LocalCacheFolder;
            var folderUwp = ApplicationData.Current.LocalCacheFolder;
            var filename = "gutenberg.zip";
            NNewBooks = 0;
            uiProgress.Maximum = FileBytes; // set to a reasonable first amount.

            // Start the actual work

            bool gotfile = await gd.DownloadFrom(this, uri, folderUwp, filename, cts.Token);

            if (gotfile)
            {
                StartTime = DateTimeOffset.Now;
                await LogAsync("Processing catalog\n");
                var fullpath = folder + @"\" + filename;
                //var file = await PCLStorage.FileSystem.Current.GetFileFromPathAsync(fullpath);
                var file = await StorageFile.GetFileFromPathAsync(fullpath);

                int retval = 0;
                uiProgress.Maximum = 80000; // the max is the number of books I expect to process.
                // Max is actually about 65K as of 2020-07-04
                // Don't just look at new books because of the processing times -- it takes a long time
                // to process the 65K of books in the catalog just to get the few new ones at the end.
                uiProgress.Value = 0;

                await Task.Run(async () =>
                {
                    RdfReader.UpdateType updateType = RdfReader.UpdateType.Full;
                    retval = await RdfReader.ReadZipTarRdfFileAsync(this, bookdb, file, cts.Token, updateType);
                    BookDataContext.ResetSingleton(null); // must reset database (otherwise no records can be found)
                    ;
                });
                ;
            }
        }


        public void BookEnd(BookStatus status, BookData book)
        {
        }

        public CoreDispatcher GetDispatcher()
        {
            return this.Dispatcher;
        }

        public async Task LogAsync(string str)
        {
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                uiLog.Text += str;
            });
        }

        public async Task OnHttpProgressAsync(string str)
        {
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                if (str != "") uiLog.Text = str;
            });
        }

        public async Task OnStreamCompleteAsync()
        {
            await Task.Delay(0);
        }

        ulong TotalProgress = 0;
        public async Task OnStreamProgressAsync(uint bytesRead)
        {
            TotalProgress += bytesRead;
            await Task.Delay(0); // so that the compile doesn't complain.
        }
        public async Task OnStreamTotalProgressAsync(ulong bytesRead)
        {
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                if (uiProgress.Maximum != FileBytes)
                {
                    uiProgress.Maximum = FileBytes;
                }
                uiProgress.Value = bytesRead;
            });
        }

        public void SetFileSize(ulong size)
        {
            FileBytes = size; // set to the specific size
        }

        public async Task OnAddNewBook(BookData bookData)
        {
            NNewBooks++;
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                var title = bookData.Title.Replace("\n", " ");
                uiAdding.Text = $"Adding or updating {NNewBooks}: {title}";
                uiLog.Text += $"{bookData.Title}\n";
            });
        }

        public async Task OnTotalBooks(int nbooks)
        {
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                while (uiProgress.Maximum < nbooks)
                {
                    uiProgress.Maximum += 10000;
                }
                uiProgress.Value = nbooks;
                uiTotal.Text = $"Examined catalog entry {nbooks}";
            });
        }


        public async Task OnReadComplete(int nBooksTotal, int nNewBooks)
        {
            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                uiProgress.Value = nBooksTotal;
                uiProgress.Maximum = uiProgress.Value; // we don't actually know how many books there are to process.
                // Jam the progress value to the max so that it looks complete.

                uiTotal.Text = $"Examined catalog entry {nBooksTotal} with {nNewBooks} new books";

                var delta = Math.Round (DateTimeOffset.Now.Subtract(StartTime).TotalMinutes, 2);
                uiLog.Text += $"Read from database complete in {delta} minutes\n";
                uiAdding.Text = $"Completed reading catalog; {nNewBooks}";
            });
        }

        private async Task DoCancelAsync()
        {
            if (cts != null)
            {
                cts.Cancel();
                await Task.Delay(0);
                if (DownloadTask != null)
                {
                    int nloop = 0;
                    while (!DownloadTask.IsCompleted)
                    {
                        await Task.Delay(50); // shouldnt' take long
                        nloop++;
                    }
                    ;
                }
                // TODO: Wait to be fully cancelled????
                cts = null;
            }

        }


    }
}
