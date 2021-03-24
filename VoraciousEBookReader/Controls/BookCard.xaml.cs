using SimpleEpubReader.Database;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System.Net.Http;
using System.Collections.Generic;
using SimpleEpubReader.UwpClasses;

namespace SimpleEpubReader.Controls
{
    /// <summary>
    /// Display a BookCard using a BookData object
    /// </summary>
    public sealed partial class BookCard : UserControl
    {
        public BookCard()
        {
            this.InitializeComponent();
            this.Loaded += BookCard_Loaded;
            this.DataContextChanged += BookCard_DataContextChanged;
        }

        public void CardIsSelected (bool isSelected)
        {
            EnableDownloadPanel(isSelected && AllowEnableDownloadPanel);
        }

        private void BookCard_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            var book = DataContext as BookData;
            //System.Diagnostics.Debug.WriteLine($"DBG: BookCard: DataContext: changed: {book?.Title}");
            SetupUINicely();
        }

        private void BookCard_Loaded(object sender, RoutedEventArgs e)
        {
            var book = DataContext as BookData;
            //System.Diagnostics.Debug.WriteLine($"DBG: BookCard: Loaded: changed: {book?.Title}");
            SetupUINicely();
        }

        private Visibility Vis(string value1, string value2=null, string value3=null)
        {
            return string.IsNullOrWhiteSpace(value1) && string.IsNullOrWhiteSpace(value2) && string.IsNullOrWhiteSpace(value3) 
                ? Visibility.Collapsed 
                : Visibility.Visible;
        }

        public BookData GetBookData()
        {
            var book = DataContext as BookData;
            return book;
        }

        bool AllowEnableDownloadPanel = false;

        private void SetupUINicely()
        {
            var book = DataContext as BookData;
            if (book == null) return;
            var notTwo = (string.IsNullOrWhiteSpace(book.LCC) || string.IsNullOrWhiteSpace(book.LCSH));
            uiLCCSep.Text = notTwo ? "" : " : ";

            uiTitleAlternative.Visibility = Vis(book.TitleAlternative);
            uiBookSeries.Visibility = Vis(book.BookSeries);
            uiPGNotes.Visibility = Vis(book.PGNotes);
            uiLCC.Visibility = Vis(book.LCC, book.LCCN, book.LCSH);
            ResetDownloadPanel();
            EnableDownloadPanel(false);
            EnableDownloadProgressPanel(false);
        }

        public void EnsureDownloadPanel()
        {
            if (uiDownloadList.Items.Count == 0)
            {
                ResetDownloadPanel();
            }
        }

        public void ResetDownloadPanel()
        {
            var book = DataContext as BookData;

            // Set up the uiDownload combo box
            uiDownloadList.Items.Clear();
            // Get a subset of the offered items -- we don't need to see multiple
            // text files with different character sets when we already have just
            // the one we need.
            var list = FilenameAndFormatData.GetProcessedFileList(book.Files.ToList());
            foreach (var file in list)
            {
                var cbi = new ComboBoxItem()
                {
                    Content = file.FileTypeAsString(),
                    Tag = file
                };
                uiDownloadList.Items.Add(cbi);
            }
            if (uiDownloadList.Items.Count > 0) uiDownloadList.SelectedIndex = 0;

            var bookdb = BookDataContext.Get();
            var dd = CommonQueries.DownloadedBookFind(bookdb, book.BookId);
            var fileStatus = dd?.CurrFileStatus ?? DownloadData.FileStatus.Unknown;
            switch (fileStatus)
            {
                case DownloadData.FileStatus.Unknown:
                case DownloadData.FileStatus.Deleted:
                    AllowEnableDownloadPanel = true;
                    break;
                case DownloadData.FileStatus.Downloaded:
                    AllowEnableDownloadPanel = false;
                    break;
            }
            uiDownloadButton.Visibility = Visibility.Visible;
            uiCancelDownloadButton.Visibility = Visibility.Collapsed;
            uiDownloadProgress.IsIndeterminate = false;
            uiDownloadProgress.Value = 0;

            uiDownloadFinished.Visibility = Visibility.Collapsed;
        }

        private async void OnDownloadFile(object sender, RoutedEventArgs e)
        {
            var file = (uiDownloadList.SelectedItem as ComboBoxItem)?.Tag as FilenameAndFormatData;
            if (file == null) return;
            var book = DataContext as BookData;
            if (book == null) return;
            var bookdb = BookDataContext.Get();
            await DownloadBookAsync(bookdb, book, file);
        }

        public async Task DoDownloadAsync()
        {

            var file = (uiDownloadList.SelectedItem as ComboBoxItem)?.Tag as FilenameAndFormatData;
            if (file == null) return;
            var book = DataContext as BookData;
            if (book == null) return;
            var bookdb = BookDataContext.Get();
            await DownloadBookAsync(bookdb, book, file);
        }

        const string ReadFolder = "read";


        static HttpClient hc = new HttpClient();
        CancellationTokenSource cts;

        private void NotifyUser(string str)
        {
            uiLog1.Text = str;
            //uiLog2.Text = str;
        }

        private async Task DownloadBookAsync(BookDataContext bookdb, BookData book, FilenameAndFormatData file)
        {
            Uri uri;
            var fileName = FileWizards.UriWizard.FixupUrl(file.FileName);
            var status = Uri.TryCreate(fileName, UriKind.Absolute, out uri);
            if (!status)
            {
                var md = new MessageDialog($"Internal error: {fileName} is invalid")
                {
                    Title = "Can't download file",
                };
                await md.ShowAsync(); 
                return; 
            }
            // Uri is e.g. http://www.gutenberg.org/ebooks/14.epub.noimages

            var folder = await FolderMethods.EnsureDownloadFolder();
            var (outfilename, ext) = FileWizards.UriWizard.GetUriFilename(uri);
            var preferredFilename = book.GetBestTitleForFilename() + ext;

            var collisionOption = PCLStorage.CreationCollisionOption.FailIfExists;
            var exists = await folder.CheckExistsAsync(preferredFilename);
            if (exists == PCLStorage.ExistenceCheckResult.FileExists)
            {
                var md = new ContentDialog() { 
                    Content = $"Already downloaded file {preferredFilename}",
                    Title = "File exists", 
                    PrimaryButtonText = "Skip",
                    SecondaryButtonText = "Delete existing file",
                };
                var command = await md.ShowAsync();
                switch (command)
                {
                    case ContentDialogResult.Primary:
                        return;
                    case ContentDialogResult.Secondary:
                        collisionOption = PCLStorage.CreationCollisionOption.ReplaceExisting;
                        break;
                }
            }
            PCLStorage.IFile outfile;
            try
            {
                outfile = await folder.CreateFileAsync(preferredFilename, collisionOption);
            }
            catch (Exception ex)
            {
                var md = new MessageDialog($"Already downloaded file {preferredFilename} {ex.Message}")
                {
                    Title = "File already exists",
                };
                await md.ShowAsync();
                return;
            }

            // Just do a quick download....
            try
            {
                EnableDownloadProgressPanel(true);
                uiDownloadButton.Visibility = Visibility.Collapsed;
                uiCancelDownloadButton.Visibility = Visibility.Visible;
                cts = new CancellationTokenSource();

                ulong totalLength = 0;
                uiDownloadProgress.IsIndeterminate = true;
                uiDownloadProgress.ShowError = false;
                var getTask = hc.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
#if NEVER_EVER_DEFINED
                getTask.Progress += async (response, progress) =>
                {
                    await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                        () =>
                        {
                            if (totalLength == 0 && progress.TotalBytesToReceive.HasValue && progress.TotalBytesToReceive.Value > 0)
                            {
                                // Now we know how many bytes we will get!
                                uiDownloadProgress.IsIndeterminate = false;
                                uiDownloadProgress.Minimum = 0;
                                uiDownloadProgress.Maximum = progress.TotalBytesToReceive.Value;
                                uiDownloadProgress.Value = 0;
                                totalLength = progress.TotalBytesToReceive.Value;
                            }
                            if (progress.BytesReceived > 0)
                            {
                                uiDownloadProgress.Value = progress.BytesReceived;
                            }
                            NotifyUser ($"GET Progress: stage {progress.Stage} bytes={progress.BytesReceived} of {progress.TotalBytesToReceive}");
                        });
                };
#endif
                // Get some level of progress....
                var responseMessage = await getTask;
                var contentLengthHeader = responseMessage.Content.Headers.ContentLength;
                if (totalLength == 0 && contentLengthHeader.HasValue && contentLengthHeader.Value > 0)
                {
                    // Now we know how many bytes we will get!
                    uiDownloadProgress.IsIndeterminate = false;
                    uiDownloadProgress.Minimum = 0;
                    uiDownloadProgress.Maximum = contentLengthHeader.Value;
                    uiDownloadProgress.Value = 0;
                    totalLength = (ulong)contentLengthHeader.Value;
                }
                NotifyUser($"GET Progress: stage Got Headers");


                var stream = await responseMessage.Content.ReadAsStreamAsync(); // gest entire buffer at once.
                byte[] tbuffer = new byte[8 * 1024];
                List<byte> buffer = new List<byte>();
                int nbytes = 0;
                int progress = 0;
                while ((nbytes = await stream.ReadAsync(tbuffer, 0, tbuffer.Length)) > 0)
                {
                    for (int i = 0; i < nbytes; i++) buffer.Add(tbuffer[i]);
                    await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,
                        () =>
                        {
                            progress += nbytes;
                            uiDownloadProgress.Value = progress;
                            NotifyUser($"GET Progress: bytes={progress} of {totalLength}\n");
                        });
                }

                //var buffer = await bufferTask.AsTask(cts.Token);
                uiDownloadProgress.Value = buffer.Count;

                bool isOk = responseMessage.IsSuccessStatusCode;
                if (isOk)
                {
                    NotifyUser($"GET complete with {buffer.Count} bytes");

                    if (buffer.Count > 2000)
                    {
                        uiDownloadFinished.Visibility = Visibility.Visible;
                        uiDownloadFinishedShowButton.Visibility = Visibility.Collapsed;

                        await outfile.WriteBytesAsync(buffer);
                        CommonQueries.DownloadedBookEnsureFileMarkedAsDownloaded(bookdb, book.BookId, folder.Path, outfile.Name);

                        uiDownloadFinishedShowButton.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        var md = new MessageDialog($"Download file error -- too small")
                        {
                            Title = "Corrupt downloaded file",
                        };
                        await md.ShowAsync();
                    }
                    NotifyUser($"Download complete -- file size {buffer.Count} bytes");
                }
                else
                {
                    NotifyUser($"Failed to download file; error {responseMessage.ReasonPhrase} length {buffer.Count}");
                    uiDownloadFinished.Visibility = Visibility.Visible;
                    uiDownloadFinishedShowButton.Visibility = Visibility.Collapsed;
                }


                uiDownloadProgress.IsIndeterminate = false;
                uiDownloadProgress.Value = 0;
                EnableDownloadProgressPanel(false);
            }
            catch (Exception ex)
            {
                NotifyUser($"Unable to download file because {ex.Message}");
                await outfile.DeleteAsync();

                uiDownloadProgress.IsIndeterminate = false;
                uiDownloadProgress.ShowError = true;
                EnableDownloadProgressPanel(false);
            }
            finally
            {
                uiDownloadButton.Visibility = Visibility.Visible;
                uiCancelDownloadButton.Visibility = Visibility.Collapsed;
            }
        }

        private void OnCancelDownload(object sender, RoutedEventArgs e)
        {
            cts?.Cancel();
        }

        private void EnableDownloadProgressPanel(bool enable)
        {
            uiDownloadProgressPanel.Visibility = enable ? Visibility.Visible : Visibility.Collapsed;
        }

        private void EnableDownloadPanel(bool enable)
        {
            var book = DataContext as BookData;
            //System.Diagnostics.Debug.WriteLine($"DBG: BookCard: EnableDownloadPanel: enable={enable}: {book?.Title}");
            uiDownloadPanel.Visibility = enable ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnReadNow(object sender, RoutedEventArgs e)
        {
            var nav = Navigator.Get();
            var parentControlId = Navigator.NavigateControlId.BookSearchDisplay;
            nav.DisplayBook(parentControlId, DataContext as BookData);
        }
    }
}
