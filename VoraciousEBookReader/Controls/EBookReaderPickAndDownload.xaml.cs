using Microsoft.Toolkit.Uwp.Helpers;
using SimpleEpubReader.Database;
using SimpleEpubReader.Searching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SimpleEpubReader.Controls
{
    public sealed partial class EBookReaderPickAndDownload : UserControl
    {
        public const string EBOOKREADERFOLDERFILETOKEN = "EBOOKREADER_FOLDER";
        public ObservableCollection<BookData> Books { get; } = new ObservableCollection<BookData>();
        public EBookReaderPickAndDownload()
        {
            this.InitializeComponent();
            this.DataContext = this;
            this.Loaded += EBookReaderPickAndDownload_Loaded;
        }

        private void EBookReaderPickAndDownload_Loaded(object sender, RoutedEventArgs e)
        {
            //await UpdateList();
        }

        private IList<BookData> GetSelectedBooks()
        {
            var retval = new List<BookData>();
            foreach (var book in Books)
            {
                var lvi = uiBookList.ContainerFromItem(book) as ListViewItem;
                var check = lvi.ContentTemplateRoot as CheckBox; // must be kept in sync with the XAML, of course.
                if (check.IsChecked.Value)
                {
                    retval.Add(book);
                }
            }
            return retval;
        }

        private async Task UpdateList()
        { 
            // Get list of currently-reading books

            var startTime = DateTime.Now;
            var search = "";
            var searchType = uiShowDownloaded.IsChecked.Value ? "Downloaded" : "Reading";
            var sortBy = "title";
            var language = "en";

            List<BookData> resultList = null;
            bool andMore = false;

            Books.Clear();

            var searchTask = Task.Run(() => {
                var bookdb = BookDataContext.Get();
                lock (bookdb)
                {
                    resultList = AllBookSearch.SearchInternal(bookdb, searchType, search, language, sortBy, out andMore);
                }
            });
            await searchTask;

            // Finally add to the output
            foreach (var book in resultList)
            {
                Books.Add(book);
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            var parent = this.Parent as ContentDialog;
            parent.Hide();
        }

        private async void OnShowChange(object sender, RoutedEventArgs e)
        {
            await UpdateList();
        }

        private void OnSelectAllCheck(object sender, RoutedEventArgs e)
        {
            CheckAll(true);
        }

        private void OnSelectAllUnchecked(object sender, RoutedEventArgs e)
        {
            CheckAll(false);
        }

        private void CheckAll(bool newCheck)
        {
            foreach (var book in Books)
            {
                var lvi = uiBookList.ContainerFromItem(book) as ListViewItem;
                var check = lvi.ContentTemplateRoot as CheckBox; // must be kept in sync with the XAML, of course.
                check.IsChecked = newCheck;
            }
        }

        private async void OnSend(object sender, RoutedEventArgs e)
        {
            int nok = 0;
            int nfail = 0;

            var bookdb = BookDataContext.Get();
            var selectedBooks = GetSelectedBooks();
            StorageFolder folder = null;
            var picker = new FolderPicker()
            {
                CommitButtonText = "Pick eBook Reader folder",
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SettingsIdentifier = EBOOKREADERFOLDERFILETOKEN,
            };
            picker.FileTypeFilter.Add("*");
            folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                foreach (var bookData in selectedBooks)
                {
                    var srcfullname = bookData.DownloadData.FullFilePath;
                    var fname = bookData.DownloadData.FileName;
                    try
                    {
                        var src = await StorageFile.GetFileFromPathAsync(srcfullname);
                        var exists = await folder.FileExistsAsync(fname);
                        if (!exists)
                        {
                            Logger.Log($"COPY: copying {fname}");
                            await src.CopyAsync(folder, fname, NameCollisionOption.FailIfExists);
                        }
                        else
                        {
                            Logger.Log($"COPY: no need to copy {fname}");
                            ; //NOTE: possibly in the future I'll do something useful here -- like offer to 
                            // re-copy the file, or verify that it's at least the same size or something.
                        }
                        var nd = CommonQueries.BookNavigationDataEnsure(bookdb, bookData);
                        nd.CurrStatus = BookNavigationData.UserStatus.CopiedToEBookReader;
                        nok++;
                    }
                    catch (Exception ex)
                    {
                        nfail++;
                        Logger.Log($"ERROR: COPY: exception when copying {fname} message {ex.Message}");
                    }
                }
            }

            Logger.Log($"COPY: OK={nok} FAIL={nfail}");

            if (nok > 0)
            {
                CommonQueries.BookSaveChanges(bookdb);
            }

            // End by closing the dialog box.
            var parent = this.Parent as ContentDialog;
            parent.Hide();
        }
    }
}
